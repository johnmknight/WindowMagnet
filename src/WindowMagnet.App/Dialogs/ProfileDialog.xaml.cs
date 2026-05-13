using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WindowMagnet.Config;
using WindowMagnet.Core;

namespace WindowMagnet.App.Dialogs;

/// <summary>
/// Editor for the on-disk profile (picker placement, default slot, per-app rules).
/// Loads the current profile, lets the user edit it through controls, and writes
/// it back via <see cref="ProfileStore.Save"/> on OK. Cancel discards.
/// </summary>
public partial class ProfileDialog : Window
{
    private static readonly string[] Anchors = new[]
    {
        "top-left",    "top",          "top-right",
        "middle-left", "middle",       "middle-right",
        "bottom-left", "bottom",       "bottom-right",
    };

    private readonly ProfileStore _store;
    private Profile _initial;

    // Working copy of the rules — bound to the ItemsControl. Edited through
    // Add/Delete; the Save handler turns this back into a Profile.Rule list.
    public ObservableCollection<RuleRow> Rules { get; } = new();

    private string _pickerAnchor;
    private string _defaultAnchor;

    public ProfileDialog(ProfileStore store, Profile current)
    {
        InitializeComponent();
        _store = store;
        _initial = current;

        RuleList.ItemsSource = Rules;
        StoragePathLabel.Text = store.Path;

        var monitors = Monitors.WorkAreas();
        BuildMonitorRadios(PickerMonitorRow, monitors, current.PickerWindow.Monitor, PickerMonitor_Checked);
        BuildAnchorGrid(PickerAnchorGrid, current.PickerWindow.Anchor, PickerAnchor_Checked);
        _pickerAnchor = current.PickerWindow.Anchor;
        PickerAnchorLabel.Text = _pickerAnchor;
        PickerOffsetX.Text = current.PickerWindow.OffsetX.ToString();
        PickerOffsetY.Text = current.PickerWindow.OffsetY.ToString();
        PickerScaleDpiToggle.IsChecked = current.PickerWindow.ScaleDpi;

        BuildMonitorCombo(DefaultMonitorCombo, monitors, current.DefaultSlot.Monitor);
        BuildAnchorGrid(DefaultAnchorGrid, current.DefaultSlot.Anchor, DefaultAnchor_Checked);
        _defaultAnchor = current.DefaultSlot.Anchor;
        DefaultAnchorLabel.Text = _defaultAnchor;
        DefaultWidth.Text = current.DefaultSlot.Width.ToString();
        DefaultHeight.Text = current.DefaultSlot.Height.ToString();
        DefaultScaleDpiToggle.IsChecked = current.DefaultSlot.ScaleDpi;

        foreach (var r in current.Rules) Rules.Add(RuleRow.From(r));

        Closing += OnClosing;
    }

    // ===== Layout builders =====

    private void BuildMonitorRadios(StackPanel host, IReadOnlyList<MonitorInfo> monitors,
                                    int selectedMonitor, RoutedEventHandler onChecked)
    {
        host.Children.Clear();
        for (int i = 0; i < monitors.Count; i++)
        {
            var m = monitors[i];
            var rb = new RadioButton
            {
                Style = (Style)FindResource("MonitorCard"),
                GroupName = host.Name,
                Tag = i + 1,
                IsChecked = (i + 1 == selectedMonitor),
            };
            rb.Content = MakeMonitorContent(i + 1, m);
            rb.Checked += onChecked;
            host.Children.Add(rb);
        }
    }

    private static UIElement MakeMonitorContent(int monitorNumber, MonitorInfo m)
    {
        var stack = new StackPanel();
        // Aspect-correct mini representation (capped at 70px max dim).
        const double maxDim = 70;
        double scale = Math.Min(maxDim / m.Width, maxDim / m.Height);
        var rect = new Border
        {
            Width = Math.Max(12, m.Width * scale),
            Height = Math.Max(12, m.Height * scale),
            CornerRadius = new CornerRadius(2),
            BorderThickness = new Thickness(1),
            Background = (Brush)Application.Current.FindResource("FluentAccentFill"),
            BorderBrush = (Brush)Application.Current.FindResource("FluentAccentBorder"),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        stack.Children.Add(rect);

        var label = new TextBlock
        {
            Text = $"Monitor {monitorNumber}",
            FontSize = 11,
            FontWeight = FontWeights.Medium,
            Foreground = (Brush)Application.Current.FindResource("PanelText"),
            Margin = new Thickness(0, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var dims = new TextBlock
        {
            Text = $"{m.Width} × {m.Height}",
            FontSize = 10,
            Foreground = (Brush)Application.Current.FindResource("PanelTextFaint"),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        stack.Children.Add(label);
        stack.Children.Add(dims);
        return stack;
    }

    private void BuildMonitorCombo(ComboBox combo, IReadOnlyList<MonitorInfo> monitors, int selected)
    {
        combo.Items.Clear();
        for (int i = 0; i < monitors.Count; i++)
        {
            var m = monitors[i];
            combo.Items.Add(new ComboBoxItem
            {
                Content = $"Monitor {i + 1}  ({m.Width} × {m.Height})",
                Tag = i + 1,
            });
        }
        // Default selection (clamp into range).
        int idx = Math.Clamp(selected - 1, 0, Math.Max(0, monitors.Count - 1));
        if (combo.Items.Count > 0) combo.SelectedIndex = idx;
    }

    private void BuildAnchorGrid(UniformGrid grid, string selectedAnchor, RoutedEventHandler onChecked)
    {
        grid.Children.Clear();
        foreach (var anchor in Anchors)
        {
            var rb = new RadioButton
            {
                Style = (Style)FindResource("AnchorCell"),
                GroupName = grid.Name,
                Tag = anchor,
                IsChecked = AnchorEquals(anchor, selectedAnchor),
                ToolTip = anchor,
            };
            rb.Checked += onChecked;
            grid.Children.Add(rb);
        }
    }

    /// <summary>Accept "top" as a synonym for "top-center", etc.</summary>
    private static bool AnchorEquals(string cell, string profileAnchor)
    {
        if (string.Equals(cell, profileAnchor, StringComparison.OrdinalIgnoreCase)) return true;
        // top-center / middle-center / bottom-center → top / middle / bottom
        if (cell.EndsWith("-center", StringComparison.OrdinalIgnoreCase))
        {
            var short_ = cell.Substring(0, cell.Length - "-center".Length);
            return string.Equals(short_, profileAnchor, StringComparison.OrdinalIgnoreCase);
        }
        // Middle plain
        if (cell == "middle" && string.Equals("middle", profileAnchor, StringComparison.OrdinalIgnoreCase)) return true;
        // top, bottom, middle equal middle-center class
        if ((cell == "top" || cell == "bottom" || cell == "middle")
            && profileAnchor.Equals(cell + "-center", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // ===== Event handlers =====

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }
    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void PickerMonitor_Checked(object sender, RoutedEventArgs e) { /* read at save */ }
    private void PickerAnchor_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string a)
        {
            _pickerAnchor = a;
            PickerAnchorLabel.Text = a;
        }
    }
    private void DefaultAnchor_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string a)
        {
            _defaultAnchor = a;
            DefaultAnchorLabel.Text = a;
        }
    }

    private void AddRule_Click(object sender, RoutedEventArgs e)
    {
        // New-rule defaults: empty process name, today's default slot.
        var template = new RuleRow
        {
            ProcessName = "",
            Slot = ReadDefaultSlot(),
        };
        if (LaunchRuleEditor(template, isNew: true) is { } edited)
        {
            Rules.Add(edited);
        }
    }

    private void EditRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is RuleRow row)
        {
            // Clone the row before editing so a Cancel doesn't leave mutated state.
            var working = new RuleRow
            {
                ProcessName = row.ProcessName,
                TitleContains = row.TitleContains,
                Slot = row.Slot,
            };
            if (LaunchRuleEditor(working, isNew: false) is { } edited)
            {
                int idx = Rules.IndexOf(row);
                if (idx >= 0) Rules[idx] = edited; else Rules.Add(edited);
            }
        }
    }

    private void DeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is RuleRow row)
        {
            Rules.Remove(row);
        }
    }

    private RuleRow? LaunchRuleEditor(RuleRow seed, bool isNew)
    {
        var monitors = Monitors.WorkAreas();
        var dlg = new RuleEditDialog(seed, monitors, isNew) { Owner = this };
        return dlg.ShowDialog() == true ? dlg.ResultRow : null;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var updated = BuildUpdatedProfile();
            _store.Save(updated);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't save profile:\n{ex.Message}",
                "WindowMagnet", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        // No-op. Could add a "discard unsaved changes?" prompt here later.
    }

    // ===== Profile assembly =====

    private Profile BuildUpdatedProfile()
    {
        var pickerMonitor = GetCheckedMonitor(PickerMonitorRow);
        var defaultMonitor = ((ComboBoxItem)DefaultMonitorCombo.SelectedItem!).Tag is int dm ? dm : 1;

        var picker = new PickerWindow
        {
            Monitor = pickerMonitor,
            Anchor = _pickerAnchor,
            OffsetX = ParseInt(PickerOffsetX.Text, 20),
            OffsetY = ParseInt(PickerOffsetY.Text, 20),
            ScaleDpi = PickerScaleDpiToggle.IsChecked == true,
        };

        var defaultSlot = new Slot
        {
            Monitor = defaultMonitor,
            Anchor = _defaultAnchor,
            Width = ParseInt(DefaultWidth.Text, 1800),
            Height = ParseInt(DefaultHeight.Text, 2400),
            OffsetX = 0,
            OffsetY = 0,
            ScaleDpi = DefaultScaleDpiToggle.IsChecked == true,
        };

        return _initial with
        {
            PickerWindow = picker,
            DefaultSlot = defaultSlot,
            Rules = Rules.Select(r => r.ToRule()).ToList(),
        };
    }

    private Slot ReadDefaultSlot()
    {
        var defaultMonitor = ((ComboBoxItem?)DefaultMonitorCombo.SelectedItem)?.Tag is int dm ? dm : 1;
        return new Slot
        {
            Monitor = defaultMonitor,
            Anchor = _defaultAnchor,
            Width = ParseInt(DefaultWidth.Text, 1800),
            Height = ParseInt(DefaultHeight.Text, 2400),
            ScaleDpi = DefaultScaleDpiToggle.IsChecked == true,
        };
    }

    private static int GetCheckedMonitor(StackPanel host)
    {
        foreach (var child in host.Children)
        {
            if (child is RadioButton rb && rb.IsChecked == true && rb.Tag is int m)
                return m;
        }
        return 1;
    }

    private static int ParseInt(string s, int fallback)
        => int.TryParse(s, out var n) ? n : fallback;

    // ===== Row VM =====

    /// <summary>
    /// Lightweight view-model for a single rule. Mirrors <see cref="Rule"/> but
    /// has the formatting helpers the template binds to.
    /// </summary>
    public sealed class RuleRow
    {
        public string? ProcessName { get; set; }
        public string? TitleContains { get; set; }
        public Slot Slot { get; set; } = new();

        public string MatchKindLabel
            => !string.IsNullOrWhiteSpace(ProcessName) ? "PROCESS"
             : !string.IsNullOrWhiteSpace(TitleContains) ? "TITLE CONTAINS"
             : "MATCH";
        public string MatchValue
            => ProcessName ?? TitleContains ?? "(empty)";
        public string SlotSummary
            => $"Monitor {Slot.Monitor} · {Slot.Anchor} · {Slot.Width} × {Slot.Height}";

        public static RuleRow From(Rule r) => new()
        {
            ProcessName = r.Match.ProcessName,
            TitleContains = r.Match.WindowTitleContains,
            Slot = r.Slot,
        };

        public Rule ToRule() => new()
        {
            Match = new Match
            {
                ProcessName = ProcessName,
                WindowTitleContains = TitleContains,
            },
            Slot = Slot,
        };
    }
}
