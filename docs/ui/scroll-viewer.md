# ScrollViewer

Provides a scrollable viewport around a single child element.

- XAML tag: `<ScrollViewer>`
- Runtime type: `MGUI.Core.UI.MGScrollViewer`
- Base type: [SingleContentHost](./single-content-host.md)
- Element type: `MGElementType.ScrollViewer`
- Content model: Single `Content` child.
- Inherits shared API from [SingleContentHost](./single-content-host.md).

## Direct Properties

| Property | Type |
| --- | --- |
| `VerticalScrollBarVisibility` | `ScrollBarVisibility?` |
| `HorizontalScrollBarVisibility` | `ScrollBarVisibility?` |
| `VerticalOffset` | `float?` |
| `HorizontalOffset` | `float?` |
| `ScrollBarUnfocusedOuterBrush` | `FillBrush` |
| `ScrollBarFocusedOuterBrush` | `FillBrush` |
| `ScrollBarUnfocusedInnerBrush` | `FillBrush` |
| `ScrollBarFocusedInnerBrush` | `FillBrush` |

## Related

- Base docs: [SingleContentHost](./single-content-host.md)
- Common element API: [Element](./element.md)
