# Prior Art

What already exists in the window-management / multi-monitor space, what it does well,
and where the gap is.

## PowerToys FancyZones

Microsoft's official window layout manager, free and open source.

**What it does well:** Custom zone layouts per monitor, drag-and-snap with `Shift`,
templates for common arrangements, FancyZonesCLI for scripted layout switching.

**Why it doesn't solve this problem:**

- Layout-driven, not window-recall driven. It defines *where* a window should land
  when dragged, not *which* windows to grab.
- All movement is keyboard-shortcut or drag based, which means the target window
  needs focus. A window buried behind an exclusive-fullscreen game cannot be focused
  without alt-tabbing the game.
- There's a long-standing PowerToys feature request from December 2021 for hotkey-based
  movement of an *arbitrary* window to a specific monitor. It hasn't shipped.
- No live thumbnail preview of hidden windows.

**Reference:** https://learn.microsoft.com/en-us/windows/powertoys/fancyzones

## DisplayFusion (Binary Fortress, paid ~$30)

The most feature-complete multi-monitor utility on Windows.

**What it does well:** Custom functions with hotkeys, "Move Window to Monitor #X" with
optional resize-to-percentage, TitleBar buttons, scripted functions written in a C#-like
DSL, monitor splitters (virtual sub-monitors).

**Why it doesn't solve this problem:**

- Same focus dependency. The user community has had to invent workarounds like the
  `Prevent Window Deactivation` scripted function to make hotkeys fire while a game
  is focused, and even then some games block global hotkeys entirely.
- TitleBar buttons can't be clicked when the title bar is hidden behind a fullscreen
  game.
- No persistent UI panel on the second monitor — actions are triggered from hotkeys
  or from the title bar of the target window.
- Closed source, paid.

**Reference threads on DisplayFusion forums:**
- "Move Window to Different Monitor" function discussion
- "Make Move window to next monitor Work with Fullscreen(Window)" — documents the
  global-hotkey-while-game-focused problem and a partial scripted workaround

## Dual Monitor Tools (free, SourceForge)

Open source, ancient, still works.

**What it does well:** Solves the cursor-trap problem — locks mouse to one monitor with
a hotkey toggle. Good for the inverse case (you're playing a game and don't want your
mouse to drift off-screen).

**Why it doesn't solve this problem:** It doesn't move or resize windows. It only
manages the cursor.

## Borderless Gaming / Borderless Window Mode

In-game setting or third-party wrapper that forces a game into a borderless window
covering the full screen.

**What it does well:** The standard answer to "how do I use a second monitor while
gaming." With borderless windowed, the second monitor stays fully usable.

**Why it doesn't solve this problem:**

- Many modern games only support HDR in true exclusive fullscreen.
- Some games get better performance in exclusive fullscreen.
- Some games (older titles, certain engines) don't expose a borderless option at all.
- You lose variable refresh rate quirks that work best in exclusive mode.

This is the most common workaround, and it's good enough for many people. WindowMagnet
exists for the cases where it isn't.

## NirCmd / AutoHotkey

Building-block utilities. `nircmd win move title "..." x y` works. AutoHotkey can do
nearly anything window-related.

**Why they don't solve this problem:** They're scripting tools, not applications. You
could build WindowMagnet in AutoHotkey, but you'd be building WindowMagnet. The
live-thumbnail-grid UI is the entire reason this project exists.

## Microsoft Teams Window/Screen Picker (the UX inspiration)

When you click "Share" in a Teams meeting, Teams shows a panel of live thumbnails — one
per visible window, plus one per monitor. The thumbnails update in real time. You click
the one you want to share.

**Why it's the right reference:**

- Live thumbnails make identification trivial — you don't have to read window titles
  and guess which "Untitled - Notepad" is which.
- The thumbnails update without disturbing the source windows.
- The picker is a deliberate, modal interaction — exactly the right metaphor for
  "show me what's hidden, let me pick one."
- It's a familiar pattern. Any Teams user already knows how to use it.

Teams generates those thumbnails using the **Desktop Window Manager (DWM) thumbnail
API** — `DwmRegisterThumbnail` and `DwmUpdateThumbnailProperties`. This API has been
in Windows since Vista and is exactly the right tool for WindowMagnet's grid.

**References:**
- https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmregisterthumbnail
- https://learn.microsoft.com/en-us/windows/win32/dwm/thumbnail-ovw

## The Gap WindowMagnet Fills

No existing tool combines:

1. A **persistent UI panel** living on the non-game monitor,
2. **Live thumbnails** of windows currently buried behind a fullscreen game,
3. **Click-to-recall** that moves and resizes the source window without requiring focus,
4. **Per-app profiles** for "when I recall Chrome, park it 1200px wide at the top of
   screen 2," and
5. **Compatibility with exclusive fullscreen** (no focus stealing, no
   compositor disruption).

That's the niche.
