# Layout-Aware UI Scaling For MGUI

## Summary
Add Option 1 as MGUI’s default scaling model: authored XAML/C# layout values remain logical base units, and MGUI computes scaled effective values during layout, text measurement, and drawing. Existing behavior stays unchanged when scale is `1.0`.

This should be implemented in increments so each stage is usable and testable before widening coverage.

## Key API And Behavior
- Add `MGScaleSettings` with `FontScale`, `SpacingScale`, `SizeScale`, `BorderScale`, and `ImageScale`; add a convenience `SetUniformScale(float scale)` or `Scale` setter that updates all categories.
- Add `MGDesktop.UIScale` as the v1 source of truth. Defer per-window/subtree overrides until after the global path is stable.
- Treat existing properties as unscaled source values: `Margin`, `Padding`, `Width`, `Height`, `FontSize`, `BorderThickness`, etc. keep returning authored values.
- Add internal effective helpers on `MGElement`, for example scaled int/thickness/point/size methods, and use them in layout/rendering.
- Scale-aware by default means existing XAML such as `Padding="8"` and `FontSize="14"` is affected by `Desktop.UIScale`; no XAML syntax changes are required for v1.
- Do not change `MGWindow.Scale`; document it as a post-layout render transform, separate from layout-aware `UIScale`.

## Increment 1: Scaling Foundation
- Add `MGScaleSettings` with change notification and centralized rounding helpers.
- Add `MGDesktop.UIScale` and wire scale changes to refresh all windows through existing traversal/layout invalidation.
- Use deterministic rounding from raw values only; never scale already-scaled values.
- Preserve minimum visible values for positive border/image/chrome dimensions where rounding would otherwise produce zero.
- Add tests around scale math, change notification, and layout invalidation.

## Increment 2: Base Layout And Text
- Update `MGElement` layout internals to use effective scaled values for margin, padding, min/max size, preferred width/height, and background render padding.
- Update `MGTextBlock` so `FontSize` remains authored, while font resolution, measurement, wrapping, line height, line padding, and shadow offset use effective scaled values.
- Ensure changing `Desktop.UIScale.FontScale` re-resolves existing text blocks and invalidates layout.
- Add tests for text measurement, wrapping changes, preferred size changes, and scale transitions such as `1.0 -> 1.5 -> 1.0`.

## Increment 3: Containers And Common Chrome
- Apply scaling to container/layout properties: stack spacing, grid row/column spacing, grid line margin, uniform grid cell size, overlay offsets, dock/grid pixel lengths, and similar layout distances.
- Apply border scaling to `MGBorder.BorderThickness` and component-backed border properties used by buttons, windows, text boxes, progress controls, sliders, tabs, and menus.
- Keep row/column indices, spans, item counts, opacity, time values, z-index, selection indices, and command/config values unscaled.
- Add focused tests for stack panel spacing, grid spacing/cell measurement, border measurement, and nested component layout.

## Increment 4: Controls, Images, Docs, And Samples
- Audit control-specific dimensions and classify each as `Font`, `Spacing`, `Size`, `Border`, `Image`, or `None`.
- Scale high-impact controls first: button, text box, checkbox/radio, progress bar/button, slider, scroll viewer/scroll bars, tree view indent/expander, context menu, tooltip, window title/chrome.
- Scale image layout sizes only when an image participates in UI sizing; do not scale arbitrary background art by default.
- Add docs explaining `UIScale` vs `MGWindow.Scale`, default scale-aware properties, and practical examples for `1.25`, `1.5`, and `2.0`.
- Add a sample window with a live scale selector to visually verify reflow.

## Increment 5: Opt-Outs And Refinement
- Add opt-out only after default scaling works: start with an element-level `UIScaleMode` or `IsUIScaleEnabled` flag, inherited by children.
- Consider per-category opt-outs later only if real use cases appear; avoid complicating v1 XAML.
- Add optional per-window scale override after global desktop scale is stable.
- Add regression tests for opt-out inheritance and mixed scaled/unscaled subtrees.

## Test Plan
- Unit-test scale helper rounding and zero/nonzero preservation.
- Unit-test layout measurement for base elements, borders, text blocks, stack panels, grids, and representative controls at `1.0`, `1.25`, `1.5`, and `2.0`.
- Verify runtime scale changes invalidate cached measurements and re-resolve text without mutating authored property values.
- Add sample/manual checks for menu UI, tooltip, text box editing/caret placement, scroll bars, and window chrome.
- Confirm existing tests and samples are unchanged at `UIScale = 1.0`.

## Assumptions
- V1 scale source is `MGDesktop.UIScale`; per-window/subtree overrides are later increments.
- Existing XAML numeric values become logical UI units by default.
- Public property getters continue returning authored values, not effective scaled values.
- `MGWindow.Scale` remains unchanged and continues to mean post-layout render scaling.
