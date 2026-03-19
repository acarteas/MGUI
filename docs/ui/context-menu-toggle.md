# ContextMenuToggle

Checkable toggle entry inside a `ContextMenu`.

- XAML tag: `<ContextMenuToggle>`
- Runtime type: `MGUI.Core.UI.MGContextMenuToggle`
- Base type: [WrappedContextMenuItem](./wrapped-context-menu-item.md)
- Element type: `MGElementType.ContextMenuItem`
- Content model: Single `Content` child used as the label/body for the menu entry.
- Inherits shared API from [WrappedContextMenuItem](./wrapped-context-menu-item.md).

## Notes

- Inherits `Submenu` support from `WrappedContextMenuItem`.

## Direct Properties

| Property | Type |
| --- | --- |
| `CommandId` | `string` |
| `IsChecked` | `bool?` |
| `ShortcutText` | `string` |

## Related

- Base docs: [WrappedContextMenuItem](./wrapped-context-menu-item.md)
- Common element API: [Element](./element.md)
