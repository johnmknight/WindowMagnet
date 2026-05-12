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
}
