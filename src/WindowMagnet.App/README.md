# WindowMagnet.App

The WPF host application — the user-facing picker window that lives on monitor 2.

## What goes here

- `App.xaml` / `App.xaml.cs` — application bootstrap, single-instance check, startup integration
- `MainWindow.xaml` — the picker grid, toolbar, recall-slot indicator
- `Views/` — additional windows (profile editor, settings, tray icon menu)
- `ViewModels/` — MVVM glue between the UI and `WindowMagnet.Core`
- `Controls/ThumbnailHost.xaml.cs` — a `HwndHost`-derived control that owns the rectangle
  where a single DWM thumbnail is rendered
- `app.manifest` — declares per-monitor DPI awareness (PMv2). Already present.

## Bootstrap notes

This project doesn't have any source files yet beyond the `.csproj` and `app.manifest`.
Once the design is locked in, scaffold the WPF entry points either with Visual Studio's
WPF App template or via `dotnet new wpf` (delete the generated `.csproj` and merge any
needed bits with the one already here).

The picker window should be positioned on monitor 2 at startup and remember its position
across runs. See `WindowMagnet.Core/WindowEnumerator` for how to enumerate monitors.

## DWM thumbnail hosting in WPF

A WPF `Image` element cannot host a DWM thumbnail directly — DWM needs a real `HWND`.
The canonical pattern is a custom `HwndHost`-derived control that creates a child HWND,
calls `DwmRegisterThumbnail` to bind a source window into it, and updates the thumbnail
rect on `OnRenderSizeChanged`. Victor Hurdugaci's "Fancy windows previewer" walkthrough
(linked in `DESIGN.md` §9) is the canonical reference.
