# ListView

Generic column-based data view that arranges items into rows and columns.

- XAML tag: `<ListView>`
- Runtime type: `MGUI.Core.UI.MGListView<TItemType>`
- Base type: [MultiContentHost](./multi-content-host.md)
- Element type: `MGElementType.ListView`
- Content model: Primary content property is `Columns`; row data comes from `Items`/`ItemType`-driven runtime state.
- Inherits shared API from [MultiContentHost](./multi-content-host.md).

## Notes

- `ItemType` controls the generic `MGListView<TItemType>` instance created at runtime.
- `Columns` is the XAML content property for this control.

## Direct Properties

| Property | Type |
| --- | --- |
| `ItemType` | `Type` |
| `Columns` | `List<ListViewColumn>` |
| `RowHeight` | `int?` |
| `HeaderGrid` | `Grid` |
| `ScrollViewer` | `ScrollViewer` |
| `DataGrid` | `Grid` |
| `SelectionMode` | `GridSelectionMode?` |

## Related

- Base docs: [MultiContentHost](./multi-content-host.md)
- Common element API: [Element](./element.md)
