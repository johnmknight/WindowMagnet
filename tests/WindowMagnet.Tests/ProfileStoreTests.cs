using System.IO;
using WindowMagnet.Config;
using Xunit;

namespace WindowMagnet.Tests;

public class ProfileStoreTests
{
    [Fact]
    public void SaveThenLoad_RoundTripsAllFields()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        try
        {
            var store = new ProfileStore(tmp);
            var original = new Profile
            {
                Version = 1,
                DefaultSlot = new Slot { Anchor = "top-left", Width = 1000, Height = 700, OffsetX = 10 },
                Rules = new[]
                {
                    new Rule
                    {
                        Match = new Match { ProcessName = "chrome.exe" },
                        Slot = new Slot { Anchor = "top-center", Width = 1400, Height = 900 },
                    },
                    new Rule
                    {
                        Match = new Match { WindowTitleContains = "Discord" },
                        Slot = new Slot { Anchor = "top-right", Width = 800, Height = 1000 },
                    },
                },
            };
            store.Save(original);
            var loaded = store.Load();

            Assert.Equal(original.Version, loaded.Version);
            Assert.Equal(original.DefaultSlot.Anchor, loaded.DefaultSlot.Anchor);
            Assert.Equal(original.DefaultSlot.Width, loaded.DefaultSlot.Width);
            Assert.Equal(original.DefaultSlot.OffsetX, loaded.DefaultSlot.OffsetX);
            Assert.Equal(2, loaded.Rules.Count);
            Assert.Equal("chrome.exe", loaded.Rules[0].Match.ProcessName);
            Assert.Equal(1400, loaded.Rules[0].Slot.Width);
            Assert.Equal("Discord", loaded.Rules[1].Match.WindowTitleContains);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void Load_ReturnsDefaultsWhenFileMissing()
    {
        var nonexistent = Path.Combine(Path.GetTempPath(), "does-not-exist-" + Path.GetRandomFileName());
        var profile = new ProfileStore(nonexistent).Load();
        Assert.NotNull(profile);
        Assert.Equal(1, profile.Version);
        Assert.NotNull(profile.DefaultSlot);
        Assert.Empty(profile.Rules);
    }

    [Fact]
    public void Load_ReturnsDefaultsWhenFileIsCorrupt()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        try
        {
            File.WriteAllText(tmp, "this is not valid json {{");
            var profile = new ProfileStore(tmp).Load();
            Assert.NotNull(profile);
            Assert.Equal(1, profile.Version);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    public void Save_CreatesDirectoryIfMissing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "WindowMagnet-Test-" + Path.GetRandomFileName());
        var file = Path.Combine(dir, "profiles.json");
        try
        {
            new ProfileStore(file).Save(new Profile());
            Assert.True(File.Exists(file));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
