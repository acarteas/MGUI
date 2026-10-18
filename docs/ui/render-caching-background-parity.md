# Cached-background native parity coverage

`MGUI.RenderCacheTestHost --background-parity` is a correctness lane for `MGCachedFillBrush`. `BackgroundParityNativeTests` in `MGUI.Tests` launches the genuine MonoGame DesktopGL host, requires its named checks, validates every full-frame RGBA comparison, and checks that the A/B PNGs and JSON report exist. Native failures fail xUnit. This fixture does not modify the accepted benchmark fixture or its historical artifacts.

Run current Release source under the shared build lock:

```bash
flock --close /tmp/mgui-render-cache-dotnet.lock dotnet test MGUI.Tests/MGUI.Tests.csproj -c Release --filter FullyQualifiedName~BackgroundParityNativeTests --logger 'console;verbosity=detailed'
```

The Linux `NativeGraphicsHost` helper owns its private Xvfb display and graphics lock. Do not hold an outer graphics lock. For an already built host and full console evidence:

```bash
python3 tools/render-cache/test-native.py MGUI.RenderCacheTestHost/bin/Release/net8.0/MGUI.RenderCacheTestHost.dll --background-parity
```

Every update publishes the same simulation timestamp (1/60 second); matched A/B inputs therefore use a fixed clock while normal desktop update tokens continue advancing. The `DIFFREPORT:` line points to `/tmp/mgui-background-parity-<pid>/report.json`; the adjacent files contain every normal/cached image pair and any difference image amplified 64 times. Reports include built host/Core assembly SHA-256 values, backend/configuration, per-channel error maxima and pixel counts, first differing coordinates, explicit per-case allowances, named results, target counts and warm-frame counters. The helper records `glxinfo -B`. The verified environment is MonoGame DesktopGL, Release, Mesa 26.2.3 llvmpipe (LLVM 23.1.1), owned Xvfb, Linux. These results establish software-renderer correctness and make no hardware FPS claim.

## Matrix

| Area | Native evidence |
| --- | --- |
| Layered source | Existing compiled effect mode 101 supplies bounded local/UV color lighting; nine-slice texture, nested theme-derived colors and a translucent effect tint paint in order. First direct miss and subsequent warm hits compare at UI layout scales 1, 1.5 and 2. |
| Effect-bearing nine-slice | The actual `MGNineSliceFillBrush(Effect, ...)` path uses standard whole-element slice-coordinate mapping. UV-gradient mode 101 and texture-sampling mode 100 compare at all three scales; no new shader or effect API is introduced. Tracked mode bypasses this effect graph; asserted static mode admits it. |
| Owner/site identity | One wrapper serves two real constructed owners with different origins and dimensions, then equal dimensions and different visual states. Each owner paints an underlay and overlay: four independent targets/hits are required. Copy policy and independent wrapper/source identities are checked; changing one copy's mode leaves the sibling wrapper's two sites eligible. |
| Geometry | UI scale replacement, window translation, owner/window resize, normal bounds and partial inherited clipping compare. Caching stays enabled through scale, translation and resize settlement; affected-owner target references must be disposed at safe preparation before any normal-reference toggle can retire them independently. |
| Source invalidation | Direct `EffectParameter.SetValue`, `Texture2D.SetData` and raw `MGCompositedFillBrush.Brushes` edits each visibly alter pixels. Explicit invalidation rejects pending observations before Prepare and ready entries after Prepare; direct fallback and rebuilt pixels compare. Effect-bearing nine-slice parameter writes have their own before/late checks. Nested observable color-brush replacement invalidates without explicit wrapper notification. |
| Eligibility | Default Tracked effect bypass, owner volatility, known volatile source overriding ExplicitlyStatic, recovery after removal, copy and SourceMode changes. |
| Final context | Fractional incoming opacity, custom outer BasicEffect, additive blend, linear sampler, scaled transform, fractional translation and partial clip all paint directly. Hits and pending requests are checked immediately after the unsupported cached-context draw, before disabling caching for the reference. Every final draw checks target-switch and allocation counters. |
| Scene | A changing world sentinel outside the UI must change between frames and remain identical to the matched normal draw. Both transparent and opaque clear colors are exercised with genuinely translucent source layers. |
| Warm/resource lifetime | Twenty-four Update/Prepare/Draw frames require 96 site hits with unchanged builds and allocations. Sampled targets survive invalidation while their draw transaction is active, then retire at the safe preparation point. Entry, positive/zero byte and dimension budgets enforce limits and recover. |
| Failure/cleanup | A controlled allocator constructs, binds, clears and reads back a genuine target, disposes it and throws. Cache allocation failure is diagnosed and suppressed by size; direct pixels remain correct and a policy change recovers. Actual presentation-reset events release targets and allow rebuilding. Explicit desktop cleanup permanently disables the same service, releases targets, and leaves caller textures/effects/device alive. Subsequent real game/device disposal completes without retained cache targets. |

Each top-level source layer has an independent omission control: removing that layer must alter more than 100 pixels by over eight channel levels. This keeps the narrowly quantified allowances below from concealing missing layers.

## Exactness and quantified intermediate rounding

All first-miss comparisons, fallback comparisons, ordinary layered opaque comparisons, texture-sampling effect-nine-slice controls and transparent layers over a transparent scene require exact RGBA equality. No image region is masked, and there is no percentage-of-image allowance.

Two named classes of warm comparison have a one-byte allowance, specified per channel:

* `transparent-layers-over-opaque-scene`: RGB may differ by one; alpha remains exact. Normal rendering blends individual translucent layers over the scene. Cached rendering first quantizes those layers into an RGBA8 transparent target and blends that quantized intermediate over the scene. The same layers over transparent clear compare exactly, isolating the additional scene-blend rounding. The observed difference affects G and B only; R and A remain exact.
* `effect-bearing-nine-slice-local-uv-scale-1.5-warm` and `effect-bearing-nine-slice-local-uv-scale-2-warm`: the mode-101 shader maps interpolated slice UV into R/G output. Only R/G may differ by one; B/A remain exact. Capture uses a different orthographic projection/viewport, so floating interpolation can fall on the opposite side of an 8-bit conversion threshold. The same geometry and scales with the existing mode-100 texture-sampling shader must match exactly. First-miss paths also remain exact.

The report records actual counts and maxima separately for every case, including cases whose permitted maximum is not reached. Normal, cached and amplified difference PNGs are retained for inspection. These allowances describe this fixed native fixture; they do not promise a universal error bound for arbitrary shaders or translucent graphs, and they do not relax fractional-opacity bypass.

The accepted local run has 86 comparisons: 83 exact, plus the three named rounded cases above. It observes and disposes 236 cache targets. The warm interval holds builds/allocations at 216/216 and advances hits from 218 to 314. Omission controls produce 31,800, 5,856 and 25,944 pixels respectively with a channel error above eight.

| Nonexact comparison | Differing pixels | Max RGBA error | Differing pixels per RGBA channel |
| --- | ---: | --- | --- |
| Translucent layers over opaque scene | 31,800 | 0, 1, 1, 0 | 0, 5,856, 31,800, 0 |
| UV-lit effect nine-slice, UI scale 1.5, warm | 846 | 1, 1, 0, 0 | 752, 101, 0, 0 |
| UV-lit effect nine-slice, UI scale 2, warm | 28 | 1, 0, 0, 0 | 28, 0, 0, 0 |

The normal/cached images and amplified differences for these three cases were visually inspected: layer shapes and gradients coincide; only the quantified color-level differences remain. Fresh reports remain the authority if a different backend produces a different distribution within the explicit per-channel bounds.

## Evidence limits

The failure is deliberately injected after genuine native target operations; it does not simulate a real driver allocation failure or hardware out-of-memory event. `GraphicsDevice.Reset()` on this DesktopGL backend resets presentation and raises real reset events. It does not reproduce graphics-context loss or driver/device removal; those remain an explicit evidence gap. Source-mode assertions remain the caller's responsibility: this lane supplies local/UV-only effects, and never treats a deliberately false static assertion as supported behavior. No input, layout, update or gameplay rule behavior is skipped or claimed as a cache benefit.
