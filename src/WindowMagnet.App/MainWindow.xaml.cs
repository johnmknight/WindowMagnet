using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using WindowMagnet.App.Dialogs;
using WindowMagnet.Config;
using WindowMagnet.Core;

namespace WindowMagnet.App;

public partial class MainWindow : Window
{
    private readonly WindowEnumerator _enumerator = new();
    private readonly DispatcherTimer _timer;
    private readonly ProfileStore _store = new();
    private Profile _profile;
    private ProfileResolver _resolver;

    public ObservableCollection<WindowInfo> Windows { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        ThumbList.ItemsSource = Windows;

        _profile = _store.Load();
        _resolver = new ProfileResolver(_profile);

        // Tell the enumerator to exclude our own HWND once we have one.
        SourceInitialized += OnSourceInitialized;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        _timer.Tick += (_, _) => RefreshWindows();

        // ContentRendered fires AFTER WPF has settled SizeToContent and per-monitor DPI
        // layout, so Win32 SetWindowPos sticks. Loaded is too early — it runs before
        // SizeToContent has computed the final size and before any WM_DPICHANGED
        // adjustment, so a manual move gets undone by subsequent re-layout.
        ContentRendered += (_, _) => PositionPickerWindow();
        Loaded += (_, _) => { RefreshWindows(); _timer.Start(); };
        Closed += (_, _) => _timer.Stop();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        _enumerator.Exclude(helper.Handle);
        // Tray controller can only register hotkeys once the HWND exists, so this
        // is the earliest safe point to wire it up.
        App.Tray?.AttachTo(this);
    }

    private bool _positioned;

    /// <summary>
    /// Place the picker window on the monitor configured in <c>PickerWindow</c>. Uses
    /// Win32 SetWindowPos with physical pixels so per-monitor DPI translation just
    /// works (WPF receives a WM_DPICHANGED message on cross-DPI moves and rescales).
    /// </summary>
    private void PositionPickerWindow()
    {
        if (_positioned) return;
        _positioned = true;
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) { App.Log("position: hwnd zero"); return; }

            var monitors = Monitors.WorkAreas();
            App.Log($"position: {monitors.Count} monitor(s)");
            for (int i = 0; i < monitors.Count; i++)
            {
                var m = monitors[i];
                App.Log($"  mon{i + 1}: ({m.X},{m.Y}) {m.Width}x{m.Height} @ {m.DpiScale:0.##}x");
            }
            if (monitors.Count == 0) return;

            var prefs = _profile.PickerWindow;
            int idx = Math.Clamp(prefs.Monitor - 1, 0, monitors.Count - 1);
            var target = monitors[idx];

            var current = WindowMover.GetBounds(hwnd);
            App.Log($"position: current bounds ({current.X},{current.Y}) {current.Width}x{current.Height}");

            // Picker Width/Height already reflect WPF's per-monitor DPI scaling (they
            // come from the live HWND in physical pixels). Only the offsets are
            // user-authored and need DPI scaling — apply it manually so we don't have
            // SlotCalculator scale Width/Height on top of WPF's already-scaled values.
            double scale = prefs.ScaleDpi ? target.DpiScale : 1.0;
            var virtualSlot = new Slot
            {
                Monitor = prefs.Monitor,
                Anchor = prefs.Anchor,
                Width = current.Width,
                Height = current.Height,
                OffsetX = (int)Math.Round(prefs.OffsetX * scale),
                OffsetY = (int)Math.Round(prefs.OffsetY * scale),
                ScaleDpi = false, // we already scaled the offsets above
            };
            var pos = SlotCalculator.Compute(virtualSlot, target);
            App.Log($"position: target mon{idx + 1} anchor={prefs.Anchor} dpiScale={scale:0.##} -> ({pos.X},{pos.Y}) {pos.Width}x{pos.Height}");

            bool ok = WindowMover.MoveTo(hwnd, pos.X, pos.Y);
            App.Log($"position: MoveTo returned {ok}");
        }
        catch (Exception ex)
        {
            App.Log($"position: EXCEPTION {ex}");
        }
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

        // Don't clobber an in-progress status message; only update if the label
        // is currently showing a window count (regular state).
        if (CountLabel.Text.EndsWith(" window") || CountLabel.Text.EndsWith(" windows") || string.IsNullOrEmpty(CountLabel.Text))
        {
            CountLabel.Text = Windows.Count == 1 ? "1 window" : $"{Windows.Count} windows";
            CountLabel.Foreground = (System.Windows.Media.Brush)FindResource("PanelTextFaint");
        }
    }

    // ---- chrome ----

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => Hide();
    // The X button hides to tray rather than terminates — the app keeps running so
    // the global hotkey and tray icon stay live. The tray's "Quit" exits for real.
    private void Close_Click(object sender, RoutedEventArgs e) => Hide();
    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshWindows();
    private void Profiles_Click(object sender, RoutedEventArgs e) => OpenProfileDialog();

    /// <summary>
    /// Open the profile editor dialog as a modal. Reloads the profile + resolver on
    /// success so new rules apply on the next thumbnail click. Public so the tray
    /// menu can trigger it without going through a routed event.
    /// </summary>
    public void OpenProfileDialog()
    {
        var dlg = new ProfileDialog(_store, _profile) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _profile = _store.Load();
            _resolver = new ProfileResolver(_profile);
            App.Log("profile: reloaded after dialog save");
            ReportStatus("profile saved");
        }
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshWindows();

    // ---- the magic ----

    private void ThumbButton_Click(object sender, RoutedEventArgs e)
    {
        // The DataContext of each templated button is the WindowInfo bound to that tile.
        // Don't rely on Tag — IntPtr binding through Tag is a known WPF foot-gun where
        // the value can be boxed in unexpected ways (or lost entirely on certain code paths).
        if (sender is not Button btn) { ReportStatus("click: not a button"); return; }
        if (btn.DataContext is not WindowInfo info) { ReportStatus("click: no WindowInfo bound"); return; }

        // Bail early when we know SetWindowPos will fail across the integrity boundary.
        // The tile is already dimmed in XAML; this gives the user a written explanation.
        if (!info.CanMove)
        {
            App.Log($"recall blocked: '{info.Title}' [{info.ProcessName}] is higher integrity (needs admin)");
            ReportStatus($"{TruncateTitle(info.Title)} needs admin to move");
            return;
        }

        var slot = _resolver.Resolve(info.ProcessName, info.Title);

        var monitors = Monitors.WorkAreas();
        if (monitors.Count == 0) { ReportStatus("no monitors enumerated"); return; }
        int idx = Math.Clamp(slot.Monitor - 1, 0, monitors.Count - 1);
        var monitor = monitors[idx];
        var target = SlotCalculator.Compute(slot, monitor); // picks up monitor.DpiScale when slot.ScaleDpi

        bool ok = WindowMover.Recall(info.Handle, target);
        int err = ok ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        App.Log($"recall '{info.Title}' [{info.ProcessName}] -> mon{idx + 1} {slot.Anchor} ({target.X},{target.Y}) {target.Width}x{target.Height} dpiScale={(slot.ScaleDpi ? monitor.DpiScale : 1.0):0.##}: ok={ok} err={err}");
        ReportStatus(ok
            ? $"recalled {TruncateTitle(info.Title)} → mon{idx + 1} {slot.Anchor} {target.Width}×{target.Height}"
            : $"recall FAILED ({err}) for {TruncateTitle(info.Title)}");
    }

    private static string TruncateTitle(string s)
        => s.Length <= 24 ? s : s.Substring(0, 21) + "...";

    /// <summary>
    /// Show a transient status message in the count label area. Reverts to the
    /// window count on the next refresh tick.
    /// </summary>
    private void ReportStatus(string text)
    {
        CountLabel.Text = text;
        CountLabel.Foreground = (System.Windows.Media.Brush)FindResource("PanelText");
    }
}
