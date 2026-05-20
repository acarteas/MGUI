# Increment 5: Opt-Outs And Per-Window UI Scaling

## Summary
Finish the v1 UI scaling model by adding inherited element-level opt-outs and optional per-window layout-aware scale overrides. `MGDesktop.UIScale` remains the global default, authored properties stay unscaled, and `MGWindow.Scale` remains the separate post-layout render transform.

## Public API / Interface Changes
- Add `UIScaleMode` enum in `MGUI.Core.UI`:
  - `Inherit`: default; use parent effective mode, or enabled at the root.
  - `Enabled`: explicitly use layout-aware UI scaling for this subtree.
  - `Disabled`: use unscaled `1.0` category values for this subtree.
- Add `MGElement.UIScaleMode`, defaulting to `Inherit`.
  - Add internal/protected effective helpers such as `DerivedUIScaleMode` / `IsUIScaleEffectivelyEnabled`.
  - `Disabled` is inherited by descendants unless a descendant explicitly sets `Enabled`.
- Add `MGWindow.UIScaleOverride`.
  - Type: nullable `MGScaleSettings`.
  - `null` means inherit from parent window, or fall back to `MGDesktop.UIScale` for top-level windows.
  - A non-null value replaces the inherited desktop/window scale for that window subtree.
- Add XAML support:
  - Base `Element` gets `UIScaleMode="Inherit|Enabled|Disabled"`.
  - XAML `Window` gets `UIScaleOverride="1.5"` as a uniform-scale convenience that creates an `MGScaleSettings` override.
  - Do not add per-category XAML scale settings in this increment.

## Implementation Changes
- Centralize scale resolution:
  - Change `MGElement.EffectiveScaleSettings` to return the resolved window scale when effective mode is enabled.
  - Return an internal read-only unscaled `MGScaleSettings` instance when effective mode is disabled.
  - Add read-only protection to the shared unscaled settings so accidental mutation throws instead of changing global behavior.
- Wire invalidation correctly:
  - Changing `MGElement.UIScaleMode` refreshes text caches and invalidates layout for that element subtree, including components, tooltips, and context menus.
  - Changing `MGWindow.UIScaleOverride`, replacing it, clearing it, or mutating its `ScaleChanged` event refreshes text caches and invalidates that window subtree.
  - Changing `MGDesktop.UIScale` continues to refresh all windows, including overlay windows.
- Audit direct global scale usage:
  - Replace control paths that call `Desktop.UIScale` directly with the element’s resolved `EffectiveScaleSettings`.
  - In particular, update checkbox checkmark stroke scaling so disabled subtrees draw unscaled chrome.
- Per-window inheritance behavior:
  - Top-level window with `UIScaleOverride == null` uses `Desktop.UIScale`.
  - Nested/modal windows with `UIScaleOverride == null` inherit the nearest parent window’s resolved scale.
  - A nested/modal window with its own override uses that override for its subtree.
  - `UIScaleMode.Disabled` on any element, including a window root, wins over the resolved window/desktop scale for that element subtree.
- Docs and sample:
  - Update `docs/ui-scaling.md` to replace the current limitation section with opt-out and per-window override examples.
  - Extend the UI scaling sample with an unscaled island and a per-window override demonstration.

## Test Plan
- Add unit tests for `UIScaleMode`:
  - default `Inherit` preserves current scaled behavior.
  - `Disabled` on a parent makes child margin/padding/font/image/control helpers resolve unscaled values.
  - explicit child `Enabled` re-enables scaling under a disabled parent.
  - authored values remain unchanged in all modes.
- Add per-window override tests:
  - top-level window override replaces desktop scale.
  - nested/modal windows inherit parent override when null.
  - nested/modal windows with their own override use their own scale.
  - mutating an override invalidates layout and refreshes text caches for the affected subtree.
- Add regression coverage for direct-scale audit:
  - checkbox/radio or representative control chrome respects `UIScaleMode.Disabled`.
  - text block effective font size switches between scaled and unscaled when mode/override changes.
- Verify:
  - `dotnet build MGUI.Core/MGUI.Core.csproj --no-restore`
  - `dotnet test MGUI.Tests/MGUI.Tests.csproj --no-restore`
  - `dotnet build MGUI.Samples/MGUI.Samples.csproj --no-restore`

## Assumptions
- `UIScaleMode` is the chosen API shape, not a boolean flag.
- Per-window `UIScaleOverride` is included in Increment 5.
- Window overrides replace inherited scale settings; they do not multiply by desktop scale.
- Per-category element opt-outs remain out of scope until a concrete use case appears.
