# UniformGrid

Grid container that arranges children into evenly sized cells.

- XAML tag: `<UniformGrid>`
- Runtime type: `MGUI.Core.UI.MGUniformGrid`
- Base type: [MultiContentHost](./multi-content-host.md)
- Element type: `MGElementType.UniformGrid`
- Content model: Collection of `Children` placed in evenly sized cells.
- Inherits shared API from [MultiContentHost](./multi-content-host.md).

## Direct Properties

| Property | Type |
| --- | --- |
| `Rows` | `int?` |
| `Columns` | `int?` |
| `CellSize` | `Size?` |
| `HeaderRowHeight` | `int?` |
| `HeaderColumnWidth` | `int?` |
| `SelectionMode` | `GridSelectionMode?` |
| `CanDeselectByClickingSelectedCell` | `bool?` |
| `SelectionBackground` | `FillBrush` |
| `SelectionOverlay` | `FillBrush` |
| `GridLineIntersectionHandling` | `GridLineIntersection?` |
| `GridLinesVisibility` | `GridLinesVisibility?` |
| `GridLineMargin` | `int?` |
| `HorizontalGridLineBrush` | `FillBrush` |
| `VerticalGridLineBrush` | `FillBrush` |
| `RowSpacing` | `int?` |
| `ColumnSpacing` | `int?` |
| `CellBackground` | `FillBrush` |
| `DrawEmptyCells` | `bool?` |
| `AutoAssignCells` | `bool?` |

## Related

- Base docs: [MultiContentHost](./multi-content-host.md)
- Common element API: [Element](./element.md)
