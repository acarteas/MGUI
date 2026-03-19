# ProgressBar

Displays scalar progress with configurable range, orientation, brushes, and value text.

- XAML tag: `<ProgressBar>`
- Runtime type: `MGUI.Core.UI.MGProgressBar`
- Base type: [Element](./element.md)
- Element type: `MGElementType.ProgressBar`
- Content model: No child elements; optional value text is configured through `ValueTextBlock`.
- Inherits shared API from [Element](./element.md).

## Direct Properties

| Property | Type |
| --- | --- |
| `Border` | `Border` |
| `ValueTextBlock` | `TextBlock` |
| `ShowValue` | `bool?` |
| `ValueDisplayFormat` | `string` |
| `NumberFormat` | `string` |
| `Minimum` | `float?` |
| `Maximum` | `float?` |
| `Value` | `float?` |
| `Size` | `int?` |
| `CompletedBrush` | `FillBrush` |
| `IncompleteBrush` | `FillBrush` |
| `Orientation` | `Orientation?` |
| `IsReversed` | `bool?` |

## Related

- Base docs: [Element](./element.md)
- Common element API: [Element](./element.md)
