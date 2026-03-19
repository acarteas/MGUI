# ContextMenu

Popup menu window that hosts context-menu items and optional scrolling.

- XAML tag: `<ContextMenu>`
- Runtime type: `MGUI.Core.UI.MGContextMenu`
- Base type: [Window](./window.md)
- Element type: `MGElementType.ContextMenu`
- Content model: Primary content property is `Items`.
- Inherits shared API from [Window](./window.md).

## Notes

- Can be attached through `Element.ContextMenu` or created explicitly as a popup menu window.

## Direct Properties

| Property | Type |
| --- | --- |
| `ButtonWrapperTemplate` | `Button` |
| `CanOpen` | `bool?` |
| `StaysOpenOnItemSelected` | `bool?` |
| `StaysOpenOnItemToggled` | `bool?` |
| `AutoCloseThreshold` | `float?` |
| `Items` | `List<ContextMenuItem>` |
| `ScrollViewer` | `ScrollViewer` |
| `ItemsPanel` | `StackPanel` |
| `HeaderWidth` | `int?` |
| `HeaderHeight` | `int?` |

## Related

- Base docs: [Window](./window.md)
- Common element API: [Element](./element.md)
