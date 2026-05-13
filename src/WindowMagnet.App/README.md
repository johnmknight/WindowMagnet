# WindowMagnet.App

The WPF host application — the tray-resident picker window that lives on monitor 2.

Targets `net8.0-windows` with WPF + WinForms enabled (WinForms is here for
`NotifyIcon` / `ContextMenuStrip`; everything user-facing is WPF). `app.manifest`
declares **per-monitor DPI awareness (PMv2)**.

## What's here

| File | Responsibility |
|---|---|
| `App.xaml` / `App.xaml.cs` | Application bootstrap. Creates `%APPDATA%\WindowMagnet\` for `profiles.json` + `windowmagnet.log`. Wires global exception handlers so a thrown exception lands in the log instead of disappearing. Stamps the log with `ver=0.3` on startup. Owns the singleton `TrayController` and runs `ShutdownMode.OnExplicitShutdown` so the picker's X button hides to tray instead of exiting. Provides the static `App.Log` everything else uses for diagnostics. |
| `MainWindow.xaml` / `MainWindow.xaml.cs` | The picker. `ItemsControl` bound to `ObservableCollection<WindowInfo>` renders tiles; a 750ms `DispatcherTimer` reconciles against the enumerator. Excludes its own HWND from enumeration in `OnSourceInitialized`. Positions itself per `Profile.PickerWindow` via `WindowMover.MoveTo` on `ContentRendered` (NOT `Loaded` — `SizeToContent` and the initial WM_DPICHANGED haven't settled at `Loaded`, and a manual position gets clobbered by re-layout). Hosts the filter box, refresh button, profile button, and the `ReportStatus` line that doubles as the window-count label. Right-click context menu offers "Recall" and "Add rule for *processName*" — the latter opens `RuleEditDialog` pre-filled with that window's process. |
| `Controls/ThumbnailHost.cs` | A `FrameworkElement` (not an `HwndHost` — we don't need a child HWND; we register against the parent window's HWND and place the rect manually). Dependency properties: `SourceHandle` (the source HWND) and `IsLocked` (greys the tile via DWM opacity, since DWM composites ABOVE WPF and a parent's Opacity won't reach the thumbnail). `UpdateRect` uses `PointToScreen` to compute the destination rect in physical pixels (the correct unit at any DPI) and asks `ThumbnailManager.ComputeCenteredCrop` for an aspect-matched source crop so tiles fill instead of letterboxing. |
| `Dialogs/ProfileDialog.xaml(.cs)` | The profile editor. Lists rules + the default slot + the picker-window placement. Add / edit / delete / reorder rules. On Save writes to disk via `ProfileStore.Save` and `MainWindow` reloads the resolver. |
| `Dialogs/RuleEditDialog.xaml(.cs)` | Per-rule editor: match by process name OR title contains, choose monitor + anchor + size + offsets + `ScaleDpi`. Used both from `ProfileDialog` and inline from the tile right-click menu. |
| `Tray/TrayController.cs` | The tray icon + context menu. Paints a 32×32 brand icon (rounded blue square + white "W" matching the accent colour) in-process — no external `.ico` file. Menu items: Show / Hide picker, Edit profile, Start with Windows (checkbox synced to the registry), Quit. Owns the `HotkeyManager` for Win+\` summon. Left-click on the tray icon toggles. |
| `Tray/HotkeyManager.cs` | `RegisterHotKey` wrapper. Hooks `WM_HOTKEY` via `HwndSource.AddHook` and raises a managed `Pressed` event. Registration is best-effort — if Win+\` is already taken by another app, registration silently fails, gets logged, and the rest of the app keeps working. |
| `Tray/StartupRegistration.cs` | Reads / writes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` with WindowMagnet's exe path (quoted). Per-user only, so no UAC. The tray's "Start with Windows" checkbox calls `IsEnabled()` on menu-opening and `SetEnabled(bool)` on click. |
| `app.manifest` | PMv2 DPI awareness, `requestedExecutionLevel level="asInvoker"` (no admin), `compatibility` block for Windows 10/11. |

## DWM thumbnail hosting in WPF

Two non-obvious things `ThumbnailHost` gets right:

1. **DWM destination must be a real HWND owned by the calling process.** We use the
   parent `Window`'s HWND (via `WindowInteropHelper`), not a child `HwndHost`. The
   thumbnail's destination rect is computed *inside* that parent HWND's client area
   in physical pixels — `PointToScreen` of the control's top-left + bottom-right
   minus `PointToScreen` of the window's origin.
2. **DWM composites ABOVE WPF.** Setting `Opacity` on the containing `Button` does
   not dim the thumbnail. To grey-out a locked tile we instead pass a low DWM
   opacity (byte ~80, about 31%) into `DwmUpdateThumbnailProperties`. The `IsLocked`
   dependency property triggers a re-`UpdateRect` so the opacity refresh is cheap.

Victor Hurdugaci's "Fancy windows previewer" walkthrough (linked in `DESIGN.md` §9)
remains the best Win32-Native reference; our implementation deviates by living
inside a parent HWND rather than allocating one per tile.
