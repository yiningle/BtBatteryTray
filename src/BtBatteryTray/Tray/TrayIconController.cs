using System.Drawing;
using System.Runtime.InteropServices;
using BtBatteryTray.ViewModels;
using BtBatteryTray.Views;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using Windows.UI.ViewManagement;

namespace BtBatteryTray.Tray;

/// <summary>
/// Owns the tray entry. The icon itself only signals "something is low"; details appear in
/// <see cref="TrayHoverCard"/> while the pointer rests on the icon.
/// </summary>
public sealed class TrayIconController : IDisposable
{
    static readonly TimeSpan ShowDelay = TimeSpan.FromMilliseconds(300);
    static readonly TimeSpan HideDelay = TimeSpan.FromMilliseconds(300);

    readonly TrayViewModel _vm;
    readonly TaskbarIcon _icon;
    readonly ToggleMenuFlyoutItem _autoStartItem;
    readonly DispatcherQueue _dispatcher;
    // Must stay referenced, or ColorValuesChanged stops firing once it is collected.
    readonly UISettings _uiSettings = new();
    // Runs only between the first mouse-move over the icon and the card closing.
    readonly DispatcherQueueTimer _hoverTimer;
    Icon? _current;
    TrayHoverCard? _card;
    DateTime _hoverStart, _lastInside;
    POINT _hoverPoint;
    // Set by a click so the card doesn't pop up over the menu or settings window; cleared once the pointer leaves.
    bool _suppressed;

    public TrayIconController(TrayViewModel vm)
    {
        _vm = vm;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _autoStartItem = new ToggleMenuFlyoutItem { Text = "开机自启", Command = vm.ToggleAutoStartCommand, IsChecked = vm.IsAutoStart };

        var menu = new MenuFlyout();
        menu.Items.Add(new MenuFlyoutItem { Text = "立即刷新", Command = vm.RefreshCommand });
        menu.Items.Add(new MenuFlyoutItem { Text = "设置…", Command = vm.OpenSettingsCommand });
        menu.Items.Add(_autoStartItem);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(new MenuFlyoutItem { Text = "退出", Command = vm.ExitCommand });

        // No ToolTipText: the shell's own tooltip would cover the hover card.
        _icon = new TaskbarIcon
        {
            ContextMenuMode = ContextMenuMode.PopupMenu,
            ContextFlyout = menu,
            LeftClickCommand = vm.OpenSettingsCommand,
            NoLeftClickDelay = true,
        };
        UpdateIcon();
        _icon.ForceCreate(false);
        _icon.TrayIcon.MessageWindow.MouseEventReceived += OnMouseEvent;

        _hoverTimer = _dispatcher.CreateTimer();
        _hoverTimer.Interval = TimeSpan.FromMilliseconds(100);
        _hoverTimer.Tick += (_, _) => OnHoverTick();

        _uiSettings.ColorValuesChanged += OnColorValuesChanged;

        _vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(TrayViewModel.HasLowDevice):
                    UpdateIcon();
                    break;
                case nameof(TrayViewModel.IsAutoStart):
                    _autoStartItem.IsChecked = _vm.IsAutoStart;
                    break;
            }
        };
    }

    public void Dispose()
    {
        _hoverTimer.Stop();
        _uiSettings.ColorValuesChanged -= OnColorValuesChanged;
        _card?.Close();
        _icon.Dispose();
        _current?.Dispose();
    }

    // Raised on a background thread when the user switches light/dark mode.
    void OnColorValuesChanged(UISettings sender, object args) => _dispatcher.TryEnqueue(UpdateIcon);

    void UpdateIcon()
    {
        var previous = _current;
        _current = RingIconRenderer.Render(_vm.HasLowDevice, RingIconRenderer.IsTaskbarLight());
        _icon.Icon = _current;
        // The shell keeps its own copy of the icon, so the old one can go once the new one is set.
        previous?.Dispose();
    }

    void OnMouseEvent(object? sender, MessageWindow.MouseEventReceivedEventArgs e) => _dispatcher.TryEnqueue(() =>
    {
        if (e.MouseEvent != MouseEvent.MouseMove)
        {
            _suppressed = true;
            _card?.Hide();
            return;
        }
        if (!_hoverTimer.IsRunning)
        {
            GetCursorPos(out _hoverPoint);
            _hoverStart = _lastInside = DateTime.Now;
            _hoverTimer.Start();
        }
    });

    // The shell reports hover-enter (mouse-move) but never hover-leave, so leaving is detected by polling the cursor.
    void OnHoverTick()
    {
        GetCursorPos(out var pt);
        var iconRect = GetIconRect();
        var cardShown = _card?.IsShown == true;
        var inside = Contains(iconRect, pt, 0) || (cardShown && Contains(_card!.ScreenBounds, pt, 4));
        var now = DateTime.Now;
        if (inside)
            _lastInside = now;

        if (cardShown)
        {
            if (now - _lastInside >= HideDelay)
            {
                _card!.Hide();
                _hoverTimer.Stop();
            }
            return;
        }

        if (!inside)
        {
            _suppressed = false;
            _hoverTimer.Stop();
        }
        else if (!_suppressed && now - _hoverStart >= ShowDelay)
        {
            _card ??= new TrayHoverCard(_vm);
            _card.ShowAt(iconRect);
        }
    }

    RectInt32 GetIconRect()
    {
        var id = new NOTIFYICONIDENTIFIER
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONIDENTIFIER>(),
            guidItem = _icon.TrayIcon.Id,
        };
        if (Shell_NotifyIconGetRect(ref id, out var r) == 0)
            return new RectInt32(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);

        // Not expected, but keep hover usable: treat a small box around where the hover started as the icon.
        const int half = 12;
        return new RectInt32(_hoverPoint.X - half, _hoverPoint.Y - half, half * 2, half * 2);
    }

    static bool Contains(RectInt32 r, POINT p, int slack) =>
        p.X >= r.X - slack && p.X < r.X + r.Width + slack && p.Y >= r.Y - slack && p.Y < r.Y + r.Height + slack;

    [StructLayout(LayoutKind.Sequential)]
    struct NOTIFYICONIDENTIFIER
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public Guid guidItem;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int X, Y; }

    [DllImport("shell32.dll")]
    static extern int Shell_NotifyIconGetRect(ref NOTIFYICONIDENTIFIER identifier, out RECT iconLocation);

    [DllImport("user32.dll")]
    static extern bool GetCursorPos(out POINT point);
}
