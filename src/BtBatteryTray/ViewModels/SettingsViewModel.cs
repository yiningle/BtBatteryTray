using System.Collections.ObjectModel;
using System.ComponentModel;
using BtBatteryTray.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BtBatteryTray.ViewModels;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    readonly SettingsService _settings;
    readonly BatteryMonitor _monitor;

    public SettingsViewModel(SettingsService settings, BatteryMonitor monitor)
    {
        _settings = settings;
        _monitor = monitor;
        _monitor.Updated += OnMonitorUpdated;
        _settings.PropertyChanged += OnSettingsChanged;
        RebuildDevices();
    }

    public ObservableCollection<DeviceItemViewModel> Devices { get; } = new();

    public bool HasNoDevices => Devices.Count == 0;

    [ObservableProperty] public partial string LastRefreshText { get; set; } = "";

    public double MinThreshold => SettingsService.MinThreshold;
    public double MaxThreshold => SettingsService.MaxThreshold;
    public double MinInterval => SettingsService.MinInterval;
    public double MaxInterval => SettingsService.MaxInterval;

    public string ThresholdHint => $"低于阈值时提醒一次；电量回升到阈值 +{LowBatteryAlerter.Hysteresis}% 以上后才会再次提醒。";

    // NumberBox works in doubles and yields NaN when cleared; the service keeps clamped ints.
    public double Threshold
    {
        get => _settings.Threshold;
        set
        {
            if (!double.IsNaN(value))
                _settings.Threshold = Math.Clamp((int)Math.Round(value), SettingsService.MinThreshold, SettingsService.MaxThreshold);
            OnPropertyChanged();
        }
    }

    public double IntervalMinutes
    {
        get => _settings.IntervalMinutes;
        set
        {
            if (!double.IsNaN(value))
                _settings.IntervalMinutes = Math.Clamp((int)Math.Round(value), SettingsService.MinInterval, SettingsService.MaxInterval);
            OnPropertyChanged();
        }
    }

    public bool AutoStart
    {
        get => _settings.AutoStart;
        set => _settings.AutoStart = value;
    }

    [RelayCommand]
    Task Refresh() => _monitor.RefreshAsync();

    public void Dispose()
    {
        _monitor.Updated -= OnMonitorUpdated;
        _settings.PropertyChanged -= OnSettingsChanged;
    }

    void OnMonitorUpdated(object? sender, EventArgs e) => RebuildDevices();

    void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingsService.Threshold) or nameof(SettingsService.IntervalMinutes) or nameof(SettingsService.AutoStart))
            OnPropertyChanged(e.PropertyName);
    }

    void RebuildDevices()
    {
        Devices.Clear();
        foreach (var b in _monitor.Current)
            Devices.Add(new DeviceItemViewModel(b.Name, b.Level, _settings));
        // Keep muted devices listed while offline so they can still be unmuted.
        var online = _monitor.Current.Select(b => b.Name).ToHashSet();
        foreach (var name in _settings.MutedDevices.Where(n => !online.Contains(n)).Order())
            Devices.Add(new DeviceItemViewModel(name, null, _settings));

        OnPropertyChanged(nameof(HasNoDevices));
        LastRefreshText = _monitor.LastRefresh is { } t ? $"上次刷新：{t:HH:mm:ss}" : "尚未刷新";
    }
}
