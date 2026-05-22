# Scale NineSliceFillBrush Target Margins

## Summary
Update `MGNineSliceFillBrush` so its destination `TargetMargin` scales with the owning element’s effective UI scale, using `MGScaleCategory.Border`. This makes 9-slice corner and edge thickness grow with UI scaling while keeping `SourceMargin` as raw texture pixels.

## Key Changes
- In `MGNineSliceFillBrush.Draw`, compute an effective target margin with:
  `Element.EffectiveScaleSettings.ScaleThickness(TargetMargin, MGScaleCategory.Border)`.
- Use the effective margin for destination rectangles instead of the raw `TargetMargin`.
- Leave `SourceMargin` unchanged because it defines source texture slicing, not rendered UI size.
- Do not add a new XAML property or opt-in flag; existing `<NineSliceFillBrush TargetMargin="...">` values now scale automatically.
- Update XML docs/comments for `TargetMargin` to clarify that it is an unscaled UI value rendered through border UI scaling.

## Tests
- Add or extend unit coverage for `MGNineSliceFillBrush` behavior by exposing a small internal helper or otherwise testable path that computes the effective target margin.
- Verify:
  - `TargetMargin=26` with `BorderScale=1.5` becomes `39`.
  - asymmetric margins scale side-by-side correctly.
  - `UIScaleMode="Disabled"` on the element uses unscaled margins.
  - `SourceMargin` remains unaffected by UI scale.
- Run `dotnet test` for the solution/test project.

## Assumptions
- `MGScaleCategory.Border` is the intended category because 9-slice target margins behave like visual border/corner thickness.
- Backward compatibility accepts the visual change for existing nine-slice brushes under non-1x UI scaling.
- No sample XAML changes are required; the existing `IFillBrush.xaml` example should demonstrate the corrected behavior automatically.
