# MenuBarItem

Top-level menu entry that opens a submenu or item collection.

- XAML tag: `<MenuBarItem>`
- Runtime type: `MGUI.Core.UI.MGMenuBarItem`
- Base type: [SingleContentHost](./single-content-host.md)
- Element type: `MGElementType.MenuBarItem`
- Content model: Primary content property is `Items`; you can also provide explicit `Content` and `Submenu`.
- Inherits shared API from [SingleContentHost](./single-content-host.md).

## Notes

- If `Submenu` is omitted, the `Items` collection is converted into a context menu automatically.

## Direct Properties

| Property | Type |
| --- | --- |
| `Text` | `string` |
| `Items` | `List<ContextMenuItem>` |
| `Submenu` | `ContextMenu` |

## Related

- Base docs: [SingleContentHost](./single-content-host.md)
- Common element API: [Element](./element.md)
