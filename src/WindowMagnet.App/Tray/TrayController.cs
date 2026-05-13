using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace WindowMagnet.App.Tray;

/// <summary>
/// Owns the system-tray icon and its context menu, plus the global hotkey that
/// summons the picker. Wraps both so MainWindow doesn't need to know either API.
/// <para>
/// Lifecycle: <see cref="AttachTo"/> is called once after MainWindow has its
/// HWND (use the <c>SourceInitialized</c> event). <see cref="Dispose"/> on app
/// shutdown to drop the icon (otherwise it lingers in the tray until you hover).
/// </para>
/// </summary>
public sealed class TrayController : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    // VK_OEM_3 = the ` ~ key on US keyboards. Win+` is the default summon hotkey.
    private const uint VK_OEM_3 = 0xC0;

    private readonly WinForms.NotifyIcon _notify;
    private readonly Icon _icon;
    private readonly IntPtr _hicon;
    private MainWindow? _window;
    private HotkeyManager? _hotkey;

    public TrayController()
    {
        (_icon, _hicon) = CreateBrandIcon();

        _notify = new WinForms.NotifyIcon
        {
            Icon = _icon,
            Text = "WindowMagnet",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };
        _notify.MouseClick += OnTrayClick;
    }

    /// <summary>Hook the tray + hotkey up to the picker window.</summary>
    public void AttachTo(MainWindow window)
    {
        _window = window;
        // Win+` toggles the picker. NoRepeat keeps a held key from auto-firing.
        _hotkey = new HotkeyManager(
            window,
            HotkeyManager.Modifiers.Win | HotkeyManager.Modifiers.NoRepeat,
            VK_OEM_3);
        _hotkey.Pressed += TogglePicker;
        App.Log($"tray: attached, hotkey registered={_hotkey.IsRegistered}");
    }

    private WinForms.ContextMenuStrip BuildMenu()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Show picker", null, (_, _) => ShowPicker());
        menu.Items.Add("Hide picker", null, (_, _) => HidePicker());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Edit profile...", null, (_, _) => OpenProfile());

        // Checkable "Start with Windows" — reflects HKCU\...\Run and toggles it.
        // CheckOnClick lets ToolStripMenuItem handle the visual flip; we sync the
        // registry in the CheckedChanged handler.
        var startupItem = new WinForms.ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = StartupRegistration.IsEnabled(),
        };
        startupItem.CheckedChanged += (_, _) => StartupRegistration.SetEnabled(startupItem.Checked);
        // Refresh the visible check state when the menu is about to open, in case
        // the registry was touched out-of-band (regedit, another install path, etc.).
        menu.Opening += (_, _) => startupItem.Checked = StartupRegistration.IsEnabled();
        menu.Items.Add(startupItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Application.Current.Shutdown());
        return menu;
    }

    private void OnTrayClick(object? sender, WinForms.MouseEventArgs e)
    {
        // Left-click toggles. Right-click is consumed by the ContextMenuStrip automatically.
        if (e.Button == WinForms.MouseButtons.Left) TogglePicker();
    }

    private void TogglePicker()
    {
        if (_window is null) return;
        if (_window.IsVisible && _window.WindowState != WindowState.Minimized) HidePicker();
        else ShowPicker();
    }

    private void ShowPicker()
    {
        if (_window is null) return;
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Show();
        _window.Activate();
    }

    private void HidePicker()
    {
        _window?.Hide();
    }

    private void OpenProfile()
    {
        if (_window is null) return;
        ShowPicker();
        _window.OpenProfileDialog();
    }

    // ==== Icon ====

    private static (Icon icon, IntPtr handle) CreateBrandIcon()
    {
        // 32x32 brand mark: rounded blue square + white "W". Renders well at 16px
        // (typical tray size) thanks to AntiAlias smoothing.
        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            using var brush = new SolidBrush(Color.FromArgb(76, 140, 255)); // matches AccentBrush
            using var path = RoundedRect(new Rectangle(1, 1, size - 2, size - 2), 6);
            g.FillPath(brush, path);

            using var font = new Font("Segoe UI", 15, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
            using var textBrush = new SolidBrush(Color.White);
            var sz = g.MeasureString("W", font);
            g.DrawString("W", font, textBrush, (size - sz.Width) / 2 + 0.5f, (size - sz.Height) / 2);
        }
        IntPtr handle = bmp.GetHicon();
        return (Icon.FromHandle(handle), handle);
    }

    private static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public void Dispose()
    {
        _hotkey?.Dispose();
        _notify.Visible = false;
        _notify.Dispose();
        _icon.Dispose();
        if (_hicon != IntPtr.Zero) DestroyIcon(_hicon);
    }
}
