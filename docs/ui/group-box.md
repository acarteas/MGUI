# GroupBox

Bordered content section with a header and optional expandable behavior.

- XAML tag: `<GroupBox>`
- Runtime type: `MGUI.Core.UI.MGGroupBox`
- Base type: [SingleContentHost](./single-content-host.md)
- Element type: `MGElementType.GroupBox`
- Content model: Single `Content` child plus a `Header` element and optional expander wrapper.
- Inherits shared API from [SingleContentHost](./single-content-host.md).

## Direct Properties

| Property | Type |
| --- | --- |
| `BorderBrush` | `UniformBorderBrush` |
| `BorderThickness` | `Thickness?` |
| `Expander` | `Expander` |
| `OuterHeaderPresenter` | `HeaderedContentPresenter` |
| `HeaderPresenter` | `ContentPresenter` |
| `IsExpandable` | `bool?` |
| `Header` | `Element` |
| `HeaderHorizontalMargin` | `int?` |
| `HeaderHorizontalPadding` | `int?` |

## Related

- Base docs: [SingleContentHost](./single-content-host.md)
- Common element API: [Element](./element.md)
