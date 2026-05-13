using System.IO;
using WindowMagnet.Config;
using Xunit;

namespace WindowMagnet.Tests;

public class PickerWindowTests
{
    [Fact]
    public void Defaults_TargetMonitor2_TopRight()
    {
        var p = new PickerWindow();
        Assert.Equal(2, p.Monitor);
        Assert.Equal("top-right", p.Anchor);
        Assert.Equal(20, p.OffsetX);
        Assert.Equal(20, p.OffsetY);
    }

    [Fact]
    public void Profile_IncludesPickerWindowByDefault()
    {
        var profile = new Profile();
        Assert.NotNull(profile.PickerWindow);
        Assert.Equal(2, profile.PickerWindow.Monitor);
    }

    [Fact]
    public void RoundTripsViaJson()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        try
        {
            var original = new Profile
            {
                PickerWindow = new PickerWindow { Monitor = 3, Anchor = "bottom-left", OffsetX = 50, OffsetY = 100 },
            };
            new ProfileStore(tmp).Save(original);
            var loaded = new ProfileStore(tmp).Load();
            Assert.Equal(3, loaded.PickerWindow.Monitor);
            Assert.Equal("bottom-left", loaded.PickerWindow.Anchor);
            Assert.Equal(50, loaded.PickerWindow.OffsetX);
            Assert.Equal(100, loaded.PickerWindow.OffsetY);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void OldConfigWithoutPickerWindow_GetsDefault()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        try
        {
            // Simulate a profiles.json written before this feature existed
            File.WriteAllText(tmp, """
                {
                  "version": 1,
                  "defaultSlot": { "monitor": 2, "anchor": "top", "width": 1200, "height": 800 },
                  "rules": []
                }
                """);
            var loaded = new ProfileStore(tmp).Load();
            Assert.NotNull(loaded.PickerWindow);
            Assert.Equal(2, loaded.PickerWindow.Monitor);  // default
            Assert.Equal("top-right", loaded.PickerWindow.Anchor);  // default
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
