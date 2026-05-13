# WindowMagnet

**A second-screen window picker for gamers running exclusive fullscreen.**

When a game is running fullscreen on your primary monitor — especially one that requires
exclusive fullscreen for HDR or peak performance — every other window on that screen is
buried. Alt-Tab works, but it's slow, it can glitch the game's resolution on the way back,
and some games refuse to honor it cleanly.

WindowMagnet lives on your second monitor. It shows a Microsoft Teams-style grid of live
thumbnails for every visible window currently stuck behind the game. Click a thumbnail,
and that window is pulled — magnet-style — onto monitor 2: resized to a sensible width,
parked at the top of the screen, ready to use.

The game keeps running, undisturbed, on monitor 1.

---

## The Problem in One Sentence

Borderless windowed mode is the standard answer to "how do I use my second monitor while
gaming," but many modern games only support HDR — and some performance features — in
exclusive fullscreen, which captures the compositor and buries everything else.

## The Approach

WindowMagnet is *not* a replacement for FancyZones, DisplayFusion, or PowerToys. It does one
specific thing those tools don't:

- It runs persistently on monitor 2 (the non-game monitor).
- It enumerates other top-level windows continuously.
- It renders **live DWM thumbnails** of those windows in a grid (the Teams approach).
- A click on a thumbnail moves and resizes the source window to a chosen slot on monitor 2.

The user never has to focus or alt-tab to the buried window. The interaction happens
entirely on monitor 2, on a UI that's always visible because the game doesn't own that
screen.

## Why It Works When Other Tools Don't

| Tool | Works with exclusive fullscreen? | Live thumbnails of hidden windows? | UI on monitor 2? |
|---|---|---|---|
| PowerToys FancyZones | Hotkeys are focus-dependent | No | No |
| DisplayFusion | Focus-dependent; needs "Prevent Deactivation" workaround | No | TitleBar buttons only |
| Dual Monitor Tools | Solves cursor trap, not window recall | No | No |
| Windows native `Win+Shift+←/→` | Focus-dependent | No | No |
| **WindowMagnet** | **Yes — no focus needed** | **Yes (DWM)** | **Yes** |

The trick is that `SetWindowPos` works on a window handle regardless of focus or Z-order.
You don't need to bring a window to the front to move it — you just need its `HWND`.
`EnumWindows` gives you every HWND on the system. DWM thumbnails let you preview them
without disturbing the source. Combine those three and the problem is solved.

## Status

Working WPF app — stamped `ver=0.3` in [`App.xaml.cs`](./src/WindowMagnet.App/App.xaml.cs).

What's done:

- **Tray-resident picker** on monitor 2, summoned with ``Win+` `` or a left-click on
  the tray icon, hidden again with the same.
- **Live DWM thumbnails** of every visible top-level window, refreshed every 750ms
  via a reconciling timer (existing thumbnails are left alone — DWM keeps them
  updating; new/vanished sources are registered/unregistered).
- **Per-app profile rules** loaded from `%APPDATA%\WindowMagnet\profiles.json`.
  Match by process name or window title; falls through to a configurable default
  slot. Click a tile → window flies (no focus stolen) to the resolved slot.
- **Profile editor UI** built in — full `ProfileDialog` plus a per-rule
  `RuleEditDialog`. Right-click a tile → "Add rule for chrome.exe…" pre-fills
  the new-rule form with that window's process name.
- **Integrity-level awareness** — tiles for higher-IL windows (e.g. an elevated
  Task Manager) are dimmed at 31% opacity and the click handler reports
  "needs admin to move" instead of failing silently with a Win32 ACCESS_DENIED.
- **Per-monitor DPI (PMv2)** — `Monitors.WorkAreas()` reports per-monitor DPI
  scale; `SlotCalculator` honours a `ScaleDpi` flag on each `Slot` so the same
  profile lays out correctly across mixed-DPI displays.
- **Run at login** — toggleable from the tray menu (per-user `HKCU\…\Run`,
  no elevation required).

What's still ahead (per [`DESIGN.md`](./DESIGN.md) §8 v1.0):

- Per-game compatibility testing against real exclusive-fullscreen / HDR titles,
  documented per title.
- README screenshots.
- License decision and a tagged release.

See [`DESIGN.md`](./DESIGN.md) for the technical plan and [`PRIOR_ART.md`](./PRIOR_ART.md)
for the landscape of existing tools and why none of them quite fit.

A clickable HTML mockup of the original UI design lives in
[`mockup/index.html`](./mockup/index.html). The shipping app's main window closely
follows it — useful as a reference when reading the WPF XAML.

## Repo Layout

```
WindowMagnet/
├── README.md                  ← this file
├── DESIGN.md                  ← architecture, APIs, gotchas
├── PRIOR_ART.md               ← what already exists and where it falls short
├── WindowMagnet.sln           ← solution file
├── .gitignore
├── .editorconfig
├── src/
│   ├── WindowMagnet.App/      ← WPF host app (the picker window)
│   ├── WindowMagnet.Core/     ← window enumeration, Win32 wrapper, DWM thumbnails
│   └── WindowMagnet.Config/   ← profile / per-app rule schema
├── tests/
├── mockup/                    ← clickable HTML mockup of the planned UI
├── docs/                      ← screenshots, diagrams, supplementary notes
└── assets/                    ← icons, branding
```

## License

TBD — likely MIT, consistent with John's other open-source projects.
