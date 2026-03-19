# ContextMenuRadioButton

Grouped radio-style entry inside a `ContextMenu`.

- XAML tag: `<ContextMenuRadioButton>`
- Runtime type: `MGUI.Core.UI.MGContextMenuRadioButton`
- Base type: [WrappedContextMenuItem](./wrapped-context-menu-item.md)
- Element type: `MGElementType.ContextMenuItem`
- Content model: Single `Content` child used as the label/body for the menu entry.
- Inherits shared API from [WrappedContextMenuItem](./wrapped-context-menu-item.md).

## Notes

- Inherits `Submenu` support from `WrappedContextMenuItem`.
- Items with the same `GroupName` are mutually exclusive within the same menu.

## Direct Properties

| Property | Type |
| --- | --- |
| `GroupName` | `string` |
| `IsChecked` | `bool?` |
| `CommandId` | `string` |
| `ShortcutText` | `string` |

## Related

- Base docs: [WrappedContextMenuItem](./wrapped-context-menu-item.md)
- Common element API: [Element](./element.md)
