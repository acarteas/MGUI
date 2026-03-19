# OverlayHost

Hosts a primary content element plus zero or more overlays above it.

- XAML tag: `<OverlayHost>`
- Runtime type: `MGUI.Core.UI.MGOverlayHost`
- Base type: [SingleContentHost](./single-content-host.md)
- Element type: `MGElementType.OverlayHost`
- Content model: Single primary `Content` child plus an `Overlays` collection.
- Inherits shared API from [SingleContentHost](./single-content-host.md).

## Direct Properties

| Property | Type |
| --- | --- |
| `OverlayBackground` | `FillBrush` |
| `IsModal` | `bool?` |
| `Overlays` | `List<Overlay>` |

## Related

- Base docs: [SingleContentHost](./single-content-host.md)
- Common element API: [Element](./element.md)
