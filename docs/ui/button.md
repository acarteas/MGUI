# Button

Clickable single-content control that can invoke commands and optionally repeat while held.

- XAML tag: `<Button>`
- Runtime type: `MGUI.Core.UI.MGButton`
- Base type: [SingleContentHost](./single-content-host.md)
- Element type: `MGElementType.Button`
- Content model: Single `Content` child.
- Inherits shared API from [SingleContentHost](./single-content-host.md).

## Direct Properties

| Property | Type |
| --- | --- |
| `Border` | `Border` |
| `CommandName` | `string` |
| `IsRepeatButton` | `bool?` |
| `InitialRepeatInterval` | `TimeSpan?` |
| `RepeatInterval` | `TimeSpan?` |

## Related

- Base docs: [SingleContentHost](./single-content-host.md)
- Common element API: [Element](./element.md)
