namespace WindowMagnet.Config;

/// <summary>Root config document. See DESIGN.md §5 for schema.</summary>
public sealed record Profile
{
    public int Version { get; init; } = 1;

    /// <summary>Where the picker window itself appears at launch.</summary>
    public PickerWindow PickerWindow { get; init; } = new();

    public Slot DefaultSlot { get; init; } = new();
    public IReadOnlyList<Rule> Rules { get; init; } = Array.Empty<Rule>();
}

/// <summary>
/// Where to position the WindowMagnet picker window itself on startup. Uses the same
/// anchor vocabulary as <see cref="Slot"/>. The picker is sized by WPF; only its
/// position is taken from this record.
/// </summary>
public sealed record PickerWindow
{
    /// <summary>
    /// 1-based monitor index. 1 = primary, 2 = first non-primary, etc. Defaults to 2 —
    /// the whole point of WindowMagnet is to live on the non-game monitor. If the user
    /// only has one monitor, the index is clamped to the available count.
    /// </summary>
    public int Monitor { get; init; } = 2;

    /// <summary>
    /// Anchor on the target monitor. One of: top-left, top, top-center, top-right,
    /// middle-left, middle, middle-right, bottom-left, bottom, bottom-right.
    /// Defaults to top-right — out of the way of typical work content.
    /// </summary>
    public string Anchor { get; init; } = "top-right";

    public int OffsetX { get; init; } = 20;
    public int OffsetY { get; init; } = 20;
}

/// <summary>
/// Where a recalled window should land. Anchor is symbolic so config stays
/// portable across resolutions; pixel translation happens in
/// <c>WindowMagnet.Core.SlotCalculator</c>.
/// </summary>
public sealed record Slot
{
    /// <summary>1-based monitor index. Defaults to 2 — the whole point of this app.</summary>
    public int Monitor { get; init; } = 2;

    /// <summary>One of: top-left, top, top-center, top-right, middle-left, middle, middle-right, bottom-left, bottom, bottom-right.</summary>
    public string Anchor { get; init; } = "top";

    public int Width { get; init; } = 1200;
    public int Height { get; init; } = 800;
    public int OffsetX { get; init; }
    public int OffsetY { get; init; }
}

/// <summary>A match + slot pair.</summary>
public sealed record Rule
{
    public Match Match { get; init; } = new();
    public Slot Slot { get; init; } = new();
}

/// <summary>
/// What to match a window against. ProcessName wins over WindowTitleContains
/// per the resolution order in DESIGN.md §5.
/// </summary>
public sealed record Match
{
    public string? ProcessName { get; init; }
    public string? WindowTitleContains { get; init; }
}
