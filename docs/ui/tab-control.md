# TabControl

Hosts tab headers and the content of the currently selected tab.

- XAML tag: `<TabControl>`
- Runtime type: `MGUI.Core.UI.MGTabControl`
- Base type: [Element](./element.md)
- Element type: `MGElementType.TabControl`
- Content model: Primary content property is `Tabs`; each `TabItem` contributes its own header and content.
- Inherits shared API from [Element](./element.md).

## Direct Properties

| Property | Type |
| --- | --- |
| `Border` | `Border` |
| `HeadersPanel` | `StackPanel` |
| `HeaderAreaBackground` | `FillBrush` |
| `TabHeaderPosition` | `Dock?` |
| `SelectedTabHeaderTemplate` | `Button` |
| `UnselectedTabHeaderTemplate` | `Button` |
| `Tabs` | `List<TabItem>` |

## Related

- Base docs: [Element](./element.md)
- Common element API: [Element](./element.md)
