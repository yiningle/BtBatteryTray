using Microsoft.Win32;

namespace BtBatteryTray.Services;

/// <summary>Start-with-Windows for the unpackaged build via HKCU\...\Run.</summary>
public static class AutoStartRegistry
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "BtBatteryTray";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string s && !string.IsNullOrWhiteSpace(s);
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --autostart");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
