# Grid

Row/column layout container with optional selection and gridline features.

- XAML tag: `<Grid>`
- Runtime type: `MGUI.Core.UI.MGGrid`
- Base type: [MultiContentHost](./multi-content-host.md)
- Element type: `MGElementType.Grid`
- Content model: Collection of `Children` placed by attached row/column properties.
- Inherits shared API from [MultiContentHost](./multi-content-host.md).

## Direct Properties

| Property | Type |
| --- | --- |
| `RowLengths` | `string` |
| `ColumnLengths` | `string` |
| `RowDefinitions` | `List<RowDefinition>` |
| `ColumnDefinitions` | `List<ColumnDefinition>` |
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

## Related

- Base docs: [MultiContentHost](./multi-content-host.md)
- Common element API: [Element](./element.md)
