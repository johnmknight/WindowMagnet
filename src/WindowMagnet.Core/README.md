# WindowMagnet.Core

The Win32 / DWM interop layer. No UI dependencies — pure logic and P/Invoke. Should be
testable headlessly (modulo the parts that actually require a desktop session).

## Planned types

| Type | Responsibility |
|---|---|
| `Native/User32.cs` | P/Invoke wrappers for `EnumWindows`, `IsWindowVisible`, `GetWindowText`, `GetWindowThreadProcessId`, `SetWindowPos`, `ShowWindow`, `GetForegroundWindow`, `SetForegroundWindow` |
| `Native/Dwmapi.cs` | P/Invoke wrappers for `DwmRegisterThumbnail`, `DwmUpdateThumbnailProperties`, `DwmQueryThumbnailSourceSize`, `DwmUnregisterThumbnail`, `DwmGetWindowAttribute` |
| `WindowEnumerator.cs` | Calls `EnumWindows`, applies filters from `DESIGN.md` §3a, returns a list of `WindowInfo` |
| `WindowInfo.cs` | Record: `HWND`, title, process name, process ID, bounds, monitor index |
| `ThumbnailManager.cs` | Owns the `(sourceHwnd → thumbnailHandle)` map. Registers, updates rects, unregisters. Reconciles against the enumerator on each refresh tick. |
| `WindowMover.cs` | Wraps `SetWindowPos` with `SWP_NOACTIVATE`. Restores minimized windows with `SW_SHOWNOACTIVATE`. |
| `MonitorEnumerator.cs` | Wraps `Screen.AllScreens` (or `EnumDisplayMonitors`) for slot coordinate math. Handles negative coordinates and per-monitor DPI. |
| `Diagnostics/LiveLog.cs` | Cheap in-memory ring buffer for "what just happened" diagnostics. Useful when a recall misfires. |

## Notes

- All P/Invoke signatures should set `SetLastError = true` where the Win32 docs say it
  matters, so we can call `Marshal.GetLastWin32Error()` on failure paths.
- `WindowInfo` should be a value type or record — these are produced on every refresh
  tick (every 500–1000ms) and we don't want allocation churn.
- `ThumbnailManager` is the most subtle piece. Re-registering an already-registered
  source/destination pair leaks DWM resources; reconciliation must be careful.
