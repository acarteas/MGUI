# Overlay

Single-content overlay that can be opened, closed, and optionally wrapped in a border.

- XAML tag: `<Overlay>`
- Runtime type: `MGUI.Core.UI.MGOverlay`
- Base type: [SingleContentHost](./single-content-host.md)
- Element type: `MGElementType.Overlay`
- Content model: Single `Content` child that should be hosted by an `OverlayHost`.
- Inherits shared API from [SingleContentHost](./single-content-host.md).

## Notes

- Designed to be added through `OverlayHost.Overlays`.

## Direct Properties

| Property | Type |
| --- | --- |
| `Border` | `Border` |
| `IsOpen` | `bool?` |
| `CloseButton` | `Button` |
| `ShowCloseButton` | `bool?` |

## Related

- Base docs: [SingleContentHost](./single-content-host.md)
- Common element API: [Element](./element.md)
