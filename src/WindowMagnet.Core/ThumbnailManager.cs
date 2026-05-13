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

    /// <summary>
    /// Position the thumbnail inside the destination window's client area.
    /// <para>
    /// When <paramref name="cropSource"/> is non-null, only that rect of the source
    /// window is rendered (DWM stretches it to fill the destination). Use this to
    /// eliminate letterbox bars when the destination aspect doesn't match the
    /// source — see <see cref="ComputeCenteredCrop"/>.
    /// </para>
    /// </summary>
    public void UpdateRect(IntPtr thumb, int x, int y, int width, int height,
                           byte opacity = 255, bool sourceClientAreaOnly = false,
                           (int X, int Y, int W, int H)? cropSource = null)
    {
        if (thumb == IntPtr.Zero) return;
        if (width <= 0 || height <= 0) return;

        uint flags = NativeMethods.DWM_TNP_VISIBLE
                   | NativeMethods.DWM_TNP_RECTDESTINATION
                   | NativeMethods.DWM_TNP_OPACITY
                   | NativeMethods.DWM_TNP_SOURCECLIENTAREAONLY;
        var rcSource = default(RECT);
        if (cropSource is { } c && c.W > 0 && c.H > 0)
        {
            flags |= NativeMethods.DWM_TNP_RECTSOURCE;
            rcSource = new RECT { Left = c.X, Top = c.Y, Right = c.X + c.W, Bottom = c.Y + c.H };
        }

        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = flags,
            fVisible = true,
            opacity = opacity,
            fSourceClientAreaOnly = sourceClientAreaOnly,
            rcDestination = new RECT { Left = x, Top = y, Right = x + width, Bottom = y + height },
            rcSource = rcSource,
        };
        NativeMethods.DwmUpdateThumbnailProperties(thumb, ref props);
    }

    /// <summary>
    /// Compute a centered crop rect inside a source of <paramref name="srcW"/> x
    /// <paramref name="srcH"/> that matches the aspect ratio of <paramref name="dstW"/> x
    /// <paramref name="dstH"/>. Returns null when aspects already match (no crop needed)
    /// or when either dimension is non-positive.
    /// </summary>
    public static (int X, int Y, int W, int H)? ComputeCenteredCrop(
        int srcW, int srcH, int dstW, int dstH)
    {
        if (srcW <= 0 || srcH <= 0 || dstW <= 0 || dstH <= 0) return null;
        double srcAspect = (double)srcW / srcH;
        double dstAspect = (double)dstW / dstH;
        // 1% tolerance — no point cropping for sub-pixel mismatches.
        if (Math.Abs(srcAspect - dstAspect) / dstAspect < 0.01) return null;

        if (srcAspect > dstAspect)
        {
            // Source wider than dest — crop horizontally.
            int cropW = (int)Math.Round(srcH * dstAspect);
            int cropX = (srcW - cropW) / 2;
            return (cropX, 0, cropW, srcH);
        }
        else
        {
            // Source taller than dest — crop vertically.
            int cropH = (int)Math.Round(srcW / dstAspect);
            int cropY = (srcH - cropH) / 2;
            return (0, cropY, srcW, cropH);
        }
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
