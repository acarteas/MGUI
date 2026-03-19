# ComboBox

Generic dropdown selector that templates and displays a set of items.

- XAML tag: `<ComboBox>`
- Runtime type: `MGUI.Core.UI.MGComboBox<TItemType>`
- Base type: [MultiContentHost](./multi-content-host.md)
- Element type: `MGElementType.ComboBox`
- Content model: Primary content property is `Items`; items are templated into the dropdown and selected item presenter.
- Inherits shared API from [MultiContentHost](./multi-content-host.md).

## Notes

- `ItemType` controls the generic runtime type that gets instantiated from XAML.

## Direct Properties

| Property | Type |
| --- | --- |
| `ItemType` | `Type` |
| `Border` | `Border` |
| `DropdownArrow` | `ContentPresenter` |
| `DropdownArrowColor` | `XAMLColor?` |
| `Items` | `List<object>` |
| `DropdownItemTemplate` | `ContentTemplate` |
| `SelectedItemTemplate` | `ContentTemplate` |
| `SelectedIndex` | `int?` |
| `MinDropdownWidth` | `int?` |
| `MaxDropdownWidth` | `int?` |
| `MinDropdownHeight` | `int?` |
| `MaxDropdownHeight` | `int?` |
| `Dropdown` | `Window` |
| `DropdownScrollViewer` | `ScrollViewer` |
| `DropdownStackPanel` | `StackPanel` |
| `DropdownHeader` | `Element` |
| `DropdownFooter` | `Element` |

## Related

- Base docs: [MultiContentHost](./multi-content-host.md)
- Common element API: [Element](./element.md)
