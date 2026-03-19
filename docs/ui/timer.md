# Timer

Countdown display/control for remaining duration with pause and scaling options.

- XAML tag: `<Timer>`
- Runtime type: `MGUI.Core.UI.MGTimer`
- Base type: [Element](./element.md)
- Element type: `MGElementType.Timer`
- Content model: No child elements; display content is configured through the `Value` text block.
- Inherits shared API from [Element](./element.md).

## Direct Properties

| Property | Type |
| --- | --- |
| `Border` | `Border` |
| `Value` | `TextBlock` |
| `ValueDisplayFormat` | `string` |
| `RemainingDuration` | `TimeSpan?` |
| `RemainingDurationStringFormat` | `string` |
| `AllowsNegativeDuration` | `bool?` |
| `TimeScale` | `double?` |
| `IsPaused` | `bool?` |

## Related

- Base docs: [Element](./element.md)
- Common element API: [Element](./element.md)
