using BtBatteryTray.Models;
using Windows.Devices.Enumeration.Pnp;

namespace BtBatteryTray.Services;

public static class BatteryReader
{
    // Undocumented key the Settings app reads; written for HFP headsets and for BLE devices exposing the Battery Service.
    const string BatteryKey = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
    const string IsPresentKey = "{540b947e-8b40-45bc-a8a2-6a0b894cbda2} 5";   // DEVPKEY_Device_IsPresent
    const string FriendlyNameKey = "{a45c254e-df1c-4efd-8020-67d146a850e0} 14"; // DEVPKEY_Device_FriendlyName
    const string CategoryKey = "{78c34fc8-104a-4aca-9ea4-524d52996e57} 90";    // DEVPKEY_DeviceContainer_Category, e.g. "Input.Keyboard"
    const string DisplayNameKey = "System.ItemNameDisplay";

    public static async Task<List<BtBattery>> ReadAllAsync()
    {
        var devices = await PnpObject.FindAllAsync(
            PnpObjectType.Device,
            new[] { BatteryKey, IsPresentKey, DisplayNameKey, FriendlyNameKey, CategoryKey });

        return devices
            .Where(d => d.Properties.TryGetValue(BatteryKey, out var v) && v is byte)
            // Disconnected nodes keep their last reported level, so only present devices count.
            .Where(d => d.Properties.TryGetValue(IsPresentKey, out var p) && p is true)
            .Select(d => new BtBattery(NameOf(d), (byte)d.Properties[BatteryKey], KindOf(d)))
            // One physical device can expose several nodes (e.g. HFP and A2DP on a headset).
            .GroupBy(b => b.Name)
            .Select(g => g.First())
            .OrderBy(b => b.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    static string NameOf(PnpObject d)
    {
        foreach (var key in new[] { DisplayNameKey, FriendlyNameKey })
        {
            if (d.Properties.TryGetValue(key, out var v) && v is string s && !string.IsNullOrWhiteSpace(s))
                return s.Trim();
        }
        return "未知设备";
    }

    static DeviceKind KindOf(PnpObject d)
    {
        var category = d.Properties.TryGetValue(CategoryKey, out var v) switch
        {
            true when v is string[] list => string.Join(";", list),
            true when v is string s => s,
            _ => "",
        };
        return category.ToLowerInvariant() switch
        {
            var c when c.Contains("keyboard") => DeviceKind.Keyboard,
            var c when c.Contains("mouse") => DeviceKind.Mouse,
            var c when c.Contains("head") || c.Contains("earbud") => DeviceKind.Headphones,
            var c when c.Contains("speaker") => DeviceKind.Speaker,
            var c when c.Contains("gam") || c.Contains("controller") => DeviceKind.Gamepad,
            var c when c.Contains("pen") => DeviceKind.Pen,
            _ => DeviceKind.Other,
        };
    }
}
