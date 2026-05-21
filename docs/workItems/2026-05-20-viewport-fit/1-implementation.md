# Increment 1: Native MGUI Viewport Fit Constraints

## Summary

Add first-class MGUI support for constraining an element’s measured size to the current viewport, so dialogs can express responsive caps in XAML instead of a code-behind in code that consumes this library. 

## Key Changes

- Add a runtime enum, `ViewportFitMode`, with:
  - `None`
  - `Width`
  - `Height`
  - `WidthAndHeight`

- Add `MGElement` properties:
  - `ViewportFit`, default `None`
  - `ViewportMargin`, default zero `Thickness`

- Add matching XAML properties on base `Element`:
  - `ViewportFit="WidthAndHeight"`
  - `ViewportMargin="48"` or normal `Thickness` forms if already supported by MGUI parsing

- Treat `ViewportMargin` as viewport/screen-space inset in pixels, not as authored UI spacing. The remaining viewport size is then converted through the element’s effective UI scale during measurement.

## Implementation Shape

- Apply viewport fit during measurement as an additional effective max-size cap.
- Do not mutate authored `Width`, `Height`, `MaxWidth`, or `MaxHeight`.
- Combine constraints as:
  - authored max size, if present
  - viewport-derived max size, if `ViewportFit` enables that axis
  - existing min/preferred/explicit size behavior remains responsible for final clamping
- Use the element’s effective UI scale snapshot so `UIScaleMode` and window/desktop scale are honored consistently.
- Use the current desktop valid screen bounds as the viewport source.
- Changing `ViewportFit` or `ViewportMargin` should invalidate layout.

## Documentation

- Update MGUI layout docs to describe viewport fit constraints.
- Document that `ViewportFit` is intended for dialogs, overlays, popups, and other bounded UI that should remain usable on small windows or high UI scale.
- Document that `ViewportMargin` is a viewport inset in screen pixels.

## Test Plan

- Add MGUI tests for:
  - XAML parsing of `ViewportFit` and `ViewportMargin`
  - `None` preserving existing max-size behavior
  - width-only, height-only, and both-axis fitting
  - viewport cap combining with authored `MaxWidth`/`MaxHeight`
  - explicit `Width`/`Height` shrinking through measurement when viewport cap is smaller
  - `MinWidth`/`MinHeight` still acting as the lower bound
  - non-1.0 UI scale producing the expected viewport-constrained measured size
  - layout invalidation when fit settings change

## Assumptions

- The property name should be `ViewportFit`, not `FitToViewport`, because the behavior is a constraint applied during layout rather than a command-style resize operation.
- Component-level UI scaling is not part of this work.
- The initial implementation only needs viewport width/height fitting; other constraint families can be added later if a real use case appears.
