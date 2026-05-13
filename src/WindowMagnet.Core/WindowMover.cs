using WindowMagnet.Core.Native;

namespace WindowMagnet.Core;

/// <summary>
/// Recall implementation per DESIGN.md §3c. SetWindowPos with SWP_NOACTIVATE
/// is the key — it moves the source window without making it the foreground
/// window, so a fullscreen game on the other monitor keeps focus.
/// </summary>
public static class WindowMover
{
    /// <summary>
    /// Move a window to a target rectangle without stealing focus. Restores
    /// from minimized state first if necessary (also without activation).
    /// </summary>
    /// <returns>True on success; check <see cref="System.Runtime.InteropServices.Marshal.GetLastWin32Error"/> on failure.</returns>
    public static bool Recall(IntPtr hWnd, int x, int y, int width, int height)
    {
        if (hWnd == IntPtr.Zero) return false;

        if (NativeMethods.IsIconic(hWnd))
        {
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOWNOACTIVATE);
        }

        return NativeMethods.SetWindowPos(
            hWnd,
            IntPtr.Zero,
            x, y, width, height,
            NativeMethods.SWP_NOACTIVATE
            | NativeMethods.SWP_NOZORDER
            | NativeMethods.SWP_SHOWWINDOW
            | NativeMethods.SWP_ASYNCWINDOWPOS);
    }

    public static bool Recall(IntPtr hWnd, WindowBounds bounds)
        => Recall(hWnd, bounds.X, bounds.Y, bounds.Width, bounds.Height);

    /// <summary>
    /// Move a window to (x, y) in physical pixels WITHOUT resizing or activating.
    /// Used by the picker to position itself on a configured monitor at launch —
    /// we let WPF determine the size, then nudge the position via Win32 so per-monitor
    /// DPI translation is handled correctly.
    /// </summary>
    public static bool MoveTo(IntPtr hWnd, int x, int y)
    {
        if (hWnd == IntPtr.Zero) return false;
        return NativeMethods.SetWindowPos(
            hWnd, IntPtr.Zero, x, y, 0, 0,
            NativeMethods.SWP_NOACTIVATE
            | NativeMethods.SWP_NOZORDER
            | NativeMethods.SWP_NOSIZE
            | NativeMethods.SWP_SHOWWINDOW);
    }

    /// <summary>Get the current window rect in physical pixels.</summary>
    public static WindowBounds GetBounds(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return default;
        return NativeMethods.GetWindowRect(hWnd, out var r)
            ? new WindowBounds(r.Left, r.Top, r.Width, r.Height)
            : default;
    }
}
