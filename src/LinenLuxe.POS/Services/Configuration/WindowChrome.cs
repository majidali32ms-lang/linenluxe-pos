using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace LinenLuxe.POS.Services.Configuration;

/// <summary>
/// Paints the window's title bar in the brand navy.
///
/// The strip carrying the minimise / maximise / close buttons is drawn by Windows,
/// not by WPF, so it can't be styled in XAML. Windows 11 build 22000 and later let
/// an app colour it through DwmSetWindowAttribute, which keeps the real system
/// buttons and their behaviour — the alternative, a custom-drawn title bar, means
/// reimplementing snapping, double-click-to-maximise and the system menu, and
/// getting one of them subtly wrong.
///
/// On anything older the call simply fails and the default title bar stays. That
/// is the right outcome: a plain title bar is not a fault.
/// </summary>
public static class WindowChrome
{
    private const int CaptionColour = 35;   // DWMWA_CAPTION_COLOR
    private const int TextColour = 36;      // DWMWA_TEXT_COLOR

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Apply the brand colours once the window has a handle.</summary>
    public static void ApplyBrand(Window window)
    {
        void Apply()
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;

            // Navy #0A1A3A caption with off-white text on it.
            Set(handle, CaptionColour, Color.FromRgb(0x0A, 0x1A, 0x3A));
            Set(handle, TextColour, Color.FromRgb(0xFA, 0xF7, 0xF0));
        }

        if (window.IsLoaded) Apply();
        else window.SourceInitialized += (_, _) => Apply();
    }

    private static void Set(IntPtr handle, int attribute, Color colour)
    {
        // DWM wants 0x00BBGGRR — the reverse of the usual RGB order.
        var value = colour.R | (colour.G << 8) | (colour.B << 16);
        try
        {
            DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));
        }
        catch (Exception)
        {
            // Older Windows doesn't know these attributes; the default bar stays.
        }
    }
}
