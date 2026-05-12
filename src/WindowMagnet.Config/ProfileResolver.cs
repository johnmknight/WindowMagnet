namespace WindowMagnet.Config;

/// <summary>
/// Picks the best <see cref="Slot"/> for a window. Resolution order per DESIGN.md §5:
/// processName match wins first, then titleContains, then the defaultSlot.
/// </summary>
public sealed class ProfileResolver
{
    private readonly Profile _profile;

    public ProfileResolver(Profile profile) => _profile = profile;

    public Slot Resolve(string? processName, string? windowTitle)
    {
        if (!string.IsNullOrEmpty(processName))
        {
            var byProc = _profile.Rules.FirstOrDefault(r =>
                !string.IsNullOrEmpty(r.Match.ProcessName)
                && string.Equals(r.Match.ProcessName, processName, StringComparison.OrdinalIgnoreCase));
            if (byProc is not null) return byProc.Slot;
        }

        if (!string.IsNullOrEmpty(windowTitle))
        {
            var byTitle = _profile.Rules.FirstOrDefault(r =>
                !string.IsNullOrEmpty(r.Match.WindowTitleContains)
                && windowTitle.Contains(r.Match.WindowTitleContains!, StringComparison.OrdinalIgnoreCase));
            if (byTitle is not null) return byTitle.Slot;
        }

        return _profile.DefaultSlot;
    }
}
