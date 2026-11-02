using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BtBatteryTray.Tray;

/// <summary>
/// Draws the tray entry as a ring. Normally it is a plain ring in the taskbar's foreground color, giving no
/// detail away (that lives in the hover card); when a device is low it turns into a solid red ring.
/// </summary>
public static class RingIconRenderer
{
    const int SM_CXSMICON = 49;

    public static Icon Render(bool low, bool lightTaskbar)
    {
        // Render at the exact small-icon size for the system DPI so the shell doesn't resample it.
        var size = GetSystemMetricsForDpi(SM_CXSMICON, GetDpiForSystem());
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var thickness = size * 0.17f;
            var inset = thickness / 2 + size * 0.06f;
            var rect = new RectangleF(inset, inset, size - 2 * inset, size - 2 * inset);

            // Same foreground the shell uses for its own glyphs, so the idle entry blends in with them.
            var color = low
                ? (lightTaskbar ? Color.FromArgb(0xD1, 0x34, 0x38) : Color.FromArgb(0xFF, 0x6B, 0x6B))
                : (lightTaskbar ? Color.FromArgb(0x1A, 0x1A, 0x1A) : Color.White);
            using var pen = new Pen(color, thickness);
            g.DrawEllipse(pen, rect);
        }

        var handle = bitmap.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    /// <summary>The taskbar follows the "Windows mode" setting, which can differ from the app mode.</summary>
    public static bool IsTaskbarLight()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("SystemUsesLightTheme") is int v && v != 0;
    }

    [DllImport("user32.dll")]
    static extern uint GetDpiForSystem();

    [DllImport("user32.dll")]
    static extern int GetSystemMetricsForDpi(int nIndex, uint dpi);

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr hIcon);
}
