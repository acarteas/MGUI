# Expander

Collapsible control that toggles a header and a single expandable content region.

- XAML tag: `<Expander>`
- Runtime type: `MGUI.Core.UI.MGExpander`
- Base type: [SingleContentHost](./single-content-host.md)
- Element type: `MGElementType.Expander`
- Content model: Single `Content` child plus optional `Header` and header-panel customization.
- Inherits shared API from [SingleContentHost](./single-content-host.md).

## Direct Properties

| Property | Type |
| --- | --- |
| `ExpanderToggleButton` | `ToggleButton` |
| `ExpanderButtonSize` | `int?` |
| `ExpanderButtonBorderBrush` | `BorderBrush` |
| `ExpanderButtonBorderThickness` | `Thickness?` |
| `ExpanderButtonExpandedBackgroundBrush` | `FillBrush` |
| `ExpanderButtonCollapsedBackgroundBrush` | `FillBrush` |
| `ExpanderDropdownArrowColor` | `XAMLColor?` |
| `ExpanderDropdownArrowSize` | `int?` |
| `HeaderSpacingWidth` | `int?` |
| `Header` | `Element` |
| `HeaderVerticalAlignment` | `VerticalAlignment?` |
| `IsExpanded` | `bool?` |
| `ExpandedVisibility` | `Visibility?` |
| `CollapsedVisibility` | `Visibility?` |
| `HeadersPanel` | `StackPanel` |

## Related

- Base docs: [SingleContentHost](./single-content-host.md)
- Common element API: [Element](./element.md)
