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
        // Honour explicit Width/Height; otherwise prefer 16:9 to match the
        // typical aspect of recalled windows.
        double w = double.IsFinite(Width) ? Width : (double.IsFinite(availableSize.Width) ? availableSize.Width : 100);
        double h = double.IsFinite(Height) ? Height : w * 9.0 / 16.0;
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

        // The destination rect is in client-area coordinates of the host window.
        var elementOnScreen = PointToScreen(new Point(0, 0));
        var windowOnScreen = _hostWindow.PointToScreen(new Point(0, 0));

        int x = (int)Math.Round(elementOnScreen.X - windowOnScreen.X);
        int y = (int)Math.Round(elementOnScreen.Y - windowOnScreen.Y);
        int w = (int)Math.Round(ActualWidth);
        int h = (int)Math.Round(ActualHeight);

        // DWM opacity is a single byte 0..255. ~80 (~31%) reads as clearly dimmed
        // without being so faint that you can't tell what window it is.
        byte opacity = IsLocked ? (byte)80 : (byte)255;
        _manager.UpdateRect(_thumb, x, y, w, h, opacity: opacity, sourceClientAreaOnly: false);
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
