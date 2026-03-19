# ProgressButton

Button-like control that embeds progress state, timing, and completion behavior.

- XAML tag: `<ProgressButton>`
- Runtime type: `MGUI.Core.UI.MGProgressButton`
- Base type: [SingleContentHost](./single-content-host.md)
- Element type: `MGElementType.ProgressButton`
- Content model: Single `Content` child plus embedded progress-bar customization.
- Inherits shared API from [SingleContentHost](./single-content-host.md).

## Direct Properties

| Property | Type |
| --- | --- |
| `Border` | `Border` |
| `ActionWhenProcessing` | `ProgressButtonActionType?` |
| `ActionWhenPaused` | `ProgressButtonActionType?` |
| `ActionWhenCompleted` | `ProgressButtonActionType?` |
| `ActionOnCompleted` | `ProgressButtonActionType?` |
| `HideWhenPaused` | `bool?` |
| `IsPaused` | `bool?` |
| `Minimum` | `float?` |
| `Maximum` | `float?` |
| `Value` | `float?` |
| `Duration` | `TimeSpan?` |
| `Orientation` | `Orientation?` |
| `IsReversed` | `bool?` |
| `ProgressBarAlignment` | `ProgressBarAlignment?` |
| `ProgressBarSize` | `int?` |
| `ProgressBarMargin` | `Thickness?` |
| `ProgressBarBorderThickness` | `Thickness?` |
| `ProgressBarBorderBrush` | `BorderBrush` |
| `ProgressBarBackground` | `FillBrush` |
| `ProgressBarForeground` | `FillBrush` |

## Related

- Base docs: [SingleContentHost](./single-content-host.md)
- Common element API: [Element](./element.md)
