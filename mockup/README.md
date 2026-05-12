# WindowMagnet — UI Mockup

A self-contained clickable mockup of the planned WindowMagnet picker UI.

Open [`index.html`](./index.html) in any browser. No build, no dependencies, no
network calls.

## What it demonstrates

- The grid of live-looking thumbnails (Chrome, Discord, Spotify, VS Code, Notepad, Slack)
- Click-to-recall behavior — clicking a tile "parks" that window in the slot below and
  marks the tile as recalled
- A status line that logs the `SetWindowPos(..., SWP_NOACTIVATE)` call that would happen
  in the real app
- The "monitor 1" context strip at the top, with a fake fps counter to suggest the game
  is still running undisturbed while the user works on monitor 2
- A `Reset` button to clear the recalled state and play again

## What it doesn't do

- No actual window enumeration — the six thumbnails are hand-drawn HTML stand-ins
- No actual DWM compositing — the "live" green dots are decorative
- No actual `SetWindowPos` — the status line shows what the call *would* look like

The point is to lock in the visual language and interaction model before writing C#.
Once the live-thumbnail v0.1 proof-of-concept (per `DESIGN.md` §8) is working, the
real picker should resemble this layout closely.

## Notes for the WPF port

When translating to WPF/XAML:

- The dark panel background (`#0e1116`) is intentional — it makes white-app thumbnails
  pop and reads as "system tool, not productivity app."
- The 16:9 thumbnails with a 6px label below match the `~240×135 + chrome` figure in
  `DESIGN.md` §4b.
- The recall slot should be a real `HwndHost` region in the actual app — when a window
  is recalled, it physically moves to that screen position; the slot in the picker is
  just a *visual indicator* of "your recalled window is over there."
- The accent color `#4c8cff` is the only saturated hue in the whole UI. Keep it that way
  — borrowed-from-VS-Code restraint.
