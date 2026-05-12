using System.Diagnostics;
using System.Text;
using WindowMagnet.Core.Native;

namespace WindowMagnet.Core;

/// <summary>
/// Walks every top-level window via Win32 EnumWindows and applies the filters
/// described in DESIGN.md §3a. Threadsafe in the sense that it's stateless —
/// call <see cref="Enumerate"/> as often as you like, e.g. on a 500–1000ms timer.
/// </summary>
public sealed class WindowEnumerator
{
    private readonly HashSet<IntPtr> _excludeHandles = new();
    private readonly HashSet<string> _excludeProcessNames = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Minimum width to consider — filters out tooltips and floating toolbars.</summary>
    public int MinWidth { get; set; } = 100;

    /// <summary>Minimum height to consider.</summary>
    public int MinHeight { get; set; } = 50;

    /// <summary>Exclude a specific window handle (e.g. WindowMagnet's own window).</summary>
    public void Exclude(IntPtr hWnd) => _excludeHandles.Add(hWnd);

    /// <summary>Exclude all windows belonging to a process (e.g. "Cyberpunk2077.exe").</summary>
    public void ExcludeProcess(string name) => _excludeProcessNames.Add(name);

    public IReadOnlyList<WindowInfo> Enumerate()
    {
        var list = new List<WindowInfo>(32);

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            try
            {
                if (_excludeHandles.Contains(hWnd)) return true;
                if (!NativeMethods.IsWindowVisible(hWnd)) return true;

                int len = NativeMethods.GetWindowTextLength(hWnd);
                if (len <= 0) return true;

                // Skip cloaked windows — e.g. virtual-desktop windows that aren't on the active desktop.
                if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0
                    && cloaked != 0)
                {
                    return true;
                }

                if (!NativeMethods.GetWindowRect(hWnd, out var rect)) return true;
                if (rect.Width < MinWidth || rect.Height < MinHeight) return true;

                var sb = new StringBuilder(len + 1);
                NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
                var title = sb.ToString();

                NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
                string proc;
                try
                {
                    using var p = Process.GetProcessById((int)pid);
                    proc = p.ProcessName + ".exe";
                }
                catch
                {
                    proc = string.Empty;
                }

                if (proc.Length > 0 && _excludeProcessNames.Contains(proc)) return true;

                list.Add(new WindowInfo(
                    hWnd,
                    title,
                    proc,
                    pid,
                    new WindowBounds(rect.Left, rect.Top, rect.Width, rect.Height),
                    NativeMethods.IsIconic(hWnd)));
            }
            catch
            {
                // Per-window errors shouldn't kill the scan.
            }
            return true;
        }, IntPtr.Zero);

        return list;
    }
}
