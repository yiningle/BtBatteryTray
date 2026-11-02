using BtBatteryTray.Models;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace BtBatteryTray.Services;

public class LowBatteryAlerter(SettingsService settings)
{
    // Re-arm only after the level climbs this far above the threshold, so a level hovering around it alerts once.
    public const int Hysteresis = 5;

    readonly HashSet<string> _alerted = new();

    public void Check(IEnumerable<BtBattery> list)
    {
        foreach (var b in list)
        {
            if (b.Level <= settings.Threshold)
            {
                if (!settings.IsMuted(b.Name) && _alerted.Add(b.Name))
                    Show(b);
            }
            else if (b.Level > settings.Threshold + Hysteresis)
            {
                _alerted.Remove(b.Name);
            }
        }
    }

    static void Show(BtBattery b)
    {
        Log.Write($"Low battery alert: {b.Name} {b.Level}%");
        try
        {
            var toast = new AppNotificationBuilder()
                .AddText($"{b.Name} 电量低")
                .AddText($"剩余 {b.Level}%，请及时充电")
                .BuildNotification();
            AppNotificationManager.Default.Show(toast);
        }
        catch (Exception ex)
        {
            Log.Write($"Failed to show notification: {ex}");
        }
    }
}
