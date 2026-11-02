using System.Runtime.InteropServices;
using BtBatteryTray.ViewModels;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace BtBatteryTray.Views;

public sealed partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(460 * scale), (int)(620 * scale)));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        Closed += (_, _) => ViewModel.Dispose();
    }

    public SettingsViewModel ViewModel { get; }

    [DllImport("user32.dll")]
    static extern uint GetDpiForWindow(IntPtr hwnd);
}
