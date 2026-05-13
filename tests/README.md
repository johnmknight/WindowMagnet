# WindowMagnet — tests

A single xUnit project — `WindowMagnet.Tests` — covers the `Core` and `Config`
projects together. Targets `net8.0-windows` because some scenarios touch
`PresentationCore` / WPF types; that hasn't been a problem on CI in practice.

Current test files:

- **`SlotCalculatorTests.cs`** — anchor → pixel-coordinate translation against
  synthetic `WindowBounds` monitor layouts. Covers each anchor name, offsets,
  the `ScaleDpi` flag interaction, and negative-X/-Y monitors positioned left of
  or above primary.
- **`ProfileStoreTests.cs`** — JSON round-trip via `System.Text.Json`. Verifies
  camelCase property naming, comment + trailing-comma tolerance, and that a
  malformed file returns a fresh default `Profile` instead of throwing.
- **`ProfileResolverTests.cs`** — matching order from DESIGN.md §5: processName
  match wins, then `WindowTitleContains` (case-insensitive substring), then
  `DefaultSlot`. Empty / null processName + title cases.
- **`PickerWindowTests.cs`** — exercises the `Profile.PickerWindow` placement
  record + `SlotCalculator` math for picker positioning (anchor + offset +
  ScaleDpi handling for the picker itself).

The Win32 P/Invoke layer can't be meaningfully unit-tested — verifying that
`DwmRegisterThumbnail` actually paints pixels or that `SetWindowPos` actually
moves a window needs a live desktop session. That coverage comes from running
the app itself; the test project deliberately doesn't try to fake out `user32`
or `dwmapi`.

If we ever add fakes, NSubstitute is the planned tool. The Win32 surface is
small enough that hand-rolled interface fakes have stayed clean so far.
