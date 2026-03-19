# GridColorPicker

Selectable grid of colors with optional preview and multi-select support.

- XAML tag: `<GridColorPicker>`
- Runtime type: `MGUI.Core.UI.MGGridColorPicker`
- Base type: [Element](./element.md)
- Element type: `MGElementType.GridColorPicker`
- Content model: Content property is `Colors`; the selected-color presenter is configured through dedicated element properties.
- Inherits shared API from [Element](./element.md).

## Direct Properties

| Property | Type |
| --- | --- |
| `Border` | `Border` |
| `Columns` | `int?` |
| `Colors` | `List<string>` |
| `CommaSeparatedColors` | `string` |
| `ColorPalette` | `ColorPalette?` |
| `ColorSize` | `Size?` |
| `RowSpacing` | `int?` |
| `ColumnSpacing` | `int?` |
| `SelectedColorBorderBrush` | `BorderBrush` |
| `SelectedColorBorderThickness` | `Thickness?` |
| `UnselectedColorBorderBrush` | `BorderBrush` |
| `UnselectedColorBorderThickness` | `Thickness?` |
| `HoveredColorOverlay` | `FillBrush` |
| `SelectedColorOverlay` | `FillBrush` |
| `SelectedColor` | `XAMLColor?` |
| `AllowMultiSelect` | `bool?` |
| `ShowSelectedColorLabel` | `bool?` |
| `SelectedColorPresenter` | `HeaderedContentPresenter` |
| `SelectedColorLabel` | `TextBlock` |
| `SelectedColorValue` | `Rectangle` |

## Related

- Base docs: [Element](./element.md)
- Common element API: [Element](./element.md)
