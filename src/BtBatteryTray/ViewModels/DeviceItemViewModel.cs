using BtBatteryTray.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BtBatteryTray.ViewModels;

public partial class DeviceItemViewModel : ObservableObject
{
    readonly SettingsService _settings;

    public DeviceItemViewModel(string name, int? level, SettingsService settings)
    {
        _settings = settings;
        Name = name;
        LevelText = level is { } l ? $"{l}%" : "离线";
        AlertEnabled = !settings.IsMuted(name);
    }

    public string Name { get; }
    public string LevelText { get; }

    [ObservableProperty] public partial bool AlertEnabled { get; set; }

    partial void OnAlertEnabledChanged(bool value) => _settings.SetMuted(Name, !value);
}
