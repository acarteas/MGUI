# ChatBoxMessage

Runtime-only element that renders one message row inside a `ChatBox`.

- XAML tag: `runtime-only`
- Runtime type: `MGUI.Core.UI.MGChatBoxMessage`
- Base type: [Element](./element.md)
- Element type: `MGElementType.ChatBoxMessage`
- Content model: Not authored directly in XAML. Instances are created by `MGChatBox` as messages are added.
- Inherits shared API from [Element](./element.md).

## Notes

- This type exposes the generated text elements for the timestamp, username, and message body.

## Direct Properties

| Property | Type |
| --- | --- |
| `ChatBox` | `MGChatBox` |
| `MessageData` | `ChatBoxMessageData` |
| `Username` | `string` |
| `Timestamp` | `DateTime` |
| `Message` | `string` |
| `TimestampTextBlock` | `MGTextBlock` |
| `UsernameTextBlock` | `MGTextBlock` |
| `MessageTextBlock` | `MGTextBlock` |

## Related

- Base docs: [Element](./element.md)
- Common element API: [Element](./element.md)
