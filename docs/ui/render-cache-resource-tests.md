# Render-cache resource service and validation

`MGDesktop.RenderCaches` is one CPU-only `MGRenderCacheManager` created with its desktop. No cache target is constructed by registration, queueing or lookup. The service owns only its allocated `RenderTarget2D` instances. `DisposeRenderCaches()` permanently disables the same service; it does not own the renderer, device, or supplied textures/effects.

## Internal producer protocol

`Register(owner, stableOperationToken)` returns a registration keyed by both object identities. The owner and operation are weakly held, including in queued/ready work. A shared brush must use distinct operation tokens for distinct paint sites; the operation should expose producer validation/capture capability for the preparation coordinator without putting an owner-capturing delegate in the queue.

Before any direct draw or semantic invalidation/detach/disable, call `registration.Invalidate()`. This changes its generation, removes its pending request and makes ready targets unusable immediately. It only retires targets; deferred final-draw sprites may still reference them. After a successful eligible normal draw, `Queue(registration, key)` records the immutable observation. It coalesces the newest key for an existing operation in insertion order and admits at most `MaximumEntries` distinct pending operations. The key includes the revision epoch and visual/layout/structure/source revisions, observation/context/layout-resolution tokens, full bounds/offset, physical target dimensions, visual state, opacity and transform. Producers must advance tokens for relevant state not represented by the explicit fields; the manager does not discover UI eligibility or tree state.

Warm `TryGet` is a fixed number of generation/key/liveness checks and a registration dictionary lookup. It never allocates, binds or disposes. The desktop preparation coordinator additionally gates this lookup on successful preparation in the current completed-Update frame; the resource manager does not implement the host frame protocol.

At a transaction-free pre-scene point, enter `BeginPreparation()`. A null scope means disposed or reentrant work. The scope sweeps dead registrations and drains retired targets. Dequeue immutable requests with `TryDequeue`; resolve their weak owner/operation, validate producer state, then call `TryAllocate`. Native construction uses RGBA8, no mipmaps/depth/multisampling, and DiscardContents. The 4096 dimension default is a policy cap; native construction and the coordinator's first bind/clear validate actual backend limits. There is one active candidate at a time, charged together with ready/retired targets against both limits. Replacement and LRU eviction release old targets before construction.

Validate immediately before paint and after paint. End/flush the capture transaction and unbind its target before calling `candidate.Publish()`, `candidate.Dispose()` or `candidate.RejectNativeFailure(exception)`. First-bind/clear exceptions must use `RejectNativeFailure` after state restoration. A failed size is suppressed until size/device/policy changes; Clear alone does not retry it. `Publish` checks service, registration and revision epochs again, but does not replace producer snapshot validation. Dispose the preparation scope after every candidate is released. The scope refuses to end while a lease remains active.

`Clear`, explicit disposal and budget/dimension setters require the UI/render thread outside final external transactions. Policy mutation during preparation or target retirement throws. Reentrant Clear/Dispose during capture cancel the generation immediately and retain all sampled/bound targets until the candidate is released after flushing/unbinding. Resource-disposal callback reentrancy cannot double-release targets or start another preparation. Logical retirement remains charged until actual disposal. Device reset invalidates generations/resources; device disposal disables the service. Weak device event forwarding cannot root an abandoned desktop.

## Running tests

On Linux, run the complete suite directly with only the build lock:

```sh
flock --close /tmp/mgui-render-cache-dotnet.lock dotnet test MGUI.Tests/MGUI.Tests.csproj -c Release
```

The Linux project builds current source dependencies before tests. Each native xUnit wrapper uses `NativeGraphicsHost` and `tools/render-cache/test-native.py`: the child acquires `/tmp/mgui-render-cache-graphics.lock`, owns a private Xvfb display, reports `glxinfo -B`, runs an already built host, then tears down its server. Both effect and resource hosts use this path. Do not wrap `dotnet test` in `benchmark.native_display()` or hold the graphics lock around it: native children acquire that lock themselves. The benchmark command continues to own its display separately and its accepted fixtures/captures are unchanged. Other platforms launch the native host directly.

For standalone native resource evidence, first build under the build lock, release it, then run:

```sh
flock --close /tmp/mgui-render-cache-dotnet.lock dotnet build MGUI.RenderCacheTestHost/MGUI.RenderCacheTestHost.csproj -c Release
python3 tools/render-cache/test-native.py MGUI.RenderCacheTestHost/bin/Release/net8.0/MGUI.RenderCacheTestHost.dll --resources
```

The pure policy suite uses an internal allocation seam and proves LRU/ties, both limits including in-flight/retired storage, checked sizes, weak identities, queue coalescing and epochs, failure suppression, and reentrant lifecycle safety. The resource host also uses real native targets, first bind/clear/readback, SpriteBatch sampling during Clear/Dispose, live device reset/disposal, independent devices/desktops, weak event roots, caller-resource ownership and target disposal event counts. Its synthetic post-bind exception tests the failure cleanup protocol after a genuine native bind; it does not claim to exhaust hardware memory. Mesa llvmpipe runs establish graphics correctness, not hardware FPS.
