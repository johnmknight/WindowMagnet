using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using WindowMagnet.Core;

namespace WindowMagnet.App.Controls;

/// <summary>
/// A WPF FrameworkElement that reserves a rectangle inside its hosting Window
/// where a DWM thumbnail is rendered. The thumbnail itself is drawn by the
/// Desktop Window Manager, on top of WPF content within the reserved rect.
///
/// One instance per source window. When the bound <see cref="SourceHandle"/>
/// changes, the previous thumbnail is unregistered and a new one is registered.
///
/// IMPORTANT: DWM composites the thumbnail ABOVE WPF's render — so WPF Opacity
/// on a parent Button does NOT dim the thumbnail. To visually grey a "locked"
/// tile we have to pass a low opacity straight into DwmUpdateThumbnailProperties.
/// Bind <see cref="IsLocked"/> in XAML to achieve that.
/// </summary>
public sealed class ThumbnailHost : FrameworkElement, IDisposable
{
    public static readonly DependencyProperty SourceHandleProperty =
        DependencyProperty.Register(
            nameof(SourceHandle),
            typeof(IntPtr),
            typeof(ThumbnailHost),
            new PropertyMetadata(IntPtr.Zero, OnSourceHandleChanged));

    public static readonly DependencyProperty IsLockedProperty =
        DependencyProperty.Register(
            nameof(IsLocked),
            typeof(bool),
            typeof(ThumbnailHost),
            new PropertyMetadata(false, OnIsLockedChanged));

    public IntPtr SourceHandle
    {
        get => (IntPtr)GetValue(SourceHandleProperty);
        set => SetValue(SourceHandleProperty, value);
    }

    /// <summary>
    /// When true the DWM thumbnail renders at ~30% opacity. Bind this to the
    /// inverse of <c>CanMove</c> for the "you can't move this" treatment.
    /// </summary>
    public bool IsLocked
    {
        get => (bool)GetValue(IsLockedProperty);
        set => SetValue(IsLockedProperty, value);
    }

    private ThumbnailManager? _manager;
    private IntPtr _thumb;
    private IntPtr _registeredSource;
    private Window? _hostWindow;
    private IntPtr _destHwnd;

    public ThumbnailHost()
    {
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
        LayoutUpdated += (_, _) => UpdateRect();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Honour explicit Width/Height; otherwise stretch to whatever the parent
        // gives us (e.g. a Grid star row). Fall back to 16:9 only when the parent
        // passes infinity in a dimension (Auto layout, top-level StackPanel, etc.).
        double w = double.IsFinite(Width)
            ? Width
            : (double.IsFinite(availableSize.Width) && availableSize.Width > 0 ? availableSize.Width : 100);
        double h = double.IsFinite(Height)
            ? Height
            : (double.IsFinite(availableSize.Height) && availableSize.Height > 0 ? availableSize.Height : w * 9.0 / 16.0);
        return new Size(w, h);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _hostWindow = Window.GetWindow(this);
        if (_hostWindow is null) return;
        _destHwnd = new WindowInteropHelper(_hostWindow).Handle;
        Bind();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e) => Dispose();

    private static void OnSourceHandleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ThumbnailHost host) host.Bind();
    }

    private static void OnIsLockedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ThumbnailHost host) host.UpdateRect();
    }

    private void Bind()
    {
        if (_destHwnd == IntPtr.Zero) return;
        _manager ??= new ThumbnailManager(_destHwnd);

        // If we were already showing a thumbnail for a different source, drop it.
        if (_registeredSource != IntPtr.Zero && _registeredSource != SourceHandle)
        {
            _manager.Unregister(_registeredSource);
            _registeredSource = IntPtr.Zero;
            _thumb = IntPtr.Zero;
        }

        if (SourceHandle == IntPtr.Zero) return;

        try
        {
            _thumb = _manager.Register(SourceHandle);
            _registeredSource = SourceHandle;
            UpdateRect();
        }
        catch
        {
            // Source window may have died between enumeration and registration.
        }
    }

    private void UpdateRect()
    {
        if (_manager is null || _thumb == IntPtr.Zero || _hostWindow is null) return;
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        // Dest rect MUST be in physical pixels relative to the host window's client
        // area. Critically — at >100% DPI (e.g. 150%), ActualWidth/ActualHeight are
        // DIPs and need to be scaled. Using PointToScreen on both corners gives us
        // physical-pixel coordinates throughout, so the subtraction yields a width
        // and height in the same unit as the (x,y) offset. Earlier this mixed DIPs
        // (for w/h) with physical pixels (for x/y) and the thumbnail rendered at
        // 1/dpiScale of the tile size — looked exactly like letterbox padding.
        var topLeftPx     = PointToScreen(new Point(0, 0));
        var bottomRightPx = PointToScreen(new Point(ActualWidth, ActualHeight));
        var windowTopLeft = _hostWindow.PointToScreen(new Point(0, 0));

        int x = (int)Math.Round(topLeftPx.X - windowTopLeft.X);
        int y = (int)Math.Round(topLeftPx.Y - windowTopLeft.Y);
        int w = (int)Math.Round(bottomRightPx.X - topLeftPx.X);
        int h = (int)Math.Round(bottomRightPx.Y - topLeftPx.Y);

        // DWM opacity is a single byte 0..255. ~80 (~31%) reads as clearly dimmed
        // without being so faint that you can't tell what window it is.
        byte opacity = IsLocked ? (byte)80 : (byte)255;

        // Centre-crop the source to match the destination aspect ratio so the
        // thumbnail FILLS the tile instead of letterboxing inside it. DWM stretches
        // the cropped rect across the destination — that's what makes the preview
        // feel like it dominates the tile rather than floating in a black box.
        (int X, int Y, int W, int H)? crop = null;
        if (_manager.TryGetSourceSize(_thumb, out int srcW, out int srcH))
        {
            crop = ThumbnailManager.ComputeCenteredCrop(srcW, srcH, w, h);
        }

        _manager.UpdateRect(_thumb, x, y, w, h,
            opacity: opacity,
            sourceClientAreaOnly: false,
            cropSource: crop);
    }

    public void Dispose()
    {
        if (_manager is not null)
        {
            _manager.Dispose();
            _manager = null;
        }
        _thumb = IntPtr.Zero;
        _registeredSource = IntPtr.Zero;
    }
}
