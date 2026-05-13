using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WindowMagnet.App.Tray;

/// <summary>
/// Registers a global system-wide hotkey via Win32 RegisterHotKey and raises
/// <see cref="Pressed"/> when the user hits it. The owning window's HWND receives
/// the WM_HOTKEY message; an HwndSource hook translates that to a managed event.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    // Modifier bits matching Win32 RegisterHotKey constants.
    [Flags]
    public enum Modifiers : uint
    {
        None = 0,
        Alt = 0x0001,
        Control = 0x0002,
        Shift = 0x0004,
        Win = 0x0008,
        NoRepeat = 0x4000,
    }

    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly IntPtr _hwnd;
    private readonly HwndSource? _source;
    private readonly int _id;
    private bool _registered;

    public event Action? Pressed;

    /// <summary>
    /// Register a hotkey for the given window. The hwnd must already be created
    /// (call after Window.SourceInitialized). Returns whether registration succeeded;
    /// failure is silent — the user just won't get the hotkey, the app still runs.
    /// </summary>
    public HotkeyManager(Window owner, Modifiers modifiers, uint virtualKey, int id = 0x4D57)
    {
        var helper = new WindowInteropHelper(owner);
        _hwnd = helper.Handle;
        _id = id;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);

        _registered = RegisterHotKey(_hwnd, _id, (uint)modifiers, virtualKey);
        if (!_registered)
        {
            int err = Marshal.GetLastWin32Error();
            App.Log($"hotkey: RegisterHotKey FAILED err={err} (likely already taken by another app)");
        }
    }

    public bool IsRegistered => _registered;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == _id)
        {
            Pressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered)
        {
            UnregisterHotKey(_hwnd, _id);
            _registered = false;
        }
        _source?.RemoveHook(WndProc);
    }
}
