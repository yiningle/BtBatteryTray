using BtBatteryTray.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using Velopack;

namespace BtBatteryTray;

public static class Program
{
    const string MainInstanceKey = "BtBatteryTray.Main";

    /// <summary>Velopack's uninstall hook argument; the running instance treats an activation carrying it as "exit now".</summary>
    public const string UninstallHookArgument = "--veloapp-uninstall";

    [STAThread]
    static int Main(string[] args)
    {
        // Must run first: the installer launches the exe with hook arguments, which Run() handles and then exits.
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => CleanUpBeforeUninstall())
            .Run();

        WinRT.ComWrappersSupport.InitializeComWrappers();
        if (RedirectToExistingInstance())
            return 0;

        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
        return 0;
    }

    // Remove what the app registered outside its install folder, so uninstalling leaves nothing behind.
    static void CleanUpBeforeUninstall()
    {
        ShutDownRunningInstance();
        try
        {
            AutoStartRegistry.Set(false);
        }
        catch (Exception ex)
        {
            Log.Write($"Uninstall: failed to remove autostart: {ex}");
        }
        try
        {
            AppNotificationManager.Default.UnregisterAll();
        }
        catch (Exception ex)
        {
            Log.Write($"Uninstall: failed to unregister notifications: {ex}");
        }
    }

    // Velopack's own running-process check misses the tray instance, and its files can't be deleted while it runs.
    // Forward this hook activation to it (App exits on seeing the uninstall argument); kill it if it doesn't go.
    static void ShutDownRunningInstance()
    {
        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            var main = AppInstance.GetInstances().FirstOrDefault(i => i.Key == MainInstanceKey);
            if (main is null)
                return;

            using var process = System.Diagnostics.Process.GetProcessById((int)main.ProcessId);
            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            Task.Run(() => main.RedirectActivationToAsync(activation).AsTask()).Wait(TimeSpan.FromSeconds(5));
            if (!process.WaitForExit(TimeSpan.FromSeconds(5)))
            {
                Log.Write("Uninstall: running instance did not exit, killing it");
                process.Kill();
                process.WaitForExit(TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Uninstall: failed to stop running instance: {ex}");
        }
    }

    // A second launch hands its activation to the running instance (which opens the settings window) and exits.
    static bool RedirectToExistingInstance()
    {
        var main = AppInstance.FindOrRegisterForKey(MainInstanceKey);
        if (main.IsCurrent)
            return false;

        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        // Redirect on a worker thread: blocking the STA thread on this call can deadlock.
        Task.Run(() => main.RedirectActivationToAsync(activation).AsTask()).Wait();
        return true;
    }
}
