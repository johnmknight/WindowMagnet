using WindowMagnet.Config;

namespace WindowMagnet.Core;

/// <summary>
/// Translates a symbolic <see cref="Slot"/> (anchor + width + height + offsets) into a
/// concrete pixel rectangle on a given monitor. See DESIGN.md §5.
/// </summary>
public static class SlotCalculator
{
    public static WindowBounds Compute(Slot slot, WindowBounds monitor)
    {
        int w = slot.Width;
        int h = slot.Height;
        int x = HorizontalAnchor(slot.Anchor) switch
        {
            HAnchor.Left   => monitor.X + slot.OffsetX,
            HAnchor.Right  => monitor.Right - w + slot.OffsetX,
            _              => monitor.X + (monitor.Width - w) / 2 + slot.OffsetX,
        };
        int y = VerticalAnchor(slot.Anchor) switch
        {
            VAnchor.Bottom => monitor.Bottom - h + slot.OffsetY,
            VAnchor.Middle => monitor.Y + (monitor.Height - h) / 2 + slot.OffsetY,
            _              => monitor.Y + slot.OffsetY, // top is the WindowMagnet default
        };
        return new WindowBounds(x, y, w, h);
    }

    private enum HAnchor { Left, Center, Right }
    private enum VAnchor { Top, Middle, Bottom }

    private static HAnchor HorizontalAnchor(string anchor)
    {
        if (anchor.EndsWith("-left", StringComparison.OrdinalIgnoreCase))  return HAnchor.Left;
        if (anchor.EndsWith("-right", StringComparison.OrdinalIgnoreCase)) return HAnchor.Right;
        return HAnchor.Center;
    }

    private static VAnchor VerticalAnchor(string anchor)
    {
        if (anchor.StartsWith("bottom", StringComparison.OrdinalIgnoreCase)) return VAnchor.Bottom;
        if (anchor.StartsWith("middle", StringComparison.OrdinalIgnoreCase)) return VAnchor.Middle;
        return VAnchor.Top;
    }
}
