# InputConsumer

Intercepts selected mouse input so it does not continue to underlying UI.

- XAML tag: `<InputConsumer>`
- Runtime type: `MGUI.Core.UI.MGInputConsumer`
- Base type: [SingleContentHost](./single-content-host.md)
- Element type: `MGElementType.InputConsumer`
- Content model: Single `Content` child.
- Inherits shared API from [SingleContentHost](./single-content-host.md).

## Direct Properties

| Property | Type |
| --- | --- |
| `HandlesMousePresses` | `bool?` |
| `HandlesMouseReleases` | `bool?` |
| `HandlesMouseDrags` | `bool?` |
| `HandlesMouseScroll` | `bool?` |

## Related

- Base docs: [SingleContentHost](./single-content-host.md)
- Common element API: [Element](./element.md)
