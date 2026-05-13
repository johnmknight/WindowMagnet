namespace WindowMagnet.Core;

/// <summary>Physical pixel rectangle (Win32 virtual screen coordinates).</summary>
public readonly record struct WindowBounds(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}

/// <summary>
/// A snapshot of a top-level window discovered by <see cref="WindowEnumerator"/>.
/// </summary>
/// <param name="CanMove">
/// True if the current process has integrity rights to SetWindowPos this window.
/// Higher-IL targets (e.g. an elevated Task Manager) return false — the UI greys
/// the tile and the click handler short-circuits with an explanatory status line.
/// </param>
public sealed record WindowInfo(
    IntPtr Handle,
    string Title,
    string ProcessName,
    uint ProcessId,
    WindowBounds Bounds,
    bool IsMinimized,
    bool CanMove)
{
    /// <summary>Convenience for XAML triggers/bindings — the inverse of CanMove.</summary>
    public bool IsLocked => !CanMove;

    /// <summary>Tooltip text shown on the tile — title plus an admin hint when locked.</summary>
    public string Tooltip =>
        CanMove ? Title : Title + "\n(requires admin to move)";
}
