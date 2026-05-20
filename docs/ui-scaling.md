# UI Scaling

`MGDesktop.UIScale` is MGUI's layout-aware scaling source. Authored values in XAML and C# stay unchanged, while layout, text measurement, common chrome, control affordances, and UI image measurements use scaled effective values internally.

`MGWindow.Scale` is different: it is a post-layout render scale for a window. It does not cause text to reflow, controls to request more space, or scrollbars to reserve larger layout bounds.

## Categories

- `Font`: font sizes, text line padding, and text shadow offsets.
- `Spacing`: margins, padding, container gaps, tooltip offsets, hover/touch expansion, and similar empty-space distances.
- `Size`: preferred sizes, min/max sizes, grid pixel lengths, scrollbar sizes, slider thumbs/ticks, progress bar thicknesses, and other control dimensions.
- `Border`: border thicknesses and stroked chrome.
- `Image`: intrinsic image sizes and inline text image sizes.
- `None`: data values that should not scale.

## Authored And Effective Values

Public properties return the authored value. For example, `Padding="8"` still reads back as `8` after `Desktop.UIScale.SetUniformScale(1.5f)`. During measurement and drawing, MGUI uses the effective value for that category, so that padding behaves like `12` without mutating the property.

This keeps XAML stable and avoids repeated scaling when the UI scale changes from `1.0` to `1.5` and back to `1.0`.

## Practical Scales

- `1.25`: a modest readability bump. A `FontSize="14"` text run resolves around `18`, and `Padding="8"` behaves like `10`.
- `1.5`: a comfortable high-DPI scale. A `16x16` checkbox component participates in layout around `24x24`; inline `16x16` images measure around `24x24`.
- `2.0`: a full doubling. A `BorderThickness="1"` remains visible, common hit targets expand, and intrinsic images request twice their authored size.

Data values remain unscaled: slider ranges, scroll offsets, counts, indices, durations, opacity, selected values, and command IDs are still model values.

## Current Limitation

The v1 scaling model is desktop-wide. There is not yet an element-level opt-out or per-window scale override.
