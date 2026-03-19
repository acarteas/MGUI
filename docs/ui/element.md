# Element

Abstract base for all XAML-authored UI elements in MGUI.

- XAML tag: `abstract`
- Runtime type: `MGUI.Core.UI.MGElement`
- Base type: `XAMLBindableBase`
- Content model: No default child content. Concrete element types define their own content model.

## Notes

- These properties are inherited by nearly every concrete element page in this docs set.
- Attached layout properties such as `Dock`, `Row`, `Column`, `Offset`, and `ZIndex` are defined here.

## Direct Properties

| Property | Type |
| --- | --- |
| `Name` | `string` |
| `Margin` | `Thickness?` |
| `Padding` | `Thickness?` |
| `HorizontalAlignment` | `HorizontalAlignment?` |
| `VerticalAlignment` | `VerticalAlignment?` |
| `HorizontalContentAlignment` | `HorizontalAlignment?` |
| `VerticalContentAlignment` | `VerticalAlignment?` |
| `MinWidth` | `int?` |
| `MinHeight` | `int?` |
| `MaxWidth` | `int?` |
| `MaxHeight` | `int?` |
| `Width` | `int?` |
| `Height` | `int?` |
| `ToolTip` | `ToolTip` |
| `ContextMenu` | `ContextMenu` |
| `CanHandleInputsWhileHidden` | `bool?` |
| `IsHitTestVisible` | `bool?` |
| `IsSelected` | `bool?` |
| `IsEnabled` | `bool?` |
| `BackgroundRenderPadding` | `Thickness?` |
| `Background` | `FillBrush` |
| `Overlay` | `FillBrush` |
| `DisabledBackground` | `FillBrush` |
| `SelectedBackground` | `FillBrush` |
| `BackgroundFocusedColor` | `XAMLColor?` |
| `TextForeground` | `XAMLColor?` |
| `DisabledTextForeground` | `XAMLColor?` |
| `SelectedTextForeground` | `XAMLColor?` |
| `Visibility` | `Visibility?` |
| `ClipToBounds` | `bool?` |
| `Opacity` | `float?` |
| `RenderScale` | `float?` |
| `Dock` | `Dock` |
| `Row` | `int` |
| `Column` | `int` |
| `RowSpan` | `int` |
| `ColumnSpan` | `int` |
| `GridAffectsMeasure` | `bool` |
| `Offset` | `Thickness` |
| `ZIndex` | `double?` |
| `IsStyleable` | `bool` |
| `Styles` | `List<Style>` |
| `StyleNames` | `string` |
| `AttachedProperties` | `Dictionary<string, object>` |
| `Tag` | `object` |

## Related

