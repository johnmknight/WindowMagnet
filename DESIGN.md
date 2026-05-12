# WindowMagnet — Design

The technical plan for a Teams-style window picker that pulls hidden windows out from
behind an exclusive-fullscreen game.

---

## 1. Goals & Non-Goals

### Goals

- **Live, real-time thumbnails** of every visible top-level window — the "Teams share
  picker" UX.
- **Click-to-recall**: one click moves the source window to a defined slot on monitor 2,
  resized and positioned according to a per-app profile.
- **No focus stealing.** The game keeps focus the entire time.
- **No alt-tab.** The user never has to leave the game's fullscreen context.
- **Persistent UI** on monitor 2, visible whenever the app is running.
- **Compatible with exclusive fullscreen**, including DirectX 11/12 games and HDR.

### Non-Goals

- Not a layout manager. Won't compete with FancyZones for general productivity layouts.
- Not a hotkey-driven utility. The whole point is the visual picker on monitor 2.
- Not a cursor-trap solver. Dual Monitor Tools already does that fine.
- Not a multi-monitor everything tool. Two monitors is the assumed setup; three is fine
  but not the target.
- No cross-platform aspirations. Windows only — the entire premise rests on Win32
  and DWM APIs.

---

## 2. Architecture Sketch

```
┌─────────────────── Monitor 1 (Primary) ───────────────────┐
│                                                            │
│              ┌──────────────────────────────┐              │
│              │                              │              │
│              │   FULLSCREEN GAME (HDR)      │              │
│              │                              │              │
│              │   (compositor captured)      │              │
│              │                              │              │
│              └──────────────────────────────┘              │
│                                                            │
│   [Chrome window — buried, not visible to user]            │
│   [Discord — buried]                                       │
│   [Spotify — buried]                                       │
│                                                            │
└────────────────────────────────────────────────────────────┘

┌────────────────── Monitor 2 (Secondary) ──────────────────┐
│                                                            │
│   ┌────────────────── WindowMagnet ───────────────────────┐   │
│   │                                                    │   │
│   │  ┌────────┐  ┌────────┐  ┌────────┐  ┌────────┐  │   │
│   │  │ Chrome │  │Discord │  │Spotify │  │Notepad │  │   │
│   │  │ [live] │  │ [live] │  │ [live] │  │ [live] │  │   │
│   │  └────────┘  └────────┘  └────────┘  └────────┘  │   │
│   │                                                    │   │
│   │  Click a thumbnail → window flies to slot below   │   │
│   └────────────────────────────────────────────────────┘   │
│                                                            │
│   ┌──────────────── Recalled window slot ─────────────┐   │
│   │     (where clicked windows are parked)            │   │
│   └────────────────────────────────────────────────────┘   │
│                                                            │
└────────────────────────────────────────────────────────────┘
```

### Layers

```
┌─────────────────────────────────────────────────────┐
│  UI Layer (WPF or WinUI 3)                          │
│  - Grid of thumbnail tiles                          │
│  - Click handling                                   │
│  - Profile editor                                   │
└────────────────────┬────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────┐
│  WindowMagnet.Core                                      │
│  - WindowEnumerator    (EnumWindows + filters)      │
│  - ThumbnailManager    (DWM register/update/unreg)  │
│  - WindowMover         (SetWindowPos / MoveWindow)  │
│  - ProfileResolver     (per-app rules)              │
└────────────────────┬────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────┐
│  Win32 P/Invoke surface (user32, dwmapi)            │
└─────────────────────────────────────────────────────┘
```

---

## 3. The Three Building Blocks

### 3a. Enumerating Hidden Windows — `EnumWindows`

`EnumWindows` walks every top-level window on the system. It doesn't care whether a
window is visible, focused, minimized, or buried behind a fullscreen game. Z-order is
irrelevant — you get the handles regardless.

```csharp
[DllImport("user32.dll")]
static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

[DllImport("user32.dll")]
static extern bool IsWindowVisible(IntPtr hWnd);

[DllImport("user32.dll", CharSet = CharSet.Unicode)]
static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

[DllImport("user32.dll")]
static extern int GetWindowTextLength(IntPtr hWnd);

[DllImport("user32.dll")]
static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
```

**Filtering rules** for what makes the picker:

- `IsWindowVisible(hWnd) == true`
- `GetWindowTextLength(hWnd) > 0` (skip windows with empty titles)
- Skip the game itself (configurable — match by process name or by the foreground
  fullscreen window's process)
- Skip WindowMagnet's own windows
- Skip cloaked/system windows (`DwmGetWindowAttribute` with
  `DWMWA_CLOAKED` — Windows uses cloaking for things like inactive virtual-desktop
  windows and these would be confusing in the picker)
- Optionally skip windows below a minimum size (filter out tiny tooltips, floating
  toolbars)

### 3b. Live Thumbnails — DWM Thumbnail API

This is the Microsoft Teams trick. The DWM thumbnail API lets you ask the Desktop
Window Manager to render a live composited preview of any source window into a
rectangle inside your own window. It updates in real time, runs on the compositor's
schedule, and costs almost nothing in CPU.

The relevant functions live in `dwmapi.dll`:

| Function | Purpose |
|---|---|
| `DwmRegisterThumbnail(hDest, hSource, out hThumb)` | Bind a source HWND to a destination HWND |
| `DwmUpdateThumbnailProperties(hThumb, ref props)` | Set the destination rect, opacity, visibility |
| `DwmQueryThumbnailSourceSize(hThumb, out size)` | Get the source's natural size (for aspect-correct sizing) |
| `DwmUnregisterThumbnail(hThumb)` | Tear down the relationship |

The pattern:

1. On startup, create the WindowMagnet main window on monitor 2.
2. For each source window the picker should show, call `DwmRegisterThumbnail` with
   `hDest = WindowMagnetMainWindow` and `hSource = sourceHwnd`.
3. Compute the destination rectangle for that thumbnail's slot in the grid.
4. Call `DwmUpdateThumbnailProperties` with the rect + `DWM_TNP_VISIBLE` flag.
5. The thumbnail now renders continuously, in real time, with no further code needed.

Two important properties of this API:

- **The destination HWND must be owned by the calling process.** You can't draw
  thumbnails into someone else's window. This is a security boundary.
- **The thumbnail is always rendered on top** of the destination window's normal
  content within its rectangle. Style accordingly — borders and labels go *outside*
  the thumbnail rect.

A C# wrapper would look like:

```csharp
[StructLayout(LayoutKind.Sequential)]
struct DwmThumbnailProperties
{
    public uint dwFlags;
    public Rect rcDestination;
    public Rect rcSource;
    public byte opacity;
    public bool fVisible;
    public bool fSourceClientAreaOnly;
}

const uint DWM_TNP_RECTDESTINATION    = 0x00000001;
const uint DWM_TNP_RECTSOURCE         = 0x00000002;
const uint DWM_TNP_OPACITY            = 0x00000004;
const uint DWM_TNP_VISIBLE            = 0x00000008;
const uint DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010;

[DllImport("dwmapi.dll")]
static extern int DwmRegisterThumbnail(IntPtr hDest, IntPtr hSrc, out IntPtr phThumb);

[DllImport("dwmapi.dll")]
static extern int DwmUpdateThumbnailProperties(IntPtr hThumb, ref DwmThumbnailProperties props);

[DllImport("dwmapi.dll")]
static extern int DwmUnregisterThumbnail(IntPtr hThumb);

[DllImport("dwmapi.dll")]
static extern int DwmQueryThumbnailSourceSize(IntPtr hThumb, out Size size);
```

**Why DWM and not Windows.Graphics.Capture (WGC)?**

WGC is the newer API (Windows 10 1803+) used for things like screen recording. It's
more capable — you can post-process pixels, apply filters, etc. But for a live preview
that just needs to render to a rect, DWM thumbnails are:

- Simpler (4 functions vs. a winrt interface chain)
- Lower CPU/GPU cost (the compositor does the work it was already doing)
- Lower latency (no buffer-copy round trip)
- Available since Vista

WGC is overkill here. Stick with DWM unless a need for pixel access emerges.

### 3c. Moving Windows Without Stealing Focus — `SetWindowPos`

`MoveWindow` works but reactivates the window as a side effect on some Windows versions.
`SetWindowPos` is the right call:

```csharp
[DllImport("user32.dll", SetLastError = true)]
static extern bool SetWindowPos(
    IntPtr hWnd,
    IntPtr hWndInsertAfter,
    int X, int Y,
    int cx, int cy,
    uint uFlags);

const uint SWP_NOACTIVATE    = 0x0010;
const uint SWP_NOZORDER      = 0x0004;
const uint SWP_SHOWWINDOW    = 0x0040;
const uint SWP_ASYNCWINDOWPOS = 0x4000;
```

For a recall action:

```csharp
SetWindowPos(
    sourceHwnd,
    IntPtr.Zero,
    targetX, targetY, targetWidth, targetHeight,
    SWP_NOACTIVATE | SWP_NOZORDER | SWP_SHOWWINDOW | SWP_ASYNCWINDOWPOS
);
```

`SWP_NOACTIVATE` is the key flag. It moves the window without making it the foreground
window, which means the game retains focus.

If the source window was minimized, restore it first:

```csharp
[DllImport("user32.dll")]
static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

const int SW_SHOWNOACTIVATE = 4;   // restore without activation

ShowWindow(sourceHwnd, SW_SHOWNOACTIVATE);
```

---

## 4. The UI

### 4a. Toolkit Choice

Three reasonable options:

| Toolkit | Pros | Cons |
|---|---|---|
| **WPF** | Mature, well-documented, DWM thumbnail samples exist | Older feel; XAML quirks |
| **WinUI 3** | Modern Fluent design, Mica/Acrylic, future-facing | Less mature tooling, MSIX packaging complexity |
| **WinForms** | Simple, fast prototype | Dated; harder to do nice thumbnails grids |

**Recommendation: WPF for v1**, with the option to migrate to WinUI 3 later. WPF lets
the project ship fast, has well-trodden DWM thumbnail examples (Victor Hurdugaci's
"Fancy windows previewer" article is the canonical reference), and works without
packaging hassles. The UI doesn't need to be flashy — it needs to be a grid of
thumbnails that updates in real time.

### 4b. Layout

```
┌─ WindowMagnet (always on monitor 2) ─────────────────────┐
│  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐       │
│  │      │ │      │ │      │ │      │ │      │       │
│  │ live │ │ live │ │ live │ │ live │ │ live │       │
│  │ thumb│ │ thumb│ │ thumb│ │ thumb│ │ thumb│       │
│  │      │ │      │ │      │ │      │ │      │       │
│  └──────┘ └──────┘ └──────┘ └──────┘ └──────┘       │
│  Chrome   Discord   Spotify  Notepad  Explorer       │
│                                                       │
│  [⚙ Profiles] [Refresh] [☰ Slots]                   │
└───────────────────────────────────────────────────────┘
```

Tile size: ~240×135 (16:9) by default, sized to fit 4–6 across without scrolling on a
typical 1080p second monitor.

### 4c. Click Behavior

Single click → recall the window using the matching profile (see §5).
Right click → show a context menu: pick a target slot, edit this app's profile, hide.
Middle click → minimize/hide the source window (useful for "I clicked the wrong one").

### 4d. Auto-refresh

A 500–1000ms `DispatcherTimer` re-runs `EnumWindows` and reconciles the list. Newly
discovered windows get a `DwmRegisterThumbnail`; vanished ones get an unregister.
Existing thumbnails are left alone — DWM keeps them updating on its own.

---

## 5. Profile / Config Schema

Per-app rules that determine *where* and *how big* a recalled window should land.

JSON, stored at `%APPDATA%\WindowMagnet\profiles.json`:

```json
{
  "version": 1,
  "defaultSlot": {
    "monitor": 2,
    "anchor": "top",
    "width": 1200,
    "height": 800,
    "offsetX": 0,
    "offsetY": 0
  },
  "rules": [
    {
      "match": { "processName": "chrome.exe" },
      "slot": {
        "monitor": 2,
        "anchor": "top-center",
        "width": 1400,
        "height": 900
      }
    },
    {
      "match": { "windowTitleContains": "Discord" },
      "slot": {
        "monitor": 2,
        "anchor": "top-right",
        "width": 800,
        "height": 1000
      }
    },
    {
      "match": { "processName": "spotify.exe" },
      "slot": {
        "monitor": 2,
        "anchor": "top-left",
        "width": 1000,
        "height": 700
      }
    }
  ]
}
```

`anchor` values: `top-left`, `top-center`, `top-right`, `top`, plus equivalents for
middle/bottom rows for completeness.

Resolution order:

1. First matching rule by `processName` wins.
2. Otherwise first matching rule by `windowTitleContains`.
3. Otherwise `defaultSlot`.

A profile-editor UI is optional for v1; hand-editing JSON is fine to start.

---

## 6. Known Gotchas & How to Handle Them

### 6a. The game minimizes on focus loss

Some games (older DirectX 9 titles especially) minimize when they lose foreground.
Because WindowMagnet uses `SWP_NOACTIVATE`, this shouldn't happen — the game never loses
focus when a window moves. But:

- A user *clicking on WindowMagnet's own window* does change foreground.
- If the game minimizes when WindowMagnet gets clicked, the workaround is the same one
  DisplayFusion uses: hook the foreground window before the click and restore it
  immediately after. Practically, this means:

```csharp
[DllImport("user32.dll")]
static extern IntPtr GetForegroundWindow();

[DllImport("user32.dll")]
static extern bool SetForegroundWindow(IntPtr hWnd);

// In click handler:
var gameWindow = GetForegroundWindow();
// ... do the recall ...
SetForegroundWindow(gameWindow);
```

This may or may not be needed depending on the game. Make it a config toggle.

### 6b. Exclusive fullscreen captures the GPU

True exclusive fullscreen (DXGI flip mode with exclusive scanout) hands the compositor
off to the game. While in that state:

- DWM thumbnails of windows behind the game *should* still work — DWM still owns the
  other windows even though it doesn't own the active scanout — but this is worth
  testing early. Some sources suggest exclusive fullscreen on Windows 10/11 has been
  effectively replaced by "fullscreen optimizations" (a borderless-windowed mode
  Windows applies transparently), in which case DWM is fully in control and nothing
  is special.
- If a specific game truly captures the compositor, WindowMagnet's thumbnails on monitor 2
  may freeze while the game has the GPU. The recall click still works (window
  enumeration and `SetWindowPos` are CPU-side), but live thumbnails may stop updating.
  Test, then document per-game.

### 6c. UWP / packaged apps

Modern Store apps (Calculator, Photos, Mail, etc.) sometimes have unusual window class
behavior. `EnumWindows` returns their `ApplicationFrameHost.exe` window, which is the
outer frame, not the actual app. The thumbnail still works correctly, and `SetWindowPos`
on the frame window moves the whole thing. No special handling needed in v1.

### 6d. DPI scaling and per-monitor DPI

If monitor 1 is 4K @ 150% scaling and monitor 2 is 1080p @ 100%, the coordinate math
needs care. WindowMagnet should declare itself **per-monitor DPI aware (PMv2)** in its
manifest:

```xml
<dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">
  PerMonitorV2
</dpiAwareness>
```

Then `Screen.Bounds` and `SetWindowPos` use physical pixels and the math just works.

### 6e. Negative coordinates

A monitor positioned left of or above the primary monitor has negative X or Y bounds
in the virtual screen. `SetWindowPos` accepts negative coordinates fine; the only risk
is hardcoding `0,0` as if it were always the top-left of monitor 1. Always derive slot
coordinates from `Screen.AllScreens[targetMonitorIndex].Bounds`.

### 6f. The destination window owns the thumbnail

Per DWM API rules, `DwmRegisterThumbnail` requires the destination HWND to be owned by
the calling process. WindowMagnet's main window is fine. Just don't try to render thumbnails
into shared/system windows — that errors out.

### 6g. Maximum reasonable thumbnail count

DWM thumbnails are cheap but not free. A dozen on screen is fine. Fifty might start to
strain things. If a user has 100 windows open (browsers with many windows, dev IDEs,
etc.), the picker needs filtering — by process, by recent use, by title search — to
keep the grid manageable. Plan for filter UI in v1.1.

---

## 7. Build & Distribution

- .NET 8 (or .NET 9 — whichever is current LTS at build time)
- WPF project type
- Visual Studio 2022+ or `dotnet` CLI
- Single-file publish for distribution; no installer needed for v1
- Run at user login via a registry `Run` key or a startup-folder shortcut
- No admin elevation required for normal operation. (Admin is only needed if WindowMagnet
  has to control elevated windows like Task Manager — same caveat as FancyZones.)

---

## 8. Milestones

### v0.1 — Proof of Concept (1 evening)

- Enumerate windows
- Show a single DWM thumbnail in a WPF window
- Verify it updates live with the source

### v0.2 — Grid (1 weekend)

- Grid of N thumbnails auto-sized to the window
- Click handler that moves the source to a hardcoded slot on monitor 2

### v0.3 — Profiles (1 weekend)

- JSON profile loading
- Per-app slot resolution
- A "default slot" for unmatched apps

### v0.4 — Polish (ongoing)

- Filter / search box
- Profile editor UI
- Tray icon and startup integration
- Per-monitor DPI testing
- Game-compatibility documentation per title

### v1.0 — Release

- Tested against 3–5 popular fullscreen-HDR titles
- README with screenshots
- MIT license, posted to GitHub

---

## 9. Reference Links

- DWM Thumbnail Overview: https://learn.microsoft.com/en-us/windows/win32/dwm/thumbnail-ovw
- `DwmRegisterThumbnail`: https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmregisterthumbnail
- `SetWindowPos`: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos
- `EnumWindows`: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumwindows
- Victor Hurdugaci, "Fancy windows previewer" — the canonical WPF DWM-thumbnail walkthrough
- AutoHotkey `LiveThumb` class — a working DWM thumbnail implementation in script form,
  useful as a sanity-check reference
- DisplayFusion forum thread on global hotkeys with fullscreen games — useful for the
  edge cases around game focus behavior
