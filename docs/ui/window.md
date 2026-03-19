# Window

Top-level or nested window container with chrome, sizing, dragging, and modal support.

- XAML tag: `<Window>`
- Runtime type: `MGUI.Core.UI.MGWindow`
- Base type: [SingleContentHost](./single-content-host.md)
- Element type: `MGElementType.Window`
- Content model: Usually a single `Content` child plus optional nested windows, modal windows, and title-bar elements.
- Inherits shared API from [SingleContentHost](./single-content-host.md).

## Notes

- Supports nested windows and modal windows in addition to a normal content body.

## Direct Properties

| Property | Type |
| --- | --- |
| `ThemeName` | `string` |
| `Left` | `int?` |
| `Top` | `int?` |
| `SizeToContent` | `SizeToContent?` |
| `Scale` | `float?` |
| `ResizeGrip` | `ResizeGrip` |
| `IsUserResizable` | `bool?` |
| `Border` | `Border` |
| `ModalWindow` | `Window` |
| `NestedWindows` | `List<Window>` |
| `TitleBar` | `DockPanel` |
| `TitleBarTextBlock` | `TextBlock` |
| `TitleText` | `string` |
| `IsTitleBarVisible` | `bool?` |
| `CloseButton` | `Button` |
| `IsCloseButtonVisible` | `bool?` |
| `CanCloseWindow` | `bool?` |
| `IsTopmost` | `bool?` |
| `AllowsClickThrough` | `bool?` |
| `IsDraggable` | `bool?` |
| `WindowStyle` | `WindowStyle?` |

## Related

- Base docs: [SingleContentHost](./single-content-host.md)
- Common element API: [Element](./element.md)
