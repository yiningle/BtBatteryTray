using System.Runtime.InteropServices;
using BtBatteryTray.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace BtBatteryTray.Views;

/// <summary>
/// Borderless, non-activating card shown while the pointer rests on the tray icon, anchored to the icon's rect.
/// The window is created once and only shown/hidden afterwards.
/// </summary>
public sealed partial class TrayHoverCard : Window
{
    const int Gap = 8;
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2;

    readonly IntPtr _hwnd;
    RectInt32 _anchor;
    bool _bound;

    public TrayHoverCard(TrayViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(true, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;
        var round = DWMWCP_ROUND;
        DwmSetWindowAttribute(_hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));

        // The device list changes size while the card is open (e.g. a poll lands); keep the window fitted.
        Card.SizeChanged += (_, _) => { if (IsShown) Place(); };
    }

    public TrayViewModel ViewModel { get; }

    public double CardWidth => 300;

    public bool IsShown => AppWindow.IsVisible;

    /// <summary>Screen rect of the card in physical pixels, for hit-testing the pointer.</summary>
    public RectInt32 ScreenBounds => new(AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height);

    public static Brush LevelBrush(bool isLow) =>
        (Brush)Application.Current.Resources[isLow ? "SystemFillColorCriticalBrush" : "TextFillColorPrimaryBrush"];

    public void ShowAt(RectInt32 anchor)
    {
        // x:Bind normally initializes on Activate(), which this window never gets.
        if (!_bound)
        {
            Bindings.Update();
            Card.UpdateLayout();
            _bound = true;
        }
        _anchor = anchor;
        Place();
        // Never take focus from whatever the user is working in.
        AppWindow.Show(activateWindow: false);
    }

    public void Hide() => AppWindow.Hide();

    void Place()
    {
        var scale = GetDpiForWindow(_hwnd) / 96.0;
        var height = Card.ActualHeight > 0 ? Card.ActualHeight : EstimateHeight();
        var w = (int)Math.Ceiling(CardWidth * scale);
        var h = (int)Math.Ceiling(height * scale);
        var work = DisplayArea.GetFromRect(_anchor, DisplayAreaFallback.Nearest).WorkArea;
        var gap = (int)(Gap * scale);

        var x = Math.Clamp(_anchor.X + _anchor.Width / 2 - w / 2, work.X + gap, work.X + work.Width - w - gap);
        // Prefer above the icon (bottom taskbar, or the overflow panel); fall back to below it for a top taskbar.
        var y = _anchor.Y - h - gap;
        if (y < work.Y)
            y = _anchor.Y + _anchor.Height + gap;
        AppWindow.MoveAndResize(new RectInt32(x, y, w, h));
    }

    // Used only before the first layout pass, when ActualHeight is still 0.
    double EstimateHeight() => 60 + Math.Max(1, ViewModel.Devices.Count) * 56;

    [DllImport("user32.dll")]
    static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
