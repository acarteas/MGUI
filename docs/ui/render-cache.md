# Render cache

`RenderCache` is an explicit single-content wrapper. It materializes `MGUI.Core.UI.Containers.MGRenderCache`; ordinary sibling elements remain live and retain their painter order. Its content can contain normal text, images, controls and nested containers.

```xml
<StackPanel>
  <RenderCache IsCacheEnabled="True">
    <StackPanel>
      <TextBlock Text="Stable panel" />
      <Image SourceName="PanelIcon" />
      <Button Content="Action" />
    </StackPanel>
  </RenderCache>
  <TextBlock Text="Live status" IsRenderVolatile="True" />
</StackPanel>
```

`Content` is the content property, so explicit `<RenderCache.Content>` syntax also works. Empty content is valid. `IsCacheEnabled` is nullable in XAML; omitting it preserves the runtime default `true`. `IsRenderVolatile` is a nullable common element property; omitting it preserves runtime `false`. Setting it to `true` keeps that element's paint live and prevents enclosing cache reuse. Setting it to `false` does not override intrinsic volatility of running controls.

Both flags support ordinary styles (including `BasedOn` inheritance) and `MGBinding`, including live window data context, element-name and resource-name sources. Explicit values take precedence over style setters according to existing style behavior. Content uses normal parent ownership, name scopes, binding initialization and data context propagation. Applying settings with content excluded preserves existing content and still applies explicitly supplied flags.

Call `desktop.PrepareRenderCaches()` after normal update and before scene drawing, outside draw transactions. The wrapper's own decorations remain ordinary drawing; only eligible content pixels are reused. Unsupported content or contexts draw directly. See the [caching contract](render-caching-design.md) and [eligibility rules](render-caching-eligibility.md) for preparation, invalidation and disposal requirements. Caching adds no update, input or layout shortcut.

Validation: the pure `RenderCacheXamlTests` lane checks content syntax, nullable flags, inherited styles and invalid input. `RenderCacheXamlNativeTests` uses a real MonoGame DesktopGL host to check parsed runtime trees/defaults/styles, content exclusion and live binding replacement through the normal parser lifecycle. Its helper owns a private Xvfb display and shared graphics lock on Linux.
