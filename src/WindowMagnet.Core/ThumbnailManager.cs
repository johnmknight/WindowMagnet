using System.ComponentModel;
using WindowMagnet.Core.Native;

namespace WindowMagnet.Core;

/// <summary>
/// Wraps the DWM Thumbnail API (DESIGN.md §3b). One manager per destination window.
/// Re-registering an already-registered source is a no-op and returns the existing handle —
/// this matters because EnumWindows on a refresh tick can hand back the same HWND repeatedly.
/// </summary>
public sealed class ThumbnailManager : IDisposable
{
    private readonly IntPtr _destHwnd;
    private readonly Dictionary<IntPtr, IntPtr> _thumbs = new();
    private bool _disposed;

    public ThumbnailManager(IntPtr destinationHwnd)
    {
        if (destinationHwnd == IntPtr.Zero)
            throw new ArgumentException("Destination HWND must not be null.", nameof(destinationHwnd));
        _destHwnd = destinationHwnd;
    }

    /// <summary>Bind a source window's contents to render inside this destination.</summary>
    public IntPtr Register(IntPtr source)
    {
        EnsureNotDisposed();
        if (_thumbs.TryGetValue(source, out var existing)) return existing;

        int hr = NativeMethods.DwmRegisterThumbnail(_destHwnd, source, out var thumb);
        if (hr != 0) throw new Win32Exception(hr);

        _thumbs[source] = thumb;
        return thumb;
    }

    /// <summary>Position the thumbnail inside the destination window's client area.</summary>
    public void UpdateRect(IntPtr thumb, int x, int y, int width, int height,
                           byte opacity = 255, bool sourceClientAreaOnly = false)
    {
        if (thumb == IntPtr.Zero) return;
        if (width <= 0 || height <= 0) return;

        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = NativeMethods.DWM_TNP_VISIBLE
                    | NativeMethods.DWM_TNP_RECTDESTINATION
                    | NativeMethods.DWM_TNP_OPACITY
                    | NativeMethods.DWM_TNP_SOURCECLIENTAREAONLY,
            fVisible = true,
            opacity = opacity,
            fSourceClientAreaOnly = sourceClientAreaOnly,
            rcDestination = new RECT { Left = x, Top = y, Right = x + width, Bottom = y + height },
        };
        NativeMethods.DwmUpdateThumbnailProperties(thumb, ref props);
    }

    /// <summary>The source window's natural pixel size — useful for aspect-correct layouts.</summary>
    public bool TryGetSourceSize(IntPtr thumb, out int width, out int height)
    {
        if (NativeMethods.DwmQueryThumbnailSourceSize(thumb, out var sz) == 0)
        {
            width = sz.cx;
            height = sz.cy;
            return true;
        }
        width = height = 0;
        return false;
    }

    public void Unregister(IntPtr source)
    {
        if (_thumbs.TryGetValue(source, out var thumb))
        {
            NativeMethods.DwmUnregisterThumbnail(thumb);
            _thumbs.Remove(source);
        }
    }

    public void UnregisterAll()
    {
        foreach (var thumb in _thumbs.Values)
            NativeMethods.DwmUnregisterThumbnail(thumb);
        _thumbs.Clear();
    }

    /// <summary>Drop thumbnails for sources no longer in <paramref name="liveSources"/>.</summary>
    public void Reconcile(IEnumerable<IntPtr> liveSources)
    {
        var live = new HashSet<IntPtr>(liveSources);
        foreach (var stale in _thumbs.Keys.Where(s => !live.Contains(s)).ToArray())
            Unregister(stale);
    }

    private void EnsureNotDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(ThumbnailManager));
    }

    public void Dispose()
    {
        if (_disposed) return;
        UnregisterAll();
        _disposed = true;
    }
}
