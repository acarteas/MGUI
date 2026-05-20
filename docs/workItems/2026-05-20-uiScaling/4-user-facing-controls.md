# Increment 4: Controls, Images, Docs, And Samples

## Summary
Widen `MGDesktop.UIScale` coverage from layout primitives to user-facing controls, image sizing, inline images, and developer docs. Public authored values should remain unchanged; effective helpers should drive measurement, layout, hit targets, and drawing.

## Public API / Interfaces
- Add internal/protected effective helpers on controls where control-specific dimensions are not already covered by `MGElement`, `MGBorder`, or container scaling.
- Extend `ITextMeasurer` with a default `MeasureImage(MGTextRunImage image)` implementation returning the authored image size, then implement the scaled version in `MGTextBlock`. This keeps existing custom measurers source-compatible.
- Do not add XAML syntax or opt-out APIs in this increment.
- Keep `MGWindow.Scale` unchanged and documented as post-layout render scaling.

## Implementation Changes
- Images and inline text images:
  - In `MGImage`, add effective source/render-size helpers using `MGScaleCategory.Image`.
  - Use effective image size for `Stretch.None`, pseudo-infinite measurement fallbacks, and any intrinsic-size measurement path.
  - Do not image-scale final draw bounds when an image is already constrained by parent layout or explicit `Width`/`Height`; those are already handled by layout size scaling.
  - In `MGTextBlock`/`MGTextLine`, measure, wrap, align, draw, and hit-test inline `[Image=...]` runs using image-scaled sizes.
  - Scale inline image defaults from `MGResources.GetTextureDimensions` at effective-use time, not by mutating stored runs.

- High-impact control dimensions:
  - `MGProgressBar`: scale `Size` with `MGScaleCategory.Size` in measurement and drawing.
  - `MGProgressButton`: scale `ProgressBarSize` with `Size`, `ProgressBarMargin` with `Spacing`, and continue using effective border helpers from Increment 3.
  - `MGSlider`: scale number-line size, tick width/height, thumb width/height with `Size`; scale number-line/tick/thumb border thickness with `Border`; scale hover/touch expansion with `Spacing`. Do not scale value range, tick frequency, discrete intervals, or max tick count.
  - `MGScrollViewer`: scale scrollbar width/height and scrollbar padding with `Size`/`Spacing`; scale minimum scrollbar thumb size and mouse-wheel scroll interval with `Size`. Do not scale `HorizontalOffset`, `VerticalOffset`, max offsets, or visibility modes.
  - `MGCheckBox`: rely on existing component preferred-size and margin scaling, but scale checkmark shadow offset with `Spacing` and checkmark stroke thickness with `Border`.
  - `MGRadioButton`: rely on existing component preferred-size and spacing scaling, but scale bubble border thickness with `Border` and checked-dot inset/radius math with `Size`.
  - `MGResizeGrip`: scale dot spacing and dot draw size with `Spacing`/`Size`; keep `MaxDots` unscaled.
  - `MGSpacer`: scale `Width` and `Height` with `Size` during measurement.

- Menus, tooltip, and window chrome:
  - `MGComboBox`: scale dropdown arrow draw dimensions with `Size`; existing arrow presenter size and margins should continue through base effective layout.
  - `MGContextMenuItem`: scale submenu arrow draw dimensions and submenu hover/open expansion with `Size`/`Spacing`; header presenter size should remain authored but flow through base preferred-size scaling.
  - `MGContextMenuSeparator`: ensure separator height flows through effective size, either via existing separator/spacer path or a helper.
  - `MGToolTip`: scale draw offset with `Spacing`; keep host relationship and open/close behavior unchanged.
  - `MGWindow`: audit title/close/resize chrome for raw geometry reads. Convert raw geometry reads to effective values where they affect visible chrome or hit areas.

- Remaining chrome/raw-value cleanup:
  - Update `MGBorderedFillBrush` so its internal border thickness is scaled with `Border` when drawing element chrome.
  - Update visible raw `BorderThickness`/`Padding` geometry in controls such as `MGGridColorPicker` to use effective border/padding helpers.
  - Leave texture fill brushes and textured border source artwork unscaled directly; they should scale only through the destination bounds they are asked to fill.

## Docs And Sample
- Add a UI scaling doc page covering:
  - `MGDesktop.UIScale` vs `MGWindow.Scale`.
  - Category meanings: `Font`, `Spacing`, `Size`, `Border`, `Image`, `None`.
  - Authored values vs effective values.
  - Practical examples for `1.25`, `1.5`, and `2.0`.
  - Known v1 limitation: no element-level opt-out yet.
- Link the doc from the existing docs index/home.
- Add a sample feature window, preferably `MGUI.Samples/Features/UIScaling.xaml(.cs)`, registered in `Compendium`.
  - Include a live scale selector for `1.0`, `1.25`, `1.5`, and `2.0`.
  - Show representative controls: text, buttons, checkbox/radio, slider, scroll viewer, image, inline image, context menu/tooltip.
  - The sample should set `Desktop.UIScale.SetUniformScale(...)` and restore `1.0` when closed/hidden.

## Test Plan
- Extend effective-scale unit tests for:
  - `MGImage` intrinsic measurement and `Stretch.None`.
  - Inline image line measurement/wrapping/draw-size helpers.
  - `MGProgressBar.Size`, `MGProgressButton.ProgressBarSize`, and `ProgressBarMargin`.
  - Slider effective thumb/tick/number-line sizes and borders.
  - Scroll viewer scrollbar width/height, padding, min thumb size, and wheel interval.
  - Checkbox/radio visual helper dimensions.
  - Resize grip and spacer measurement.
  - `MGBorderedFillBrush` border thickness scaling.
- Add `1.0` preservation assertions for every new helper.
- Verify:
  - `dotnet build MGUI.Core/MGUI.Core.csproj --no-restore`
  - `dotnet test MGUI.Tests/MGUI.Tests.csproj --no-restore`
  - If practical, run the sample app and manually check the new UI scaling sample at `1.0`, `1.5`, and `2.0`.

## Assumptions
- Increments 1-3 are committed.
- Control-specific visual dimensions should scale by default.
- Data/model values remain unscaled: counts, indices, selection values, durations, opacity, slider ranges, scroll offsets, and command IDs.
- Element-level opt-outs and per-window scale overrides remain Increment 5 work.
