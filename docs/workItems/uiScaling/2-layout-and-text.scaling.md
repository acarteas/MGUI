# Increment 2 Implementation Plan: Base Layout And Text Scaling

## Summary
Make the Increment 1 `MGDesktop.UIScale` infrastructure affect base element layout and text. Existing public properties remain authored/unscaled values; layout, measurement, wrapping, and rendering use new internal effective values. This increment covers `MGElement`, `MGTextBlock`, and text caret/render metadata, but leaves borders, containers, control chrome, inline images, and per-element opt-outs for later increments.

## Key Behavior
- Keep public getters unchanged: `Margin`, `Padding`, `PreferredWidth`, `FontSize`, `LinePadding`, etc. continue returning authored values.
- Add internal/protected effective values for scaled layout/text use.
- At `UIScale = 1.0`, behavior must match current MGUI.
- Scale categories for this increment:
  - `Margin`, `Padding`, `BackgroundRenderPadding`: `Spacing`.
  - `MinWidth`, `MaxWidth`, `PreferredWidth`, height equivalents: `Size`.
  - `FontSize`, `LinePadding`, text shadow offsets: `Font`.
- Do not scale `BorderThickness`, stack/grid spacing, control-specific dimensions, inline image sizes, formatted text background padding, or underline metrics yet.

## Implementation Changes
- Harden `MGScaleSettings.ScaleInt` before broader use:
  - Use `double` math internally.
  - Clamp overflow to `int.MinValue` / `int.MaxValue`.
  - Preserve the existing nonzero minimum behavior for `Border` and `Image`.
  - Add tests for very large values.

- In `MGElement`, add `protected internal` effective helpers:
  - `EffectiveMargin`, `EffectivePadding`, `EffectiveBackgroundRenderPadding`.
  - Effective width/height properties for min, max, preferred, and preferred-including-margin.
  - Effective aggregate helpers: horizontal/vertical margin, padding, margin+padding, size variants, min/max including margin.
  - Keep existing public aggregate properties authored/unscaled.

- Update `MGElement` internals to use effective layout values:
  - `GetBackgroundBounds` compresses by `EffectiveBackgroundRenderPadding`.
  - `UpdateMeasurement` clamps using effective preferred/min/max values.
  - `MeasureSelf` adds/subtracts effective margin and padding.
  - Component measurement paths that subtract owner padding use effective padding.
  - `UpdateLayout` uses effective margin/max values when consuming bounds, aligning stretched content, and calculating `LayoutBounds`.

- In `MGTextBlock`, add effective text helpers:
  - `EffectiveFontSize`: scale `FontSize` with `Font`; preserve positive font sizes at minimum `1`.
  - `EffectiveLinePadding`: scale `LinePadding` with `Font`.
  - Effective padding helpers should reuse `MGElement` effective padding.
  - Effective text shadow offset helper scales default and inline shadow offsets with `Font`.

- Update `MGTextBlock` font resolution:
  - `FontSize` remains authored.
  - `TrySetFont` stores authored `FontSize`, but resolves fonts with `EffectiveFontSize`.
  - `RefreshTextEngine` re-resolves using `EffectiveFontSize`.
  - `SpaceWidth` and all `ResolvedFont` fields reflect the effective size.
  - `NPC(nameof(FontSize))` fires only when authored size changes, not when scale changes.

- Update `MGTextBlock` measurement/rendering:
  - `UpdateLines` wraps with effective padding.
  - `MeasureSelfOverride` uses effective padding, effective preferred size, effective line padding, and effective font metrics.
  - `DrawSelf` positions text using effective padding and advances lines using effective line padding.
  - Text shadow draw offsets use effective shadow offsets.
  - Inline image layout/render size stays unscaled in this increment.

- Update text metadata consumers:
  - `TextRenderInfo` uses `EffectiveLinePadding` so caret/selection positioning matches rendered text.
  - Any text measurement code that uses `LinePadding` for vertical offsets should switch to the effective value.

## Tests
- Extend `MGScaleSettingsTests`:
  - Large positive/negative integer scaling clamps instead of overflowing.
  - Existing rounding/minimum tests still pass.

- Add headless unit tests for new effective helper behavior:
  - Effective font size scales positive authored sizes and never drops below `1`.
  - Effective line padding scales by `FontScale`.
  - Effective text shadow offsets scale by `FontScale`.
  - Effective margin/padding/size helper calculations preserve authored public values.

- Add text/layout regression tests where practical without requiring a live `GraphicsDevice`:
  - Existing text parser/idempotency tests continue passing.
  - Add focused tests around pure helper methods rather than constructing full `MGDesktop` if the current test harness cannot do so cleanly.

- Verification:
  - Run `dotnet build MGUI.Core/MGUI.Core.csproj --no-restore`.
  - Run `dotnet test MGUI.Tests/MGUI.Tests.csproj --no-restore`.
  - Manually smoke-test the sample app at `UIScale = 1.0` and `1.5` if a local graphics run is available.

## Assumptions
- Increment 1 is already merged or staged and includes `MGDesktop.UIScale`, recursive layout invalidation, and `MGScaleSettings`.
- This increment intentionally avoids changing XAML syntax.
- This increment intentionally avoids opt-outs; all base layout/text values are scale-aware by default.
- Public authored values remain stable so existing code can inspect or reset original values without reverse-scaling.
