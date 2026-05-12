using WindowMagnet.Config;
using Xunit;

namespace WindowMagnet.Tests;

public class ProfileResolverTests
{
    [Fact]
    public void ProcessName_WinsOverTitle()
    {
        // Per DESIGN §5: processName matches resolve before titleContains.
        var profile = new Profile
        {
            DefaultSlot = new Slot { Width = 1200 },
            Rules = new[]
            {
                new Rule { Match = new Match { ProcessName = "chrome.exe" }, Slot = new Slot { Width = 1400 } },
                new Rule { Match = new Match { WindowTitleContains = "Chrome" }, Slot = new Slot { Width = 999 } },
            },
        };
        var slot = new ProfileResolver(profile).Resolve("chrome.exe", "Google Chrome — github.com");
        Assert.Equal(1400, slot.Width);
    }

    [Fact]
    public void Title_MatchesWhenProcessDoesNotMatch()
    {
        var profile = new Profile
        {
            DefaultSlot = new Slot { Width = 1200 },
            Rules = new[]
            {
                new Rule { Match = new Match { WindowTitleContains = "Discord" }, Slot = new Slot { Width = 800 } },
            },
        };
        var slot = new ProfileResolver(profile).Resolve("unknown.exe", "Discord — #gaming");
        Assert.Equal(800, slot.Width);
    }

    [Fact]
    public void ProcessName_MatchesCaseInsensitively()
    {
        var profile = new Profile
        {
            DefaultSlot = new Slot { Width = 1200 },
            Rules = new[]
            {
                new Rule { Match = new Match { ProcessName = "chrome.exe" }, Slot = new Slot { Width = 1400 } },
            },
        };
        var slot = new ProfileResolver(profile).Resolve("Chrome.EXE", null);
        Assert.Equal(1400, slot.Width);
    }

    [Fact]
    public void Title_MatchesCaseInsensitively()
    {
        var profile = new Profile
        {
            DefaultSlot = new Slot { Width = 1200 },
            Rules = new[]
            {
                new Rule { Match = new Match { WindowTitleContains = "discord" }, Slot = new Slot { Width = 800 } },
            },
        };
        var slot = new ProfileResolver(profile).Resolve(null, "DISCORD — #gaming");
        Assert.Equal(800, slot.Width);
    }

    [Fact]
    public void NoMatch_FallsBackToDefault()
    {
        var profile = new Profile
        {
            DefaultSlot = new Slot { Width = 1200, Height = 800, Anchor = "top" },
            Rules = new[]
            {
                new Rule { Match = new Match { ProcessName = "spotify.exe" }, Slot = new Slot { Width = 1000 } },
            },
        };
        var slot = new ProfileResolver(profile).Resolve("anything.exe", "Anything");
        Assert.Equal(1200, slot.Width);
        Assert.Equal("top", slot.Anchor);
    }

    [Fact]
    public void FirstMatchingProcessRule_Wins()
    {
        var profile = new Profile
        {
            DefaultSlot = new Slot { Width = 1200 },
            Rules = new[]
            {
                new Rule { Match = new Match { ProcessName = "chrome.exe" }, Slot = new Slot { Width = 1400 } },
                new Rule { Match = new Match { ProcessName = "chrome.exe" }, Slot = new Slot { Width = 9999 } },
            },
        };
        var slot = new ProfileResolver(profile).Resolve("chrome.exe", null);
        Assert.Equal(1400, slot.Width);
    }

    [Fact]
    public void EmptyProfile_ReturnsDefaultSlot()
    {
        var slot = new ProfileResolver(new Profile()).Resolve("anything.exe", "anything");
        Assert.Equal(1200, slot.Width);
        Assert.Equal("top", slot.Anchor);
        Assert.Equal(2, slot.Monitor);
    }

    [Fact]
    public void NullInputs_DoNotThrow()
    {
        var profile = new Profile
        {
            Rules = new[]
            {
                new Rule { Match = new Match { ProcessName = "chrome.exe" }, Slot = new Slot { Width = 1400 } },
            },
        };
        var slot = new ProfileResolver(profile).Resolve(null, null);
        Assert.Equal(profile.DefaultSlot.Width, slot.Width);
    }
}
