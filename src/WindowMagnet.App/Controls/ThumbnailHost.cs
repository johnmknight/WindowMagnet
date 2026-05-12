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
/// </summary>
public sealed class ThumbnailHost : FrameworkElement, IDisposable
{
    public static readonly DependencyProperty SourceHandleProperty =
        DependencyProperty.Register(
            nameof(SourceHandle),
            typeof(IntPtr),
            typeof(ThumbnailHost),
            new PropertyMetadata(IntPtr.Zero, OnSourceHandleChanged));

    public IntPtr SourceHandle
    {
        get => (IntPtr)GetValue(SourceHandleProperty);
        set => SetValue(SourceHandleProperty, value);
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
        // PointToScreen returns DIP-scaled coords for the WPF element; convert
        // to physical pixels (the DWM API expects physical) using the host's
        // VisualTransform. Under PerMonitorV2 awareness, WPF coords are already
        // in physical pixels for windows positioned by hwnd, so for v0.1 we use
        // a straightforward subtraction.
        var elementOnScreen = PointToScreen(new Point(0, 0));
        var windowOnScreen = _hostWindow.PointToScreen(new Point(0, 0));

        int x = (int)Math.Round(elementOnScreen.X - windowOnScreen.X);
        int y = (int)Math.Round(elementOnScreen.Y - windowOnScreen.Y);
        int w = (int)Math.Round(ActualWidth);
        int h = (int)Math.Round(ActualHeight);

        _manager.UpdateRect(_thumb, x, y, w, h, opacity: 255, sourceClientAreaOnly: false);
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
