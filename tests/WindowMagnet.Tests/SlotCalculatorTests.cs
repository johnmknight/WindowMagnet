using WindowMagnet.Config;
using WindowMagnet.Core;
using Xunit;

namespace WindowMagnet.Tests;

public class SlotCalculatorTests
{
    // A typical secondary 1080p monitor offset to the right of primary.
    private static readonly WindowBounds Mon2 = new(1920, 0, 1920, 1080);

    [Fact]
    public void TopAnchor_CentersHorizontallyAtTop()
    {
        var slot = new Slot { Anchor = "top", Width = 1200, Height = 800 };
        var b = SlotCalculator.Compute(slot, Mon2);
        Assert.Equal(1920 + (1920 - 1200) / 2, b.X);
        Assert.Equal(0, b.Y);
        Assert.Equal(1200, b.Width);
        Assert.Equal(800, b.Height);
    }

    [Fact]
    public void TopLeftAnchor_ParksAtTopLeftCorner()
    {
        var slot = new Slot { Anchor = "top-left", Width = 1000, Height = 700 };
        var b = SlotCalculator.Compute(slot, Mon2);
        Assert.Equal(1920, b.X);
        Assert.Equal(0, b.Y);
    }

    [Fact]
    public void TopRightAnchor_ParksAtRightEdge()
    {
        var slot = new Slot { Anchor = "top-right", Width = 800, Height = 1000 };
        var b = SlotCalculator.Compute(slot, Mon2);
        Assert.Equal(1920 + 1920 - 800, b.X);
        Assert.Equal(0, b.Y);
    }

    [Fact]
    public void BottomRightAnchor_ParksAtBottomRightCorner()
    {
        var slot = new Slot { Anchor = "bottom-right", Width = 800, Height = 600 };
        var b = SlotCalculator.Compute(slot, Mon2);
        Assert.Equal(1920 + 1920 - 800, b.X);
        Assert.Equal(1080 - 600, b.Y);
    }

    [Fact]
    public void MiddleAnchor_CentersVerticallyAndHorizontally()
    {
        var slot = new Slot { Anchor = "middle", Width = 1000, Height = 600 };
        var b = SlotCalculator.Compute(slot, Mon2);
        Assert.Equal(1920 + (1920 - 1000) / 2, b.X);
        Assert.Equal((1080 - 600) / 2, b.Y);
    }

    [Fact]
    public void OffsetsAreAppliedAfterAnchor()
    {
        var slot = new Slot { Anchor = "top", Width = 1000, Height = 700, OffsetX = 50, OffsetY = 20 };
        var b = SlotCalculator.Compute(slot, Mon2);
        Assert.Equal(1920 + (1920 - 1000) / 2 + 50, b.X);
        Assert.Equal(20, b.Y);
    }

    [Fact]
    public void TopRightOffset_InsetsFromRightEdge()
    {
        // "top-right" + offsetX 20 should mean "20px in from the right edge",
        // NOT "20px past the right edge". This is the bug that put the picker
        // offscreen on the first PickerWindow build.
        var slot = new Slot { Anchor = "top-right", Width = 800, OffsetX = 20, OffsetY = 30 };
        var b = SlotCalculator.Compute(slot, Mon2);
        Assert.Equal(1920 + 1920 - 800 - 20, b.X);
        Assert.Equal(30, b.Y);
    }

    [Fact]
    public void BottomRightOffset_InsetsFromBothEdges()
    {
        var slot = new Slot { Anchor = "bottom-right", Width = 800, Height = 600, OffsetX = 15, OffsetY = 25 };
        var b = SlotCalculator.Compute(slot, Mon2);
        Assert.Equal(1920 + 1920 - 800 - 15, b.X);
        Assert.Equal(1080 - 600 - 25, b.Y);
    }

    [Fact]
    public void NegativeMonitorOrigin_PreservesSign()
    {
        // Monitor 2 positioned to the LEFT of primary at (-1920, 0) — common setup
        // and a likely source of "hardcoded (0,0)" bugs (see DESIGN §6e).
        var mon = new WindowBounds(-1920, 0, 1920, 1080);
        var slot = new Slot { Anchor = "top-left", Width = 800, Height = 600 };
        var b = SlotCalculator.Compute(slot, mon);
        Assert.Equal(-1920, b.X);
        Assert.Equal(0, b.Y);
    }

    [Fact]
    public void NegativeMonitorOrigin_TopRightStaysOnMonitor()
    {
        var mon = new WindowBounds(-1920, 0, 1920, 1080);
        var slot = new Slot { Anchor = "top-right", Width = 800, Height = 600 };
        var b = SlotCalculator.Compute(slot, mon);
        Assert.Equal(-1920 + 1920 - 800, b.X);
        Assert.Equal(-800, b.X + 0); // == -800
    }

    [Theory]
    [InlineData("top")]
    [InlineData("top-center")]
    [InlineData("bottom")]
    [InlineData("middle")]
    public void HorizontalCenter_IsConsistentAcrossSynonyms(string anchor)
    {
        var slot = new Slot { Anchor = anchor, Width = 1000, Height = 600 };
        var b = SlotCalculator.Compute(slot, Mon2);
        Assert.Equal(1920 + (1920 - 1000) / 2, b.X);
    }

    // ===== DPI scaling =====

    [Fact]
    public void DpiScaleOff_LeavesDimensionsAlone()
    {
        // ScaleDpi=false means the dpiScale argument is ignored — preserves v0.2
        // back-compat where Width/Height were physical pixels authored manually.
        var slot = new Slot { Anchor = "top-left", Width = 1200, Height = 800, ScaleDpi = false };
        var b = SlotCalculator.Compute(slot, Mon2, dpiScale: 1.5);
        Assert.Equal(1920, b.X);
        Assert.Equal(0, b.Y);
        Assert.Equal(1200, b.Width);
        Assert.Equal(800, b.Height);
    }

    [Fact]
    public void DpiScaleOn_ScalesWidthHeight()
    {
        var slot = new Slot { Anchor = "top-left", Width = 1200, Height = 800, ScaleDpi = true };
        var b = SlotCalculator.Compute(slot, Mon2, dpiScale: 1.5);
        Assert.Equal(1920, b.X);
        Assert.Equal(0, b.Y);
        Assert.Equal(1800, b.Width);
        Assert.Equal(1200, b.Height);
    }

    [Fact]
    public void DpiScaleOn_ScalesOffsetsToo()
    {
        // top-right at 150% DPI: 20-DIP inset should land at 30-physical-pixel inset.
        var slot = new Slot
        {
            Anchor = "top-right", Width = 1000, Height = 700, OffsetX = 20, OffsetY = 10,
            ScaleDpi = true,
        };
        var b = SlotCalculator.Compute(slot, Mon2, dpiScale: 1.5);
        // Width scaled to 1500, OffsetX scaled to 30 -> inset 30 from right edge.
        Assert.Equal(1920 + 1920 - 1500 - 30, b.X);
        // OffsetY scaled to 15.
        Assert.Equal(15, b.Y);
        Assert.Equal(1500, b.Width);
        Assert.Equal(1050, b.Height);
    }

    [Fact]
    public void DpiScaleOn_With2xDpi_DoublesWidthHeight()
    {
        var slot = new Slot { Anchor = "middle", Width = 800, Height = 600, ScaleDpi = true };
        var b = SlotCalculator.Compute(slot, Mon2, dpiScale: 2.0);
        Assert.Equal(1600, b.Width);
        Assert.Equal(1200, b.Height);
    }

    [Fact]
    public void DpiScaleOn_ScaleEqualsOne_BehavesLikeOff()
    {
        // 1.0x = no-op even when ScaleDpi is on. Important so tests that don't
        // pass a dpiScale (default 1.0) still get predictable arithmetic.
        var slot = new Slot { Anchor = "top", Width = 1000, Height = 600, ScaleDpi = true };
        var b = SlotCalculator.Compute(slot, Mon2, dpiScale: 1.0);
        Assert.Equal(1920 + (1920 - 1000) / 2, b.X);
        Assert.Equal(1000, b.Width);
        Assert.Equal(600, b.Height);
    }
}
