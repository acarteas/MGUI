# TextBox

Editable text input control with selection, placeholder, resize, and history options.

- XAML tag: `<TextBox>`
- Runtime type: `MGUI.Core.UI.MGTextBox`
- Base type: [Element](./element.md)
- Element type: `MGElementType.TextBox`
- Content model: Content property is `Text`; supporting visuals are configured through dedicated child slot properties.
- Inherits shared API from [Element](./element.md).

## Direct Properties

| Property | Type |
| --- | --- |
| `Border` | `Border` |
| `TextBlock` | `TextBlock` |
| `Text` | `string` |
| `FontSize` | `int?` |
| `WrapText` | `bool?` |
| `MinLines` | `int?` |
| `MaxLines` | `int?` |
| `Placeholder` | `TextBlock` |
| `PlaceholderText` | `string` |
| `CharacterCounter` | `TextBlock` |
| `CharacterLimit` | `int?` |
| `ShowCharacterCount` | `bool?` |
| `LimitedCharacterCountFormatString` | `string` |
| `LimitlessCharacterCountFormatString` | `string` |
| `FocusedSelectionForegroundColor` | `XAMLColor?` |
| `FocusedSelectionBackgroundColor` | `XAMLColor?` |
| `UnfocusedSelectionForegroundColor` | `XAMLColor?` |
| `UnfocusedSelectionBackgroundColor` | `XAMLColor?` |
| `AllowsTextSelection` | `bool?` |
| `UndoRedoHistorySize` | `int?` |
| `IsReadonly` | `bool?` |
| `AcceptsReturn` | `bool?` |
| `AcceptsTab` | `bool?` |
| `IsHeldKeyRepeated` | `bool?` |
| `InitialKeyRepeatDelay` | `TimeSpan?` |
| `KeyRepeatInterval` | `TimeSpan?` |
| `TextEntryMode` | `TextEntryMode?` |
| `ResizeGrip` | `ResizeGrip` |
| `IsUserResizable` | `bool?` |

## Related

- Base docs: [Element](./element.md)
- Common element API: [Element](./element.md)
