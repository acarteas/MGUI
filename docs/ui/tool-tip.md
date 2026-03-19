# ToolTip

Popup window usually attached to another element and shown after a hover delay.

- XAML tag: `<ToolTip>`
- Runtime type: `MGUI.Core.UI.MGToolTip`
- Base type: [Window](./window.md)
- Element type: `MGElementType.ToolTip`
- Content model: Inherits `Window` content model, usually with a single `Content` child.
- Inherits shared API from [Window](./window.md).

## Notes

- Usually attached through `Element.ToolTip` rather than created as a stand-alone window.

## Direct Properties

| Property | Type |
| --- | --- |
| `ShowOnDisabled` | `bool?` |
| `ShowDelay` | `TimeSpan?` |
| `DrawOffset` | `Size?` |

## Related

- Base docs: [Window](./window.md)
- Common element API: [Element](./element.md)
