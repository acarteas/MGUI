# Stopwatch

Displays elapsed time and can be driven as a running stopwatch.

- XAML tag: `<Stopwatch>`
- Runtime type: `MGUI.Core.UI.MGStopwatch`
- Base type: [Element](./element.md)
- Element type: `MGElementType.Stopwatch`
- Content model: No child elements; display content is configured through the `Value` text block.
- Inherits shared API from [Element](./element.md).

## Direct Properties

| Property | Type |
| --- | --- |
| `Border` | `Border` |
| `Value` | `TextBlock` |
| `ValueDisplayFormat` | `string` |
| `Elapsed` | `TimeSpan?` |
| `TimeScale` | `double?` |
| `IsRunning` | `bool?` |

## Related

- Base docs: [Element](./element.md)
- Common element API: [Element](./element.md)
