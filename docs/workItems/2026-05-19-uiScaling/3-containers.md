# Increment 3: Containers And Common Chrome Scaling

## Summary
Add scale-aware behavior to container spacing, grid pixel dimensions, overlay offsets, and shared border chrome while keeping all public authored values unchanged. This increment should make layout containers respond to `MGDesktop.UIScale` without forcing app code to manually resize common panel/chrome properties.

## Key Changes

- Add effective-value helpers for container/chrome properties:
  - `MGBorder.EffectiveBorderThickness` using `MGScaleCategory.Border`.
  - `MGStackPanel.EffectiveSpacing` using `MGScaleCategory.Spacing`.
  - `MGGrid` helpers for effective row/column spacing, grid line margin, pixel grid lengths, and min/max row/column constraints.
  - `MGUniformGrid` helpers for effective cell size, header row/column sizes, spacing, and grid line margin.
  - `MGOverlayPanel` helper for scale-aware child offsets.

- Update shared border behavior:
  - Use effective border thickness in `MGBorder` measurement and drawing.
  - Update `MGElement` background bounds/render padding paths that read `GetBorder().BorderThickness` so they use the effective value.
  - Audit common controls that manually read a `MGBorder.BorderThickness` for geometry or drawing and switch those reads to the effective value.
  - Keep `BorderThickness` itself authored/unscaled, including change notifications and XAML behavior.

- Update container layout:
  - `MGStackPanel` should use effective spacing for measurement and child layout.
  - `MGGrid` should scale row/column spacing and grid line margin everywhere they affect measurement, layout, or gridline drawing.
  - `MGGrid` should scale only pixel-sized `GridLength` values and row/column min/max constraints. Auto and weighted dimensions stay semantic and unscaled.
  - `MGUniformGrid` should scale cell size, header dimensions, spacing, and grid line margin consistently across measurement, bounds calculation, layout, and gridline drawing.
  - `MGOverlayPanel` should scale child `Offset` values during measurement/layout only. `ZIndex` and ordering remain unscaled.

## Public API Behavior

- No XAML syntax changes.
- Existing properties continue to return authored values:
  - `Spacing`, `RowSpacing`, `ColumnSpacing`, `GridLineMargin`, `CellSize`, `HeaderRowHeight`, `HeaderColumnWidth`, `BorderThickness`, overlay child `Offset`, grid pixel lengths, and min/max constraints.
- Effective helpers may be internal or protected unless there is already a strong public-helper pattern.
- Scale changes should continue to rely on the Increment 1 desktop invalidation path rather than per-property scale-change events.

## Test Plan

- Add focused tests for effective helpers:
  - Border thickness scales with `Border` and preserves nonzero minimum behavior.
  - Stack panel spacing scales with `Spacing` while authored `Spacing` remains unchanged.
  - Grid pixel lengths and min/max constraints scale with `Size`; weighted and auto lengths do not.
  - Grid row/column spacing and grid line margin scale with `Spacing`.
  - Uniform grid cell/header sizes scale with `Size`; spacing and grid line margin scale with `Spacing`.
  - Overlay offsets scale with `Spacing`; `ZIndex` is unchanged.
  - Scale `1.0` preserves existing values.

- Add at least one measurement/layout-oriented test where practical, especially for border measurement or stack/grid spacing, to catch accidental raw-property use.

- Verify with:
  - `dotnet build MGUI.Core/MGUI.Core.csproj --no-restore`
  - `dotnet test MGUI.Tests/MGUI.Tests.csproj --no-restore`

## Assumptions

- Increments 1 and 2 are present.
- `GridLineMargin` should scale with `Spacing`, because it is part of the spacing reserved around grid lines.
- This increment does not scale control-specific affordances such as slider tick sizes, tree indentation, tooltip offsets, menu dimensions, scrollbar sizes, or icon/image dimensions unless they are direct uses of shared border/container primitives. Those remain for the later controls/images increment.
