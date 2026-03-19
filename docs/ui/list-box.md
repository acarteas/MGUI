# ListBox

Generic selectable list with templates, header support, and scrollable items.

- XAML tag: `<ListBox>`
- Runtime type: `MGUI.Core.UI.MGListBox<TItemType>`
- Base type: [MultiContentHost](./multi-content-host.md)
- Element type: `MGElementType.ListBox`
- Content model: Primary content property is `Items`; list presentation is configured through borders, header, and templates.
- Inherits shared API from [MultiContentHost](./multi-content-host.md).

## Notes

- `ItemType` controls the generic `MGListBox<TItemType>` instance created at runtime.

## Direct Properties

| Property | Type |
| --- | --- |
| `ItemType` | `Type` |
| `OuterBorder` | `Border` |
| `InnerBorder` | `Border` |
| `TitleBorder` | `Border` |
| `TitlePresenter` | `ContentPresenter` |
| `IsTitleVisible` | `bool?` |
| `ScrollViewer` | `ScrollViewer` |
| `ItemsPanel` | `StackPanel` |
| `Header` | `Element` |
| `Items` | `List<object>` |
| `CanDeselectByClickingSelectedItem` | `bool?` |
| `SelectionMode` | `ListBoxSelectionMode?` |
| `SelectedValue` | `object` |
| `AlternatingRowBackgrounds` | `List<FillBrush>` |
| `ItemTemplate` | `ContentTemplate` |
| `ItemContainerStyle` | `Border` |

## Related

- Base docs: [MultiContentHost](./multi-content-host.md)
- Common element API: [Element](./element.md)
