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
}
