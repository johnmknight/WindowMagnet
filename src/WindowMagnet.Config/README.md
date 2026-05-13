# WindowMagnet.Config

Profile schema, JSON load/save, and per-app rule resolution.

Target framework is plain `net8.0` (no `-windows` suffix) — this project has no Win32
dependencies and stays portable for unit testing. `WindowMagnet.Core`'s `SlotCalculator`
handles the actual anchor → pixel-rect translation against a monitor (it needs the
DPI scale, which is Windows-specific).

## Types

| Type | Responsibility |
|---|---|
| `Profile.cs` | All four records live here: `Profile` (root: `Version`, `PickerWindow`, `DefaultSlot`, `Rules`), `PickerWindow` (where the picker window itself appears at launch — `Monitor`, `Anchor`, `OffsetX/Y`, `ScaleDpi`), `Slot` (recall destination: `Monitor`, `Anchor`, `Width`, `Height`, `OffsetX/Y`, `ScaleDpi`), `Rule` (one `Match` + one `Slot`), `Match` (`ProcessName` OR `WindowTitleContains`). All records are immutable `init`-only so config is round-trippable through `with` clones (used by `MainWindow.AddRuleFor` to append a rule). |
| `ProfileResolver.cs` | Given `(processName, windowTitle)`, returns the best-matching `Slot` per DESIGN.md §5: first `processName` rule wins (case-insensitive), then first `WindowTitleContains` rule (substring match, case-insensitive), then `DefaultSlot`. |
| `ProfileStore.cs` | Loads/saves `Profile` as JSON. Defaults to `%APPDATA%\WindowMagnet\profiles.json` but takes a custom path for tests. Uses `System.Text.Json` with `WriteIndented = true`, `JsonNamingPolicy.CamelCase`, `WhenWritingNull`, comment-tolerance, and trailing-comma tolerance — so the file is comfortably hand-editable. Returns a fresh default `Profile` on missing-file or parse-failure rather than throwing; "bad config crashes the app" is a worse UX than "bad config gets ignored." |
| `profiles.example.json` | A documented example profile with rules for Chrome, Discord, and Spotify. Drop into `%APPDATA%\WindowMagnet\profiles.json` (renamed) as a starting point. |

## Schema

See `DESIGN.md` §5 for the canonical JSON example. The TL;DR:

1. First `Rule` whose `match.processName` matches wins.
2. Otherwise first `Rule` whose `match.windowTitleContains` matches wins.
3. Otherwise the `defaultSlot` applies.

Anchors are symbolic (`top-left`, `top-center`, etc.) so config stays portable across
different monitor resolutions. `WindowMagnet.Core.SlotCalculator` translates anchor +
width/height into concrete pixel coordinates using the target monitor's bounds and
per-monitor DPI scale.

## The `ScaleDpi` flag

Each `Slot` (and the top-level `PickerWindow` record) has a `ScaleDpi` bool that
controls whether `Width`/`Height`/`OffsetX`/`OffsetY` are logical pixels (multiplied
by the target monitor's DPI scale at recall time) or raw physical pixels (used as-is).
The default is **false** to preserve v0.2 back-compat — existing hand-written
profiles authored against a single-DPI setup don't suddenly resize. New rules
created via the profile editor opt in to `ScaleDpi = true`, so picker behaviour
stays consistent across mixed-DPI multi-monitor setups.
