using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using WindowMagnet.Config;
using WindowMagnet.Core;

namespace WindowMagnet.App;

public partial class MainWindow : Window
{
    private readonly WindowEnumerator _enumerator = new();
    private readonly DispatcherTimer _timer;
    private readonly ProfileStore _store = new();
    private ProfileResolver _resolver = null!;

    public ObservableCollection<WindowInfo> Windows { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        ThumbList.ItemsSource = Windows;

        _resolver = new ProfileResolver(_store.Load());

        // Tell the enumerator to exclude our own HWND once we have one.
        SourceInitialized += OnSourceInitialized;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        _timer.Tick += (_, _) => RefreshWindows();

        Loaded += (_, _) => { RefreshWindows(); _timer.Start(); };
        Closed += (_, _) => _timer.Stop();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        _enumerator.Exclude(helper.Handle);
        PositionOnMonitor2();
    }

    private void PositionOnMonitor2()
    {
        var mons = Monitors.WorkAreas();
        if (mons.Count < 2) return;

        // Heuristic: pick the monitor whose top-left isn't (0,0). On a typical
        // dual-monitor setup, monitor 1 is at origin and monitor 2 is offset.
        // Fall back to mons[1] if all have the same origin (single virtual).
        var target = mons.FirstOrDefault(m => m.X != 0 || m.Y != 0);
        if (target.Equals(default(WindowBounds))) target = mons[1];

        // Park top-right corner of monitor 2 with a small inset.
        const int inset = 20;
        Left = target.Right - Width - inset;
        Top  = target.Y + inset;
    }

    private void RefreshWindows()
    {
        var current = _enumerator.Enumerate();

        var filter = FilterBox.Text?.Trim();
        if (!string.IsNullOrEmpty(filter))
        {
            current = current
                .Where(w => w.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)
                         || w.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Reconcile: keep existing tiles, add new ones, drop vanished ones.
        var existing = Windows.Select(w => w.Handle).ToHashSet();
        var incoming = current.Select(w => w.Handle).ToHashSet();
        foreach (var w in Windows.Where(w => !incoming.Contains(w.Handle)).ToList())
            Windows.Remove(w);
        foreach (var w in current.Where(w => !existing.Contains(w.Handle)))
            Windows.Add(w);

        CountLabel.Text = Windows.Count == 1 ? "1 window" : $"{Windows.Count} windows";
    }

    // ---- chrome ----

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshWindows();
    private void Profiles_Click(object sender, RoutedEventArgs e)
    {
        // v0.1: open profiles.json in the default editor. A proper editor UI comes in v0.4.
        try
        {
            if (!System.IO.File.Exists(_store.Path)) _store.Save(_store.Load());
            Process.Start(new ProcessStartInfo(_store.Path) { UseShellExecute = true });
        }
        catch
        {
            // ignore — best effort
        }
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshWindows();

    // ---- the magic ----

    private void ThumbButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.Tag is not IntPtr handle) return;

        var info = Windows.FirstOrDefault(w => w.Handle == handle);
        if (info is null) return;

        var slot = _resolver.Resolve(info.ProcessName, info.Title);

        var monitors = Monitors.WorkAreas();
        if (monitors.Count == 0) return;
        int idx = Math.Clamp(slot.Monitor - 1, 0, monitors.Count - 1);

        var target = SlotCalculator.Compute(slot, monitors[idx]);
        WindowMover.Recall(handle, target);
    }
}
