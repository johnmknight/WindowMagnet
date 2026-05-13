using WindowMagnet.Config;

namespace WindowMagnet.Core;

/// <summary>
/// Translates a symbolic <see cref="Slot"/> (anchor + width + height + offsets) into a
/// concrete pixel rectangle on a given monitor. See DESIGN.md §5.
/// </summary>
public static class SlotCalculator
{
    /// <summary>
    /// Compute the pixel rect for <paramref name="slot"/> on <paramref name="monitor"/>.
    /// When <paramref name="slot"/>.ScaleDpi is true, the slot's Width/Height/OffsetX/OffsetY
    /// are treated as logical pixels (100%-DPI) and multiplied by <paramref name="dpiScale"/>
    /// to get physical pixels. When ScaleDpi is false (or the scale is 1.0), the slot
    /// values are used directly — preserving back-compat with v0.2 profiles that were
    /// authored with explicit physical-pixel sizes.
    /// </summary>
    public static WindowBounds Compute(Slot slot, WindowBounds monitor, double dpiScale = 1.0)
    {
        double scale = slot.ScaleDpi ? dpiScale : 1.0;
        int w  = (int)System.Math.Round(slot.Width  * scale);
        int h  = (int)System.Math.Round(slot.Height * scale);
        int ox = (int)System.Math.Round(slot.OffsetX * scale);
        int oy = (int)System.Math.Round(slot.OffsetY * scale);

        // Offsets always move AWAY from the anchored edge toward the interior of the
        // monitor — so for Right/Bottom anchors we subtract, for Left/Top we add.
        // Center/Middle offsets are simple translations.
        int x = HorizontalAnchor(slot.Anchor) switch
        {
            HAnchor.Left   => monitor.X + ox,
            HAnchor.Right  => monitor.Right - w - ox,
            _              => monitor.X + (monitor.Width - w) / 2 + ox,
        };
        int y = VerticalAnchor(slot.Anchor) switch
        {
            VAnchor.Bottom => monitor.Bottom - h - oy,
            VAnchor.Middle => monitor.Y + (monitor.Height - h) / 2 + oy,
            _              => monitor.Y + oy, // top is the WindowMagnet default
        };
        return new WindowBounds(x, y, w, h);
    }

    /// <summary>
    /// Convenience overload that pulls the DPI scale out of a <see cref="MonitorInfo"/>.
    /// </summary>
    public static WindowBounds Compute(Slot slot, MonitorInfo monitor)
        => Compute(slot, monitor.Bounds, monitor.DpiScale);

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
