# WindowMagnet.Core

The Win32 / DWM interop layer. No UI dependencies — pure logic and P/Invoke. Testable
headlessly (modulo the parts that actually require a desktop session — those land in
`tests/` as smoke tests or integration tests rather than pure unit tests).

## Types

| Type | Responsibility |
|---|---|
| `Native/NativeMethods.cs` | All P/Invoke wrappers in one place: `user32` (`EnumWindows`, `IsWindowVisible`, `GetWindowText`, `GetClassName`, `GetWindowRect`, `GetWindowThreadProcessId`, `SetWindowPos`, `ShowWindow`, `IsIconic`, `EnumDisplayMonitors`, `GetMonitorInfo`), `dwmapi` (`DwmRegisterThumbnail`, `DwmUpdateThumbnailProperties`, `DwmQueryThumbnailSourceSize`, `DwmUnregisterThumbnail`, `DwmGetWindowAttribute`), `shcore` (`GetDpiForMonitor`), token & SID helpers from `advapi32` (`OpenProcessToken`, `GetTokenInformation`, `GetSidSubAuthority…`). `SetLastError = true` on the calls that need it so `Marshal.GetLastWin32Error()` works on failures. |
| `WindowEnumerator.cs` | Calls `EnumWindows`, applies the DESIGN.md §3a filter set (visible, non-empty title, class not in `Progman`/`WorkerW`/`Shell_TrayWnd`/`Shell_SecondaryTrayWnd`, not cloaked, min width/height, not in the excluded-HWND or excluded-process sets, `ProcessRights.CanMoveWindow == true`). Returns a `List<WindowInfo>`. Stateless modulo the exclude sets — call as often as the refresh timer wants. |
| `WindowInfo.cs` | Two records: `WindowBounds(X, Y, Width, Height)` (a readonly struct with `Right`/`Bottom` computed props), and `WindowInfo(Handle, Title, ProcessName, ProcessId, Bounds, IsMinimized, CanMove)` with an `IsLocked` convenience (= `!CanMove`) and a `Tooltip` that appends "(requires admin to move)" when locked. |
| `ThumbnailManager.cs` | Owns a `Dictionary<source HWND → thumb HWND>` for one destination window. `Register` is idempotent — re-registering the same source returns the existing handle (matters because `EnumWindows` hands back the same HWND every tick). `UpdateRect` supports opacity, `SOURCECLIENTAREAONLY`, and an optional `RECTSOURCE` crop. `ComputeCenteredCrop(srcW, srcH, dstW, dstH)` computes an aspect-matched centre crop so DWM fills the destination instead of letterboxing — used by the WPF `ThumbnailHost`. `Reconcile(liveSources)` drops thumbnails for sources that no longer appear in the enumerator. `Dispose` unregisters everything. |
| `WindowMover.cs` | `Recall(hwnd, x, y, w, h)` (and a `WindowBounds` overload): restore-without-activate via `ShowWindow(SW_SHOWNOACTIVATE)` if iconic, then `SetWindowPos` with `SWP_NOACTIVATE | SWP_NOZORDER | SWP_SHOWWINDOW | SWP_ASYNCWINDOWPOS`. `MoveTo(hwnd, x, y)` moves without resizing — used to position the picker window itself on startup once WPF has settled its DPI-aware size. `GetBounds(hwnd)` returns the current rect in physical pixels. |
| `Monitors.cs` | Static façade over `EnumDisplayMonitors` + `GetMonitorInfo` + `GetDpiForMonitor`. Returns `MonitorInfo(Bounds, DpiScale)` records with the **primary monitor first**, then secondary monitors in `EnumDisplayMonitors` order. This gives stable 1-based indexing for the `Slot.Monitor` and `PickerWindow.Monitor` config fields. `All()` returns full bounds; `WorkAreas()` returns work areas (full minus taskbar). |
| `SlotCalculator.cs` | Translates a symbolic `Slot` (anchor + width + height + offsets) into a concrete pixel `WindowBounds` on a chosen monitor. Two overloads — one takes raw bounds + a `dpiScale`, one takes a `MonitorInfo`. Honours `Slot.ScaleDpi`: when true, scales `Width`/`Height`/`OffsetX`/`OffsetY` by `dpiScale`; when false (v0.2 back-compat), uses them as raw physical pixels. Anchor vocabulary: `top-left`/`top`/`top-center`/`top-right`/`middle-left`/`middle`/`middle-right`/`bottom-left`/`bottom`/`bottom-right`. |
| `ProcessRights.cs` | `CanMoveWindow(hwnd)` — returns true iff the target HWND's process integrity level is `≤` our own. Cross-IL `SetWindowPos` fails with ACCESS_DENIED, and silently-failed recalls are a horrible UX, so we detect it up front. Uses `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)` + `OpenProcessToken(TOKEN_QUERY)` + `GetTokenInformation(TokenIntegrityLevel)` and pulls the integrity RID off the SID's last sub-authority. Cached per-PID in a `ConcurrentDictionary` — integrity doesn't change at runtime and per-session PID reuse is rare. |

## Notes

- `WindowEnumerator` produces a fresh list on every refresh tick (every 750ms by
  default). `WindowInfo` is a `record` so equality + dedup-by-handle stay cheap; we
  haven't hit allocation pressure in practice.
- `ThumbnailManager` is the most subtle piece. Re-registering an already-registered
  source/destination pair *leaks DWM resources*; the idempotent `Register` and the
  per-tick `Reconcile` together prevent both leaks and stale thumbnails.
- Logging from `Core` deliberately doesn't exist — there's no `LiveLog` here. The
  WPF host (`WindowMagnet.App.App.Log`) writes to a file in `%APPDATA%\WindowMagnet\`
  and exposes a static method that call sites use directly. Keeping `Core` free of a
  logging dependency means the test project doesn't need a logging stub.
