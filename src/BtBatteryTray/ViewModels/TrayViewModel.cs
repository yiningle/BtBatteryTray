using System.Collections.ObjectModel;
using BtBatteryTray.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BtBatteryTray.ViewModels;

/// <summary>State behind the tray entry: whether to show the low-battery mark, and the device list for the hover card.</summary>
public partial class TrayViewModel : ObservableObject
{
    readonly SettingsService _settings;
    readonly BatteryMonitor _monitor;
    readonly Action _openSettings;
    readonly Action _exit;

    public TrayViewModel(SettingsService settings, BatteryMonitor monitor, Action openSettings, Action exit)
    {
        _settings = settings;
        _monitor = monitor;
        _openSettings = openSettings;
        _exit = exit;

        _monitor.Updated += (_, _) => Recompute();
        _settings.MutedDevicesChanged += (_, _) => Recompute();
        _settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsService.Threshold))
                Recompute();
            else if (e.PropertyName == nameof(SettingsService.AutoStart))
                OnPropertyChanged(nameof(IsAutoStart));
        };
    }

    public ObservableCollection<TrayDeviceViewModel> Devices { get; } = new();

    /// <summary>True when any device that still has alerts enabled is at or below the threshold.</summary>
    [ObservableProperty] public partial bool HasLowDevice { get; set; }
    [ObservableProperty] public partial bool HasNoDevices { get; set; } = true;
    [ObservableProperty] public partial string StatusText { get; set; } = "读取中…";

    public bool IsAutoStart => _settings.AutoStart;

    [RelayCommand]
    Task Refresh() => _monitor.RefreshAsync();

    [RelayCommand]
    void OpenSettings() => _openSettings();

    [RelayCommand]
    void ToggleAutoStart() => _settings.AutoStart = !_settings.AutoStart;

    [RelayCommand]
    void Exit() => _exit();

    void Recompute()
    {
        Devices.Clear();
        foreach (var b in _monitor.Current)
            Devices.Add(new TrayDeviceViewModel(b, b.Level <= _settings.Threshold));

        HasNoDevices = Devices.Count == 0;
        HasLowDevice = Devices.Any(d => d.IsLow && !_settings.IsMuted(d.Name));
        StatusText = _monitor.LastRefresh is { } t ? $"{t:HH:mm} 更新" : "读取中…";
    }
}
