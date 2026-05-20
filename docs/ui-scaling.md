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

Public properties return the authored value. For example, `Padding="8"` still reads back as `8` after `Desktop.SetUniformUIScale(1.5f)`. During measurement and drawing, MGUI uses the effective value for that category, so that padding behaves like `12` without mutating the property.

This keeps XAML stable and avoids repeated scaling when the UI scale changes from `1.0` to `1.5` and back to `1.0`.

## Practical Scales

- `1.25`: a modest readability bump. A `FontSize="14"` text run resolves around `18`, and `Padding="8"` behaves like `10`.
- `1.5`: a comfortable high-DPI scale. A `16x16` checkbox component participates in layout around `24x24`; inline `16x16` images measure around `24x24`.
- `2.0`: a full doubling. A `BorderThickness="1"` remains visible, common hit targets expand, and intrinsic images request twice their authored size.

Data values remain unscaled: slider ranges, scroll offsets, counts, indices, durations, opacity, selected values, and command IDs are still model values.

## Opt-Outs

Use `UIScaleMode` when part of the visual tree needs authored values to resolve at `1.0` while the rest of the desktop remains scaled.

```xml
<StackPanel UIScaleMode="Disabled" Padding="8" Spacing="6">
    <TextBlock Text="This island measures and draws at authored sizes." />
    <Button Content="Unscaled button" />
    <StackPanel UIScaleMode="Enabled">
        <TextBlock Text="Scaling is re-enabled for this subtree." />
    </StackPanel>
</StackPanel>
```

`Disabled` is inherited by descendants unless a child explicitly sets `UIScaleMode="Enabled"`. The default is `Inherit`, which means top-level elements are scale-aware.

## Per-Window Overrides

`MGWindow.UIScaleOverride` replaces the inherited scale for that window subtree. A top-level window with no override uses `MGDesktop.UIScale`; nested and modal windows with no override inherit the nearest parent window scale.

```csharp
dialog.SetUniformUIScaleOverride(1.5f);
dialog.SetUniformUIScaleOverride(null);
```

XAML supports a uniform-scale convenience on `Window`:

```xml
<Window UIScaleOverride="1.5" Width="360" Height="220">
    <TextBlock Text="This window uses 1.5 layout-aware scaling." />
</Window>
```

`UIScaleOverride` does not multiply by `MGDesktop.UIScale`; it replaces the inherited scale settings. Calling `SetUniformUIScaleOverride(null)` clears the override and returns the window to inherited scale. `MGWindow.Scale` remains a separate post-layout render transform.

## Runtime Diagnostics

`MGScaleSnapshot` provides read-only scale values for diagnostics and logging. `MGElement.EffectiveUIScaleSnapshot` reports the scale currently used by an element after `UIScaleMode` and window override resolution. `MGWindow.ResolvedUIScaleSnapshot` reports the resolved window scale before any element-level opt-out is applied.

Snapshots are not a layout mutation mechanism. Change runtime scale through `MGDesktop.SetUniformUIScale`, `MGDesktop.UIScale`, `MGWindow.SetUniformUIScaleOverride`, or `MGWindow.UIScaleOverride`.
