# Increment 6: MGUI Runtime Scaling Readiness

## Summary
Add small public convenience and diagnostic APIs that make MGUI’s native UI scaling easier and safer for external games to consume at runtime. Keep behavior unchanged: `MGDesktop.UIScale` remains the global source, `MGWindow.UIScaleOverride` replaces inherited scale for a window subtree, authored properties stay unscaled, and `MGWindow.Scale` remains a post-layout render transform.

## API Changes
- Add `MGDesktop.SetUniformUIScale(float scale)`.
  - Validate through existing `MGScaleSettings.SetUniformScale`.
  - Do not replace the existing `UIScale` instance.
  - Rely on the existing `ScaleChanged` subscription for text/layout refresh.
- Add `MGWindow.SetUniformUIScaleOverride(float? scale)`.
  - `null` clears `UIScaleOverride`.
  - Non-null creates an override if missing, otherwise reuses the existing override and calls `SetUniformScale`.
  - Do not multiply by parent/desktop scale.
- Add immutable diagnostic access:
  - Add public readonly value type `MGScaleSnapshot` with `FontScale`, `SpacingScale`, `SizeScale`, `BorderScale`, and `ImageScale`.
  - Add `MGScaleSnapshot.From(MGScaleSettings settings)`.
  - Add `MGElement.EffectiveUIScaleSnapshot`, returning the scale currently used by that element after `UIScaleMode` and window override resolution.
  - Add `MGWindow.ResolvedUIScaleSnapshot`, returning the resolved window scale before element-level opt-out is applied.
  - Do not expose mutable inherited `MGScaleSettings` publicly.

## Implementation Changes
- Implement helpers as thin wrappers over existing `MGScaleSettings`, `MGElement.EffectiveScaleSettings`, and `MGWindow.ResolvedUIScaleSettings`.
- Keep `MGScaleSettings` mutation behavior unchanged; do not add per-component or per-category opt-out APIs.
- Update `docs/ui-scaling.md`:
  - Show `Desktop.SetUniformUIScale(scale)` as the preferred runtime desktop API.
  - Show `Window.SetUniformUIScaleOverride(null)` for clearing overrides.
  - Clarify again that window overrides replace inherited scale rather than multiplying it.
  - Mention snapshots are diagnostics/read-only and should not be used as a layout mutation mechanism.
- Regenerate `schemas/mgui.xsd` if the public XAML-facing schema changes. These helper methods and snapshots should not normally affect XAML schema output.

## Test Plan
- Add MGUI unit tests for runtime helpers:
  - `MGDesktop.SetUniformUIScale` updates all categories and triggers existing invalidation behavior.
  - Invalid desktop helper values throw the same exceptions as `MGScaleSettings.SetUniformScale`.
  - `MGWindow.SetUniformUIScaleOverride(1.5f)` creates an override and invalidates the window subtree.
  - Calling `SetUniformUIScaleOverride` again reuses the same override instance.
  - `SetUniformUIScaleOverride(null)` clears the override and falls back to parent/desktop scale.
- Add diagnostic tests:
  - desktop-scale element snapshot reflects desktop scale.
  - window override snapshot reflects replacement scale.
  - `UIScaleMode.Disabled` element snapshot reports all `1.0` values.
  - explicit `UIScaleMode.Enabled` under a disabled parent reports the resolved window/desktop scale.
- Add dynamic-content regression tests where practical:
  - combo box dropdown items resolve current scale after runtime scale changes.
  - tab header elements resolve current scale after runtime window override changes.
- Verify:
  - `dotnet build MGUI.Core/MGUI.Core.csproj --no-restore`
  - `dotnet test MGUI.Tests/MGUI.Tests.csproj --no-restore`
  - `dotnet build MGUI.Samples/MGUI.Samples.csproj --no-restore`

## Assumptions
- This increment is MGUI-only; consuming-game migration is separate.
- Convenience helpers are additive and do not replace existing direct `UIScale` / `UIScaleOverride` APIs.
- Diagnostics are intentionally immutable snapshots to avoid accidental mutation of inherited scale settings.
- Component-level scale factors remain out of scope.
