# WindowMagnet.Config

Profile schema, JSON load/save, and per-app rule resolution.

Target framework is plain `net8.0` (no `-windows` suffix) — this project has no Win32
dependencies and stays portable for unit testing.

## Planned types

| Type | Responsibility |
|---|---|
| `Profile.cs` | Top-level config record: `Version`, `DefaultSlot`, `Rules` |
| `Slot.cs` | `Monitor`, `Anchor`, `Width`, `Height`, `OffsetX`, `OffsetY` |
| `Rule.cs` | `Match` (process name OR window title contains) → `Slot` |
| `ProfileResolver.cs` | Given a `WindowInfo`, returns the best-matching `Slot` per the resolution order in `DESIGN.md` §5 |
| `ProfileStore.cs` | Load/save `%APPDATA%\WindowMagnet\profiles.json` |

## Schema

See `DESIGN.md` §5 for the canonical JSON example. The TL;DR:

1. First `Rule` whose `match.processName` matches wins.
2. Otherwise first `Rule` whose `match.windowTitleContains` matches wins.
3. Otherwise the `defaultSlot` applies.

Anchors are symbolic (`top-left`, `top-center`, etc.) so config stays portable across
different monitor resolutions. `ProfileResolver` translates anchor + width/height into
concrete pixel coordinates using the target monitor's bounds.
