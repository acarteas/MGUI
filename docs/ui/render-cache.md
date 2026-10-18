# Render cache

`MGUI.Core.UI.Containers.MGRenderCache` is an explicit single-content wrapper. Ordinary sibling elements remain live in their painter order, and the wrapper's own background, decorations and public callbacks keep their normal paint stages. Only eligible `Content` pixels are reused.

XAML `IsCacheEnabled` is nullable and defaults to `true` on this explicit wrapper. The flag supports ordinary styles, including `BasedOn`, and `MGBinding` from window data context, element names and resource names. Explicit values follow the normal style precedence rules. `Content` uses the usual parent ownership, namescope, binding initialization and data-context propagation. Applying settings with content excluded preserves existing content while still applying an explicitly supplied cache flag.

Preparation is optional. Call `desktop.PrepareRenderCaches()` after update and before clearing or drawing the scene. Omitting it or changing state after it causes direct painting until a fresh observation is prepared. The wrapper's drawing path leaves input, update, layout and other paint stages ordinary.

See the complete [render caching guide](render-caching.md) for APIs, invalidation and lifecycle details. The runnable [XAML sample](../../MGUI.Samples/Features/RenderCaching.xaml) shows the content wrapper beside live content. Pure [RenderCache XAML tests](../../MGUI.Tests/XAML/RenderCacheXamlTests.cs) cover syntax/defaults and invalid input; the [native parser test](../../MGUI.Tests/Rendering/RenderCacheXamlNativeTests.cs) checks runtime trees, styles, content exclusion and live binding replacement.

For qualified performance evidence, see the [matched comparison report](render-caching-comparison.md). Its synthetic panel fixtures often passed the timing gate, while dirty and scrolling content stayed live; these results do not predict whole-application FPS or a game's UI behavior.
