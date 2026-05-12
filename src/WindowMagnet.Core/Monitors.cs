using WindowMagnet.Core.Native;

namespace WindowMagnet.Core;

/// <summary>
/// Discovers the physical monitor rectangles via EnumDisplayMonitors. Returns
/// physical-pixel coordinates — WindowMagnet runs as PerMonitorV2 (see DESIGN §6d),
/// so SetWindowPos coordinates are in the same space and no DPI translation is required.
/// </summary>
public static class Monitors
{
    /// <summary>All monitors in arbitrary order (typically primary first, but not guaranteed).</summary>
    public static IReadOnlyList<WindowBounds> All()
    {
        var list = new List<WindowBounds>(2);
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr hdc, ref RECT r, IntPtr p) =>
        {
            list.Add(new WindowBounds(r.Left, r.Top, r.Width, r.Height));
            return true;
        }, IntPtr.Zero);
        return list;
    }

    /// <summary>Returns the work-area (excluding taskbar) for each monitor.</summary>
    public static IReadOnlyList<WindowBounds> WorkAreas()
    {
        var list = new List<WindowBounds>(2);
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr hdc, ref RECT r, IntPtr p) =>
        {
            var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
            if (NativeMethods.GetMonitorInfo(h, ref info))
            {
                var w = info.rcWork;
                list.Add(new WindowBounds(w.Left, w.Top, w.Width, w.Height));
            }
            else
            {
                list.Add(new WindowBounds(r.Left, r.Top, r.Width, r.Height));
            }
            return true;
        }, IntPtr.Zero);
        return list;
    }
}
