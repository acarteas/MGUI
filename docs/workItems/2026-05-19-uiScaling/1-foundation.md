# Increment 1 Implementation Plan: UI Scale Foundation

## Summary
Add the shared scaling infrastructure without converting layout/text consumers yet. This increment creates the public scale settings API, centralizes scale math and rounding, wires `MGDesktop` to react to scale changes, and adds unit coverage. At the end of this increment, setting `Desktop.UIScale` will invalidate layouts, but visible scaling behavior is intentionally deferred to later increments.

## Public API And Types
- Add `MGScaleSettings` in `MGUI.Core.UI`.
  - Derive from `ViewModelBase`.
  - Properties: `FontScale`, `SpacingScale`, `SizeScale`, `BorderScale`, `ImageScale`, all `float`, default `1.0f`.
  - Method: `SetUniformScale(float scale)` updates all five category scales.
  - Event: `ScaleChanged`, raised once per property change and once per `SetUniformScale` call if any value changed.
  - Validation: reject `NaN`, infinity, and values `<= 0.0f` with `ArgumentOutOfRangeException`.
- Add `MGScaleCategory` enum.
  - Values: `None`, `Font`, `Spacing`, `Size`, `Border`, `Image`.
- Add scale helper methods on `MGScaleSettings`.
  - `GetScale(MGScaleCategory category)`.
  - `ScaleInt(int value, MGScaleCategory category)`.
  - `ScaleNullableInt(int? value, MGScaleCategory category)`.
  - `ScaleFloat(float value, MGScaleCategory category)`.
  - `ScalePoint(Point value, MGScaleCategory category)`.
  - `ScaleSize(Size value, MGScaleCategory category)`.
  - `ScaleThickness(Thickness value, MGScaleCategory category)`.
- Add `MGDesktop.UIScale`.
  - Default: new `MGScaleSettings()`.
  - Setter rejects null, unsubscribes from the previous instance, subscribes to the new instance, raises `NPC(nameof(UIScale))`, and invalidates layouts.
  - Existing `MGWindow.Scale` remains unchanged.

## Scale Math Decisions
- `MGScaleCategory.None` always returns the original value.
- Integer-like scaling uses `Math.Round(value * scale, MidpointRounding.AwayFromZero)`.
- Scaling always computes from the caller-provided raw value; no helper stores or reuses scaled results.
- Preserve sign for negative values.
- For `Border` and `Image` categories only, positive nonzero values that would round to `0` become `1`; negative nonzero values that would round to `0` become `-1`.
- For `Font`, `Spacing`, and `Size`, allow small positive values to round to `0`; later increments can choose category-specific minimums for individual properties if needed.

## Desktop Integration
- Initialize `UIScale` in the `MGDesktop` constructor before windows or overlay elements need scale access.
- On any `MGScaleSettings.ScaleChanged`, call `InvalidateAllLayouts()`.
- Also refresh text layout caches by traversing all windows and calling `MGTextBlock.RefreshTextEngine()`; this prepares for Increment 2 and keeps the invalidation contract correct once text begins consuming `FontScale`.
- Include `OverlayWindow` in invalidation/refresh handling where possible, because it is constructed outside the public `Windows` list.
- Do not introduce per-window or per-element overrides in this increment.

## Tests
- Add xUnit tests for `MGScaleSettings`.
  - Defaults are all `1.0f`.
  - Invalid scale values throw.
  - `SetUniformScale(1.5f)` updates all categories and raises one scale-changed event.
  - Setting one category raises `PropertyChanged` for that property and one scale-changed event.
  - Setting a property to its current value raises no events.
  - `GetScale(None)` returns `1.0f`.
- Add scale math tests.
  - `10` at `1.5` becomes `15`.
  - `5` at `1.25` becomes `6` with away-from-zero rounding.
  - `-5` at `1.25` becomes `-6`.
  - positive `Border` and `Image` values preserve nonzero minimums.
  - `Spacing` and `Size` do not force minimums.
  - `Point`, `Size`, and `Thickness` scale every component consistently.
- Add desktop integration tests if a lightweight `MGDesktop` fixture is already practical.
  - Replacing `UIScale` rejects null.
  - Changing `UIScale` invalidates existing layouts.
  - Replacing `UIScale` unsubscribes from the old settings object.
- If a desktop fixture is too heavy for this increment, keep desktop behavior covered by a narrow test helper or defer only the `MGDesktop` tests, but still implement the behavior.

## Implementation Notes
- Follow existing repo style: curly braces on all `if` statements, and blank lines before whole-line comments.
- Keep helper methods in one central type; do not spread ad hoc scale multiplication into elements yet.
- Do not change XAML parsing, layout measurement, `MGTextBlock.FontSize`, or any control-specific property usage in this increment.
- Build and run `dotnet test MGUI.Tests/MGUI.Tests.csproj` after implementation.

## Assumptions
- `UIScale = 1.0` must be behaviorally identical to current MGUI.
- Scale settings are mutable and observable, matching existing `ViewModelBase` patterns.
- This increment is infrastructure-only; user-visible scaling starts in Increment 2.
