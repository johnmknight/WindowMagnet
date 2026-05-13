using System.Runtime.InteropServices;
using WindowMagnet.Core.Native;

namespace WindowMagnet.Core;

/// <summary>
/// Discovers physical monitor rectangles via EnumDisplayMonitors. Returns
/// physical-pixel coordinates — WindowMagnet runs as PerMonitorV2 (see DESIGN §6d),
/// so SetWindowPos coordinates are in the same space and no DPI translation is required.
/// Monitors are returned with the primary first; secondary monitors follow in
/// EnumDisplayMonitors order. This gives stable 1-based indexing for the
/// <c>PickerWindow.Monitor</c> and <c>Slot.Monitor</c> config fields.
/// </summary>
public static class Monitors
{
    /// <summary>All monitor rectangles (full bounds, primary first).</summary>
    public static IReadOnlyList<MonitorInfo> All() => Enumerate(workArea: false);

    /// <summary>Work areas (full monitor bounds minus taskbar), primary first.</summary>
    public static IReadOnlyList<MonitorInfo> WorkAreas() => Enumerate(workArea: true);

    private static IReadOnlyList<MonitorInfo> Enumerate(bool workArea)
    {
        var raw = new List<(MonitorInfo info, bool isPrimary)>(2);
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr hdc, ref RECT r, IntPtr p) =>
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            bool isPrimary = false;
            WindowBounds b;
            if (NativeMethods.GetMonitorInfo(h, ref info))
            {
                isPrimary = (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0;
                var src = workArea ? info.rcWork : info.rcMonitor;
                b = new WindowBounds(src.Left, src.Top, src.Width, src.Height);
            }
            else
            {
                b = new WindowBounds(r.Left, r.Top, r.Width, r.Height);
            }
            // 96 = 100%. Anything else means per-monitor scaling is in effect on this display.
            double dpi = 1.0;
            if (NativeMethods.GetDpiForMonitor(h, NativeMethods.MDT_EFFECTIVE_DPI, out uint dx, out _) == 0
                && dx > 0)
            {
                dpi = dx / 96.0;
            }
            raw.Add((new MonitorInfo(b, dpi), isPrimary));
            return true;
        }, IntPtr.Zero);
        return raw.OrderByDescending(x => x.isPrimary).Select(x => x.info).ToList();
    }
}

/// <summary>
/// Bounds + per-monitor DPI scale (1.0 = 100%, 1.5 = 150%, etc.). Exposes the
/// bounds members directly so existing call sites that read <c>X/Y/Width/Height</c>
/// keep working unchanged.
/// </summary>
public sealed record MonitorInfo(WindowBounds Bounds, double DpiScale)
{
    public int X      => Bounds.X;
    public int Y      => Bounds.Y;
    public int Width  => Bounds.Width;
    public int Height => Bounds.Height;
    public int Right  => Bounds.Right;
    public int Bottom => Bounds.Bottom;
}
