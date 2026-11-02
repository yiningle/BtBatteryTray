using BtBatteryTray.Services;
using BtBatteryTray.Tray;
using BtBatteryTray.ViewModels;
using BtBatteryTray.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;

namespace BtBatteryTray;

public partial class App : Application
{
    DispatcherQueue _dispatcher = null!;
    SettingsService _settings = null!;
    BatteryMonitor _monitor = null!;
    LowBatteryAlerter _alerter = null!;
    TrayIconController? _tray;
    SettingsWindow? _settingsWindow;

    public App()
    {
        InitializeComponent();
        // The app lives in the tray; closing the settings window must not end the process.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
        UnhandledException += (_, e) => Log.Write($"Unhandled: {e.Exception}");
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        // Unpackaged apps must subscribe before Register().
        AppNotificationManager.Default.NotificationInvoked += (_, _) => _dispatcher.TryEnqueue(ShowSettings);
        AppNotificationManager.Default.Register();
        AppInstance.GetCurrent().Activated += (_, e) =>
            _dispatcher.TryEnqueue(IsUninstallRequest(e) ? ExitApp : ShowSettings);

        _settings = SettingsService.Load();
        _alerter = new LowBatteryAlerter(_settings);
        _monitor = new BatteryMonitor(_dispatcher, TimeSpan.FromMinutes(_settings.IntervalMinutes));
        _monitor.Updated += (_, _) => _alerter.Check(_monitor.Current);
        _settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsService.IntervalMinutes))
                _monitor.Interval = TimeSpan.FromMinutes(_settings.IntervalMinutes);
        };

        _tray = new TrayIconController(new TrayViewModel(_settings, _monitor, ShowSettings, ExitApp));
        _monitor.Start();

        if (AppInstance.GetCurrent().GetActivatedEventArgs().Kind == ExtendedActivationKind.AppNotification)
            ShowSettings();
    }

    static bool IsUninstallRequest(AppActivationArguments e) =>
        e.Data is Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launch
        && launch.Arguments.Contains(Program.UninstallHookArgument, StringComparison.Ordinal);

    void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(new SettingsViewModel(_settings, _monitor));
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }
        _settingsWindow.Activate();
    }

    void ExitApp()
    {
        _settingsWindow?.Close();
        _tray?.Dispose();
        AppNotificationManager.Default.Unregister();
        Exit();
    }
}
