# WindowMagnet — tests

Test projects go here. Planned:

- `WindowMagnet.Core.Tests` — unit tests for `WindowEnumerator` filters, `ThumbnailManager`
  reconciliation logic (with a fake `IDwmThumbnailApi`), `WindowMover` coordinate math.
- `WindowMagnet.Config.Tests` — JSON round-trip, `ProfileResolver` matching order,
  anchor → pixel-coordinate translation against synthetic monitor layouts.

Likely xUnit + FluentAssertions. Mocking via NSubstitute or just plain interface fakes —
the Win32 layer is small enough that hand-rolled fakes stay clean.

The Win32 P/Invoke layer itself is best tested by a small console "smoke test" program
that runs against the real desktop — automated tests can't substitute for verifying that
`DwmRegisterThumbnail` actually paints pixels.
