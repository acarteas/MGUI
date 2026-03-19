# PasswordBox

TextBox variant that masks entered characters with a password glyph.

- XAML tag: `<PasswordBox>`
- Runtime type: `MGUI.Core.UI.MGPasswordBox`
- Base type: [TextBox](./text-box.md)
- Element type: `MGElementType.PasswordBox`
- Content model: Text content is edited through inherited `TextBox` APIs; no child elements.
- Inherits shared API from [TextBox](./text-box.md).

## Notes

- Inherits the full `TextBox` surface and only adds password masking.

## Direct Properties

| Property | Type |
| --- | --- |
| `PasswordCharacter` | `char?` |

## Related

- Base docs: [TextBox](./text-box.md)
- Common element API: [Element](./element.md)
