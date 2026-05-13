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
    public static IReadOnlyList<WindowBounds> All()
    {
        var raw = new List<(WindowBounds bounds, bool isPrimary)>(2);
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr hdc, ref RECT r, IntPtr p) =>
        {
            var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
            bool isPrimary = false;
            WindowBounds b;
            if (NativeMethods.GetMonitorInfo(h, ref info))
            {
                isPrimary = (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0;
                b = new WindowBounds(info.rcMonitor.Left, info.rcMonitor.Top, info.rcMonitor.Width, info.rcMonitor.Height);
            }
            else
            {
                b = new WindowBounds(r.Left, r.Top, r.Width, r.Height);
            }
            raw.Add((b, isPrimary));
            return true;
        }, IntPtr.Zero);
        return raw.OrderByDescending(x => x.isPrimary).Select(x => x.bounds).ToList();
    }

    /// <summary>Work areas (full monitor bounds minus taskbar), primary first.</summary>
    public static IReadOnlyList<WindowBounds> WorkAreas()
    {
        var raw = new List<(WindowBounds bounds, bool isPrimary)>(2);
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr hdc, ref RECT r, IntPtr p) =>
        {
            var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
            bool isPrimary = false;
            WindowBounds b;
            if (NativeMethods.GetMonitorInfo(h, ref info))
            {
                isPrimary = (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0;
                var w = info.rcWork;
                b = new WindowBounds(w.Left, w.Top, w.Width, w.Height);
            }
            else
            {
                b = new WindowBounds(r.Left, r.Top, r.Width, r.Height);
            }
            raw.Add((b, isPrimary));
            return true;
        }, IntPtr.Zero);
        return raw.OrderByDescending(x => x.isPrimary).Select(x => x.bounds).ToList();
    }
}
