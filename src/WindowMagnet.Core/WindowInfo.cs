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
public sealed record WindowInfo(
    IntPtr Handle,
    string Title,
    string ProcessName,
    uint ProcessId,
    WindowBounds Bounds,
    bool IsMinimized);
