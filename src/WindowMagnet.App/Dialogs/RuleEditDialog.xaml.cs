using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using WindowMagnet.Config;
using WindowMagnet.Core;

namespace WindowMagnet.App.Dialogs;

/// <summary>
/// Modal editor for a single <see cref="Rule"/>. The dialog mutates a copy of the
/// passed-in row and only writes it back when Save is clicked. Cancel = discard.
/// <para>
/// Use <see cref="ResultRow"/> after a true <see cref="ShowDialog"/> result to get
/// the edited row.
/// </para>
/// </summary>
public partial class RuleEditDialog : Window
{
    private static readonly string[] Anchors = new[]
    {
        "top-left",    "top",          "top-right",
        "middle-left", "middle",       "middle-right",
        "bottom-left", "bottom",       "bottom-right",
    };

    private string _selectedAnchor;

    /// <summary>The edited row, populated on Save. Null until then.</summary>
    public ProfileDialog.RuleRow? ResultRow { get; private set; }

    public RuleEditDialog(ProfileDialog.RuleRow row, IReadOnlyList<WindowBounds> monitors, bool isNew)
    {
        InitializeComponent();

        TitleLabel.Text = isNew ? "WindowMagnet — Add rule" : "WindowMagnet — Edit rule";
        HeaderLabel.Text = isNew ? "Add rule" : "Edit rule";

        // Match kind: prefer ProcessName when set, otherwise TitleContains, else default to process.
        bool isTitle = string.IsNullOrWhiteSpace(row.ProcessName)
                       && !string.IsNullOrWhiteSpace(row.TitleContains);
        MatchKindProcess.IsChecked = !isTitle;
        MatchKindTitle.IsChecked = isTitle;
        MatchValueBox.Text = isTitle ? (row.TitleContains ?? "") : (row.ProcessName ?? "");
        UpdateMatchHint();

        // Slot
        BuildMonitorCombo(MonitorCombo, monitors, row.Slot.Monitor);
        WidthBox.Text = row.Slot.Width.ToString();
        HeightBox.Text = row.Slot.Height.ToString();
        _selectedAnchor = row.Slot.Anchor;
        BuildAnchorGrid(AnchorGrid, _selectedAnchor);
        AnchorLabel.Text = _selectedAnchor;
    }

    // ===== Layout builders =====

    private static void BuildMonitorCombo(ComboBox combo, IReadOnlyList<WindowBounds> monitors, int selected)
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
        int idx = Math.Clamp(selected - 1, 0, Math.Max(0, monitors.Count - 1));
        if (combo.Items.Count > 0) combo.SelectedIndex = idx;
    }

    private void BuildAnchorGrid(UniformGrid grid, string selected)
    {
        grid.Children.Clear();
        foreach (var anchor in Anchors)
        {
            var rb = new RadioButton
            {
                Style = (Style)FindResource("AnchorCell"),
                GroupName = "RuleAnchor",
                Tag = anchor,
                IsChecked = AnchorEquals(anchor, selected),
                ToolTip = anchor,
            };
            rb.Checked += Anchor_Checked;
            grid.Children.Add(rb);
        }
    }

    private static bool AnchorEquals(string cell, string profileAnchor)
    {
        if (string.Equals(cell, profileAnchor, StringComparison.OrdinalIgnoreCase)) return true;
        if (cell.EndsWith("-center", StringComparison.OrdinalIgnoreCase))
        {
            var short_ = cell.Substring(0, cell.Length - "-center".Length);
            if (string.Equals(short_, profileAnchor, StringComparison.OrdinalIgnoreCase)) return true;
        }
        if ((cell == "top" || cell == "bottom" || cell == "middle")
            && profileAnchor.Equals(cell + "-center", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // ===== Handlers =====

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }
    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void MatchKind_Changed(object sender, RoutedEventArgs e) => UpdateMatchHint();

    private void UpdateMatchHint()
    {
        if (MatchHint == null) return;
        MatchHint.Text = MatchKindTitle.IsChecked == true
            ? "Substring of the window title (case-insensitive)."
            : "The .exe name (e.g. chrome.exe). Matched case-insensitively.";
    }

    private void Anchor_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string a)
        {
            _selectedAnchor = a;
            AnchorLabel.Text = a;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var value = (MatchValueBox.Text ?? "").Trim();
        if (string.IsNullOrEmpty(value))
        {
            MessageBox.Show(this, "Match value can't be empty.", "WindowMagnet",
                MessageBoxButton.OK, MessageBoxImage.Information);
            MatchValueBox.Focus();
            return;
        }

        bool isTitle = MatchKindTitle.IsChecked == true;
        int monitor = ((ComboBoxItem?)MonitorCombo.SelectedItem)?.Tag is int m ? m : 1;
        int width = int.TryParse(WidthBox.Text, out var w) ? w : 1200;
        int height = int.TryParse(HeightBox.Text, out var h) ? h : 800;

        ResultRow = new ProfileDialog.RuleRow
        {
            ProcessName = isTitle ? null : value,
            TitleContains = isTitle ? value : null,
            Slot = new Slot
            {
                Monitor = monitor,
                Anchor = _selectedAnchor,
                Width = width,
                Height = height,
            },
        };
        DialogResult = true;
    }
}
