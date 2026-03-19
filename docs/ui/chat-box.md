# ChatBox

Composite chat UI with current-user text, message history, input box, and send button.

- XAML tag: `<ChatBox>`
- Runtime type: `MGUI.Core.UI.MGChatBox`
- Base type: [Element](./element.md)
- Element type: `MGElementType.ChatBox`
- Content model: Composite control; configured through child slot properties such as `InputTextBox`, `SendButton`, and `MessagesContainer` rather than regular content.
- Inherits shared API from [Element](./element.md).

## Notes

- Each submitted message becomes a runtime `ChatBoxMessage` element.

## Direct Properties

| Property | Type |
| --- | --- |
| `Border` | `Border` |
| `MaxMessageLength` | `int?` |
| `MaxMessages` | `int?` |
| `TimestampFormat` | `string` |
| `CurrentUserTextBlock` | `TextBlock` |
| `InputTextBox` | `TextBox` |
| `SendButton` | `Button` |
| `Separator` | `Separator` |
| `MessagesContainer` | `ListBox` |

## Related

- Base docs: [Element](./element.md)
- Common element API: [Element](./element.md)
