using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BtBatteryTray.Services;

/// <summary>Live settings shared by the tray and the settings window; every change is saved to JSON immediately.</summary>
public partial class SettingsService : ObservableObject
{
    public const int MinThreshold = 5, MaxThreshold = 50;
    public const int MinInterval = 1, MaxInterval = 5;

    static readonly string FilePath = Path.Combine(Log.Directory, "settings.json");
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    readonly HashSet<string> _muted = new();
    // Suppresses saving and registry writes until Load() has applied the stored values.
    bool _loading = true;

    SettingsService()
    {
        Threshold = 20;
        IntervalMinutes = 2;
    }

    [ObservableProperty] public partial int Threshold { get; set; }
    [ObservableProperty] public partial int IntervalMinutes { get; set; }
    [ObservableProperty] public partial bool AutoStart { get; set; }

    public event EventHandler? MutedDevicesChanged;

    public IReadOnlyCollection<string> MutedDevices => _muted;

    public bool IsMuted(string device) => _muted.Contains(device);

    public void SetMuted(string device, bool muted)
    {
        if (muted ? _muted.Add(device) : _muted.Remove(device))
        {
            Save();
            MutedDevicesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    partial void OnThresholdChanged(int value) => Save();
    partial void OnIntervalMinutesChanged(int value) => Save();

    partial void OnAutoStartChanged(bool value)
    {
        if (_loading)
            return;
        try
        {
            AutoStartRegistry.Set(value);
        }
        catch (Exception ex)
        {
            Log.Write($"Failed to update autostart: {ex}");
        }
        Save();
    }

    public static SettingsService Load()
    {
        var service = new SettingsService();
        try
        {
            if (File.Exists(FilePath))
            {
                var data = JsonSerializer.Deserialize<Data>(File.ReadAllText(FilePath)) ?? new Data();
                service.Threshold = Math.Clamp(data.Threshold, MinThreshold, MaxThreshold);
                service.IntervalMinutes = Math.Clamp(data.IntervalMinutes, MinInterval, MaxInterval);
                foreach (var d in data.MutedDevices)
                    service._muted.Add(d);
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Failed to load settings, using defaults: {ex}");
        }

        // The Run key is the source of truth (the user may toggle it in Task Manager); refresh the path if the exe moved.
        service.AutoStart = AutoStartRegistry.IsEnabled();
        if (service.AutoStart)
            AutoStartRegistry.Set(true);

        service._loading = false;
        return service;
    }

    void Save()
    {
        if (_loading)
            return;
        try
        {
            var data = new Data
            {
                Threshold = Threshold,
                IntervalMinutes = IntervalMinutes,
                AutoStart = AutoStart,
                MutedDevices = _muted.Order().ToList(),
            };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(data, JsonOptions));
        }
        catch (Exception ex)
        {
            Log.Write($"Failed to save settings: {ex}");
        }
    }

    sealed class Data
    {
        public int Threshold { get; set; } = 20;
        public int IntervalMinutes { get; set; } = 2;
        // Informational only; the Run key decides on load.
        public bool AutoStart { get; set; }
        public List<string> MutedDevices { get; set; } = [];
    }
}
