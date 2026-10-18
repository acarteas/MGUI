# Render caching

MGUI provides two explicit ways to reuse pixels: `MGCachedFillBrush` for an eligible background fill, and `MGRenderCache` for the content of one wrapper. Both keep normal rendering as the fallback. Caching does not create a bitmap for every element or change the painter order of ordinary siblings.

## Choose a cache boundary

Use `CachedFillBrush` when one fill layer is stable and worth reusing while the element and its other paint stages remain ordinary. It wraps one `IFillBrush` and works at supported built-in background fill sites. A direct call to `MGCachedFillBrush.Draw`, or a cached wrapper nested inside another brush without an identified paint site, draws the source normally.

Use `RenderCache` when a bounded group of content paints together and its subtree is eligible. The wrapper caches only `Content`; its own background, border, components and other wrapper decoration stay in their normal paint stages. Elements inside the content still update, receive input, participate in layout, and keep their ordinary state. A hit skips their captured drawing until their visual state changes.

Put changing content next to the cached group in its intended painter order. A cache is a single rectangle of pixels; it cannot leave a transparent “hole” for an arbitrary live child that belongs inside the captured subtree.

```xml
<Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" Width="520" Height="380">
    <StackPanel Orientation="Vertical" Spacing="8">
        <RenderCache Name="StaticPanel">
            <Border Padding="12" Background="SlateBlue">
                <StackPanel Spacing="4">
                    <TextBlock Text="Stable navigation" IsBold="True" />
                    <TextBlock Text="Home  /  Library  /  Settings" />
                </StackPanel>
            </Border>
        </RenderCache>

        <!-- Ordinary sibling: it keeps drawing and updating in this painter position. -->
        <TextBlock Name="LiveStatus" Text="Score 001200" IsRenderVolatile="True" />

        <Border Name="TrackedFill" Height="24">
            <Border.Background>
                <CachedFillBrush SourceMode="Tracked">
                    <SolidFillBrush Color="SlateBlue" />
                </CachedFillBrush>
            </Border.Background>
        </Border>

        <Border Name="AuditedStaticFill" Height="24">
            <Border.Background>
                <CachedFillBrush SourceMode="ExplicitlyStatic">
                    <SolidFillBrush Color="Teal" />
                </CachedFillBrush>
            </Border.Background>
        </Border>
    </StackPanel>
</Window>
```

The runnable XAML-first sample is [MGUI.Samples/Features/RenderCaching.xaml](../../MGUI.Samples/Features/RenderCaching.xaml), with its runtime commands and update logic in [RenderCaching.xaml.cs](../../MGUI.Samples/Features/RenderCaching.xaml.cs). It shows normal drawing, background caching and a content boundary, plus a live score, explicit invalidation, volatility, a changing subtree and disposal.

## XAML and runtime APIs

`RenderCache` is the XAML wrapper for `MGUI.Core.UI.Containers.MGRenderCache`. `Content` is its content property; an empty wrapper is valid and simply draws normally. `IsCacheEnabled` is nullable in XAML and defaults to `true` on an explicitly created wrapper. Setting it to `false` retires that boundary's pixels. `MGRenderCache.LastBypassReason` reports the latest ordinary content draw's eligibility result; `None` means the source/context passed those checks, not that a ready texture exists. A skipped paint site leaves the diagnostic unchanged.

`CachedFillBrush` materializes `MGUI.Core.UI.Brushes.Fill_Brushes.MGCachedFillBrush`. It requires one `Brush`, supplied either as child content or by the `Brush` property. XAML `IsCacheEnabled` is nullable and defaults to `true`; the runtime wrapper also defaults to enabled. `MGRenderCacheSourceMode` in `MGUI.Core.UI.Brushes.Fill_Brushes` has the stable values `Tracked = 0` and `ExplicitlyStatic = 1`.

`MGElement` exposes read-only `VisualRevision`, `LayoutRevision` and `StructureRevision`, plus `InvalidateVisual()` and `IsRenderVolatile`. `IsRenderVolatile` defaults to `false` at runtime and is nullable in XAML; setting it to `true` marks the element live and prevents enclosing content reuse. Normal semantic changes advance the appropriate revision; callers do not set revision values. Visual changes, layout/geometry changes and child/paint-structure changes are tracked separately. `InvalidateVisual()` signals a visual change without requesting layout.

A custom element can opt into a containing cache by overriding the protected `SupportsRenderCaching` getter. This is a promise that its paint is deterministic, bounded, translation-safe and free of draw side effects, framebuffer dependencies, target switching and untracked external state. Call `InvalidateVisual()` when an opaque dependency changes or when the getter's answer changes. Unknown custom subclasses are unsupported by default. `IsRenderVolatile` is the simpler choice for content that changes continually: the element paints normally and prevents enclosing content reuse. Setting the flag back to `false` does not override intrinsic volatility of a running control.

## Cached fill source modes and invalidation

`Tracked` is the default for `MGCachedFillBrush`. Known brush/resource adapters observe supported changes; a custom source can implement [`MGUI.Core.UI.IMGRenderCacheSource`](../../MGUI.Core/UI/Rendering/MGRenderCacheSourceDependencies.cs) to expose `VisualRevision`, `VisualInvalidated`, `SupportsRenderCaching` and `IsRenderVolatile`. Unknown sources and effect-backed brushes are unsupported in tracked mode.

`ExplicitlyStatic` is a caller assertion about the entire wrapped source graph. Use it only when every draw produces bounded, premultiplied local/UV-based pixels with no time, screen-position, interaction or hidden mutable-state dependency. This mode lets a caller accept an otherwise untracked static source, such as a fixed effect or custom brush. It does not override known volatility, disposed resources, unbounded paint, invalid layout or an unsupported draw context. If output changes over time, leave it live or mark its owner volatile.

Observable property/source changes invalidate tracked pixels automatically. For opaque mutations that MGUI cannot observe, call `InvalidateVisual()` before the next draw:

```csharp

// The source changes through an API MGUI cannot observe.
texture.SetData(updatedPixels);
cachedBrush.InvalidateVisual();

// An element depends on external state outside its tracked properties.
element.InvalidateVisual();
```

`MGCachedFillBrush.InvalidateVisual()` reconciles the source graph and invalidates all of that wrapper's live owner/paint-site registrations. Use the element method for a raw brush or another external dependency; if several elements consume that raw source, invalidate each consuming element. In-place edits to the public `MGCompositedFillBrush.Brushes` or `MGCompositedBorderBrush.Brushes` lists are not observed. The same explicit invalidation rule applies to `Texture2D.SetData`, caller render-target content changes, direct `Effect` parameter writes, and callbacks that read external state. Known changes through the wrapper's `Brush`, `IsCacheEnabled` and `SourceMode` properties invalidate automatically.

MGUI does not own or dispose caller brushes, textures, effects, fonts or content resources. XAML `StaticResource` and named resource lookups materialize references: removing a resource and adding a different object under the same name does not rebind an existing element. Replace the consumed brush or invalidate/mutate the object that the element already references.

## Prepare and draw in this order

After each completed `MGDesktop.Update()`, optionally call `MGDesktop.PrepareRenderCaches()` before clearing or drawing the host scene. Then draw the world and call `MGDesktop.Draw()` at its normal composition point:

```csharp
desktop.Update();
desktop.PrepareRenderCaches(); // Optional; before clearing or drawing the scene.

graphicsDevice.SetRenderTarget(null);
graphicsDevice.Clear(Color.CornflowerBlue);
DrawWorld();
desktop.Draw();
```

Call preparation outside every MGUI `SpriteBatch`, `PrimitiveBatch` and `DrawTransaction` session. MGUI can detect its own active transactions; MonoGame has no public query for a raw host batch, so the host must ensure those batches are ended. Do not call preparation while a host render target is bound: MGUI leaves it untouched and does not capture there. Rebinding a discard-content target cannot preserve scene pixels.

Preparation is optional, and each `Update` permits one attempt. A cache is usable only after preparation for the current update. First use paints normally and records an observation; a later preparation may capture that observation. If preparation is omitted, called before an update, or cannot run in its supported context, the UI draws directly. A late invalidation, resize, reparent, changed source generation or stale capture also draws directly until fresh pixels are prepared. Do not draw first and prepare afterward: preparation must happen before the scene is valuable.

Preparation restores publicly readable graphics state. MonoGame exposes no public getter for the bound shader/effect program or vertex-buffer bindings; after preparation the host must bind its own buffers and apply its own effect before continuing scene rendering, as it normally does when starting a SpriteBatch. Cache captures do not change caller effect parameters or transactions. A producer paint exception is cleaned up and propagated; native target construction/first-bind failures reject that target and fall back to normal painting.

`MGDesktop.RenderCaches` exposes the desktop-owned `MGRenderCacheManager`. Defaults are `MaximumRetainedBytes = 33_554_432` (32 MiB), `MaximumEntries = 64` and `MaximumTargetDimension = 4096`. The dimension is an allocation policy cap, not a hardware guarantee. Cache targets use four bytes per pixel; allocated, retired and in-flight targets count until actually disposed. The manager applies bounded replacement/eviction and reports `RetainedBytes`, `EntryCount`, `Statistics`, `LastNativeFailure` and per-reason `GetBypassCount(...)`. A zero budget/entry limit disables capture. If a source cannot fit or target creation fails, its paint remains live. `Clear()` releases cache entries at a safe point. `DisposeRenderCaches()` permanently disposes the desktop cache service; accessing `RenderCaches` does not recreate it. Device reset clears cached generations, and device disposal disposes the manager. Dispose caches before disposing a desktop's graphics device when you control that lifetime.

## Eligibility and visible behavior

Content boundaries are conservative. A cache needs positive resolved bounds, attached ownership, clipped paint within the boundary, tracked eligible content, and a supported draw context. Unknown element types, public draw callbacks in captured content, volatile participants, unresolved layout, unbounded paint, and unsupported resources draw directly. The built-in participation set is an exact runtime-type allowlist; deriving a custom class from an admitted built-in does not inherit admission.

Current context checks also bypass unsupported transforms, partial inherited clips, invalid viewport/geometry and inherited opacity other than `1`. This preserves the original per-element alpha layering; flattening a partially transparent subtree into one texture can change its compositing. `RenderScale` is eligible only where its resolved transform meets the supported translation/context checks (in practice, non-unit element render scaling bypasses). `MGDesktop.UIScale` and a resolved per-window UI layout scale are layout inputs, not the post-layout `MGWindow.Scale` render transform; a scale change invalidates layout/visual observations and requires new pixels. Scroll viewers and other clipped content use the ordinary path whenever the inherited screen clip cuts through a cache boundary.

A background-only cached fill checks its own source and relevant owner/context state; it does not require unrelated owner content to qualify. `MGRenderCache` checks its captured content and keeps wrapper decorations, public hooks, input and other paint stages at their original locations. Nested boundaries prepare independently; during an outer capture, descendants paint directly so one cache is never sampled into another capture. Normal drawing preserves sibling order, so place a live element before or after a boundary according to the desired painter order.

`LastBypassReason`, manager statistics and bypass counts are diagnostics, not a promise that caching will improve a workload. Pixel reuse does not reduce `Update`, input, focus, layout, accessibility or observation work. Continuously changing, clipped or otherwise unsupported content stays on the ordinary rendering path.

## What the matched comparison shows

The [T23 matched comparison report](render-caching-comparison.md) compares normal rendering, an identical-code normal-batching control, 36 cached fill sites and one explicit panel boundary across synthetic fixtures. Each mode within a fixture produced identical pixels, with Update and input active. Eleven of twelve stable or single-change panel/configuration pairs passed the timing benefit gate. One layered 1080p/scale-1.5 pair is inconclusive: its 0.98545 ms median saving does not exceed twice the 1.36780 ms reference repeat-median spread. Trace inspection found no batching change to justify; normal-batching was an identical-code control.

Caching 36 small cell backgrounds was slower than normal in every measured configuration. Dirty and scrolling fixtures showed no useful cache benefit and remained live. In these fixtures, the useful granularity was a stable panel boundary; this result does not predict the right boundary for a particular application. Profile the target content and keep a cache only where equivalent-pixel measurements show that its warm savings repay preparation and capture costs.

The 216 captures used MonoGame DesktopGL 3.8.4.1 Release on Linux with Mesa llvmpipe software rendering. The final 120 resumed captures ran under shared external load; the original 96 accepted captures have no retrospective load journal. They measure CPU update/submission and wall-time Present phases; Present is not GPU execution time. The results do not establish hardware FPS, gameplay frame rate or behavior of a whole application's UI. The comparison report contains the workload table, method and limitations.

See the [approved caching contract](render-caching-design.md), [eligibility details](render-caching-eligibility.md), [source mutation rules](render-cache-source-mutation.md), [preparation protocol](render-cache-preparation.md), [content boundary notes](render-cache-boundary.md), and the focused [cached fill brush page](cached-fill-brush.md).
