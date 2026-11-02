using BtBatteryTray.Models;

namespace BtBatteryTray.ViewModels;

/// <summary>One row of the tray hover card.</summary>
public sealed class TrayDeviceViewModel(BtBattery battery, bool isLow)
{
    public string Name => battery.Name;
    public int Level => battery.Level;
    public string LevelText => $"{battery.Level}%";
    public bool IsLow => isLow;

    // Segoe Fluent Icons code points.
    public string Glyph => battery.Kind switch
    {
        DeviceKind.Keyboard => "",
        DeviceKind.Mouse => "",
        DeviceKind.Headphones => "",
        DeviceKind.Speaker => "",
        DeviceKind.Gamepad => "",
        DeviceKind.Pen => "",
        _ => "", // Bluetooth
    };
}
