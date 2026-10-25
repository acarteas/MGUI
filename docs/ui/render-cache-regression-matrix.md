# Final native pixel and interaction matrix

Integrated Release correctness evidence, 2026-10-18. The final six-cell theory and all relevant specialized native lanes pass; the isolated preliminary record is retained below to make the validation history and later deterministic caret correction explicit.

The new `--matrix WIDTH HEIGHT SCALE` lane reuses `LiveSiblingTests` and `LiveSiblingFixture` at six physical-surface/layout-scale combinations: 1280×720 and 1920×1080, each at 1, 1.5 and 2. Layout scale is `SetUniformUIScale`, not bitmap magnification or `Window.Scale`. Existing `--live-siblings` and the normal-path fixture keep their original 640×480 defaults and authored geometry. Matrix windows reserve an 81×71 pixel guard band so the deliberately scaled root cannot legitimately cover the corner sentinels and provide sufficient physical room for the scale-2 controls; input targets must have positive screen bounds entirely inside the physical surface and the full target rectangle inside the element's clipped screen bounds. `MGElement.Update` stores `ActualLayoutBounds` in `UnscaledScreen`; `RequireReachable` converts it explicitly to `Screen` before testing full containment of the layout-to-screen rectangle.

`RenderCacheMatrixNativeTests` has one theory case per cell. Each process completes three genuine `EndDraw`/presentation calls before constructing its test fixtures. Requested presentation dimensions and viewport must agree, but these alone do not establish a readable drawable. Every subsequent direct and cached paint submits four distinct 11×11 opaque native sprite patterns at the exact physical corners, after clear and after pre-scene Prepare. Every one of those 484 pixels must equal its independently known color after UI composition. Colors advance with the matched simulation clock. A truncated/black physical surface, scene discard or stale world therefore cannot pass because direct and cached images happen to match each other.

Both fixture instances receive identical real `InputTracker` mouse/keyboard state and 20ms simulation ticks. `MGTextCaret.Draw` uses `DateTime.Now`; matrix-only fixtures therefore set its public `BlinkRate` to one day, holding the visible blink phase throughout the bounded 90-second run whenever the existing focus and position gates permit caret painting. This preserves real focus, selection, typed input, normal updates and intrinsic caret volatility while excluding independent wall-clock blink phase. The old live-sibling fixture keeps its default blink rate. Their observable state/event snapshots agree before the reference paint. Every complete native frame must have exact RGBA equality, zero final UI target switches and zero final UI allocations. Every matrix comparison retains direct/cached PNGs and an amplified difference PNG, with exact direct/cached bytes encoded once then copied to both paths and a shared black difference PNG for each resolution, and records paths, maxima, pixel counts, hash, matched clock ticks and observable input/state snapshot in `report.json`. Pulse/text/image omission controls inspect UI pixels separately from the changing world.

The matrix adds no general tolerance. Earlier specialized background and composition lanes retain their existing narrowly named per-channel one-byte quantization cases and independent layer-omission controls. New failures require diagnosis before any tolerance is considered. World/live/input fallback pixels remain exact. No mask or image-wide percentage allowance is used.

## Execution

Build current source and reproduce the integrated six-cell theory under the shared dotnet lock:

```sh
flock --close /tmp/mgui-render-cache-dotnet.lock dotnet test MGUI.Tests/MGUI.Tests.csproj -c Release --filter FullyQualifiedName~RenderCacheMatrixNativeTests --logger 'console;verbosity=detailed'
```

`NativeGraphicsHost` calls the private-display helper. Each matrix process gets its requested Xvfb size; the helper owns the graphics lock and display cleanup. Do not add an outer graphics lock. An already built single cell may be reproduced with:

```sh
python3 tools/render-cache/test-native.py MGUI.RenderCacheTestHost/bin/Release/net8.0/MGUI.RenderCacheTestHost.dll --matrix 1280 720 2
```

The helper also accepts `--display-size=WIDTHxHEIGHT` and consumes it before launching dotnet. Existing calls retain 640×480 and the existing sample retains 1280×900. Matrix calls reject a mismatched explicit display. Pure argument checks live in `tools/render-cache/test_native_helper.py`.

## Requirement to executable evidence

Paths below are source/test anchors. Matrix report names use `s<SCALE>`; each six-cell report is printed as `ARTIFACTS:<directory>`. Every comparison entry contains its exact image and difference paths. Specialized lanes must be rerun on the final source; their existing historical reports are not substitutes for final validation.

| Requirement | Native source and durable test | Named report/comparison evidence |
| --- | --- | --- |
| Both physical resolutions; scales 1/1.5/2; readable full surface | `LiveSiblingTests.Draw/EndDraw`, `LiveSiblingFixture.Paint`, `RenderCacheMatrixNativeTests` | `width`, `height`, `layoutScale`, `genuinePresentedWarmups`, `drawableProof`; every matrix direct/cached/difference image |
| Normal/hover/pressed/selected/disabled; live click/toggle | `LiveSiblingTests.TestInput`, six-cell theory | `input-s*-hover-press-selection-disabled-toggle`; saved per-step images |
| Mouse focus, typed input, select/replace, drag, wheel and clipped-row coordinate correctness | `TestInput`, `LiveSiblingFixture.Center/SliderPosition`, six-cell theory | `mouse-focus-keyboard-selection`, `scaled-slider-drag`, `wheel-scroll-and-visible-row-input`; identical state/event snapshots |
| Warm cached interactive content still updates and receives input | `TestInput/TestButtonDescendantContext`, six-cell theory | `warm-boundary-control-input-update-and-invalidation`, `pending-offset-context-and-primary-foreground` |
| Live 400ms color/scale pulse, three painter orders, unchanged static captures/builds/updates | `TestPulse`, six-cell theory | `pulse-s*-order*-400ms-no-static-rebuild-exact-pixels-live-updates`; midpoint/complete PNGs; pulse traces and distinct order hashes |
| Text/font/foreground/image changes, visible omission controls | `MatrixCases.TestMatrixMutations`, six-cell theory; `TextImageTests`, `TextImageNativeTests` | `mutation-s*-text-font-foreground-image-mutations`; `text-direct`, `font-direct`, `foreground-direct`, `image-direct`, `image-omission-control` exact pairs |
| Style XAML parsing/defaults/binding and source changes | `RenderCacheXamlTests`, `RenderCacheXamlNativeTests`; `CachedBrushXamlTests`, `CachedBrushXamlNativeTests` | Genuine `XAMLParser.LoadRootWindow` application of Base/Derived styles and explicit overrides in specialized `RenderCacheXamlTests.LoadContent`, asserted by `defaultsAndStyles`; schema style-setter examples in `tools/MGUI.XamlSchemaGenerator/Tests/SchemaValidationTests.cs`. Matrix `foreground-direct` is only a runtime property mutation and does not claim XAML style application. |
| Translation/resize and size-driven sibling geometry | `TestLayout`, `MatrixCases.TestMatrixMutations`, six-cell theory | `real-sibling-content-size-moves-and-rebuilds-static`, `translated-resized-window`; direct/newly rebuilt pairs |
| Hidden/collapsed/empty and reinsertion | `MatrixCases.TestMatrixMutations`, six-cell theory; `ContentBoundaryTests`, `ContentBoundaryNativeTests` | `hidden-collapsed-empty-and-recovery`; specialized `initial-and-transition-empty-content-never-allocate`, `disable-empty-hidden-retire-and-recover` |
| First miss, late invalidation, omitted Prepare | `MatrixCases.TestMatrixMutations`, six-cell theory; `PreparationTests`, `RenderCachePreparationNativeTests` | `first-miss-content-and-layered-alpha-exact`, `omitted-prepare-and-late-invalidation`; `content-first-miss`, `late-after-prepare`, `omitted-prepare` exact pairs |
| Scroll clip/scissor and overflow fallback | `TestInput`, `MatrixCases`; `CompositionTests`, `CompositionNativeTests` | `wheel-scroll-and-visible-row-input`, `alpha-transform-overflow-direct-fallback`; specialized screen-scissor/bookkeeping/partial-clip pairs |
| Layered descendant alpha and incoming opacity fallback | `MatrixCases`; `CompositionTests`, `CompositionNativeTests` | Opaque-backed `content-rebuilt` exact matrix pair; `incoming-alpha` exact fallback; specialized transparent/opaque scenes, named `descendant-alpha-opaque-scene` RGB≤1/A=0 and omission controls |
| Unsupported transforms/caller settings/effects | `MatrixCases`; `CompositionTests`, `CompositionNativeTests`; `BackgroundParityTests`, `BackgroundParityNativeTests` | `window-scale` exact matrix fallback; specialized transform/blend/sampler/depth/effect/clip pairs and zero-hit/pending assertions before reference toggles |
| ExplicitlyStatic local/UV lighting and texture/nine-slice effect mutation | `BackgroundParityTests`, `BackgroundParityNativeTests` | Specialized 86-pair background report, per-case RGBA limits, actual effects/parameter mutation, three documented one-byte cases; see `render-caching-background-parity.md` |
| Participating/nonparticipating custom sources/renderers and public callback frequency | `EligibilityTests`, `RenderEligibilityNativeTests`; `CompositionTests`; `MatrixCases` | `exact-type-and-custom-manual-capture`, `unknown-custom-renderer`; six-cell `public-callback-direct-and-recovery` counts once per final paint and none during Prepare |
| Tooltip/context/menu/modal ordering | `TestInput`, six-cell theory; `DecorationTests`, `DecorationNativeTests` | `tooltip-and-context-menu`; specialized `tooltip-popup-nested-modal-live-ownership`, overlay painter order |
| Nested boundaries, inactive branches, raw root removal/reinsert, safe GPU retirement | `LifetimeTests`, `LifetimeNativeTests`; `ContentBoundaryTests` | Specialized lifetime eight exact image pairs/15 groups; `raw-remove-clear-reinsert-pending-warm-all-lifecycles`, `nested-capture-invalidation-detach-clear-and-exception`, target accounting |
| Alternate drag-preview/main observation and inline text action/tooltip repeated updates | `CompositionTests`, `CompositionNativeTests`; `TextImageTests`, `TextImageNativeTests` | `main-preview-main-and-alternate-descendant-observations`; `inline-action-tooltip-live-cycles`; no capture replay of public hooks/bookkeeping |
| Actual XAML sample modes/toggles/lifecycle | `SampleTests`, `SampleNativeTests`; `RenderCachingSampleTests` | Actual sample 16 groups and normal/background/panel/pulse/mutation/resize/scale captures; see `render-cache.md` |

## Preliminary isolated execution record

All six standalone matrix cells passed: 237 exact full-frame comparisons and 20 assertion groups each, totaling 1,422 exact comparisons. Three genuine presentations preceded every cell. Each cell retained 474 direct/cached PNG files plus one shared black difference PNG; all paths and dimensions were independently checked. Sample PNGs were decoded independently with Pillow, compared, and all 121 pixels in each corner checked against the known clock-derived colors. Full 1280×720 scale-2 scrolled input and 1920×1080 scale-1.5 pulse captures were visually inspected; all physical corners and the full readable surface are present.

The executed backend was MonoGame DesktopGL, Release, private Xvfb, Mesa 26.2.3 llvmpipe (LLVM 23.1.1, software). Other graphics backends remain unverified. These are deterministic correctness checks and establish no hardware FPS or performance conclusion. Exact source/binary hashes are in `/tmp/mgui-t26-native-source.json`; the six report/log/image indexes are in `/tmp/mgui-t26-preliminary-native-evidence.json`.

| Physical surface | Layout scale | Native report | Representative full frame | Exact difference |
| --- | --- | --- | --- | --- |
| 1280×720 | 1 | `/tmp/mgui-matrix-379308/report.json` | `pulse-s1-order1-f10-cached.png` in that directory | `exact-difference-1280x720.png` |
| 1280×720 | 1.5 | `/tmp/mgui-matrix-379884/report.json` | `pulse-s1.5-order1-f10-cached.png` | `exact-difference-1280x720.png` |
| 1280×720 | 2 | `/tmp/mgui-matrix-380279/report.json` | `input-s2-row-release-cached.png` | `exact-difference-1280x720.png` |
| 1920×1080 | 1 | `/tmp/mgui-matrix-380881/report.json` | `pulse-s1-order1-f10-cached.png` | `exact-difference-1920x1080.png` |
| 1920×1080 | 1.5 | `/tmp/mgui-matrix-382160/report.json` | `pulse-s1.5-order1-f10-cached.png` | `exact-difference-1920x1080.png` |
| 1920×1080 | 2 | `/tmp/mgui-matrix-383014/report.json` | `input-s2-row-release-cached.png` | `exact-difference-1920x1080.png` |

Commands used the shared dotnet lock for each already-built helper invocation, serially, with no outer graphics lock. The initial trials exposed two fixture mistakes: a layout-changing late Text setter needs layout settlement and a new direct observation before capture; and a deliberately scaled root can legitimately overlap a sentinel without a reserved guard band. The final fixture independently tests pure-visual and layout-changing late mutations and reserves that guard band. No production correction or new tolerance was required. Artifact encoding initially performed three redundant PNG/GPU roundtrips per pair; exact images now encode once and copy to both paths, and the exact black difference is shared. Every frame still has full pixel/state/maxima/hash assertions and valid artifact paths. The 90-second child deadline was unchanged.

The old `LiveSiblingNativeTests` regression also passed (372 exact pairs, 26 groups; `/tmp/mgui-t26-old-live-regression.log` and the evidence index), as did the three pure display-helper contracts. Release compilation passed with zero errors; focused XAML/trace contracts passed 20/20. This preliminary run deliberately omitted a broad suite, performance measurements and main integration. The final integrated validation below supersedes these preliminary results for acceptance; the older artifacts remain available without being counted as additional distinct cases.


## Final integrated evidence

The final durable `RenderCacheMatrixNativeTests` theory passes 6/6: each cell has 237 exact RGBA comparisons and 20 required assertion groups, totaling 1,422 comparisons of the same defined matrix cases. The final reports independently record equal direct/cached pixel hashes, identical matched input/state and simulation clock, and the asserted one-day caret interval. All 475 PNG paths per cell and their physical dimensions are checked by xUnit; sampled decoded images, their pixel hashes and all 484 known corner pixels were independently verified again. No tolerance or mask was introduced.

`MGTextCaret.Draw` uses `DateTime.Now - Position.InitialShowTime`, rather than the fixture clock, and shows the caret while `int(elapsedSeconds / BlinkRate.TotalSeconds) % 2 == 0`. An additional comparison of the preliminary and first integrated runs found seven cross-run differences, each confined to a one-pixel caret column 35 or 44 pixels high. Those A/B pairs had still matched exactly within each run. The matrix-only one-day public interval now keeps that quotient zero throughout its bounded child process. Focus, position, selection, actual keyboard/mouse input, normal updates and intrinsic caret volatility retain their existing behavior. The old fixture defaults are unchanged. The original evidence and `/tmp/mgui-t26-caret-diagnosis.json` are retained; repeat runs are not counted as new cases.

| Physical surface | Layout scale | Final report | Representative actual input | Exact difference |
| --- | --- | --- | --- | --- |
| 1280×720 | 1 | `/tmp/mgui-matrix-440769/report.json` | `input-s1-row-release-cached.png` in that directory | `exact-difference-1280x720.png` |
| 1280×720 | 1.5 | `/tmp/mgui-matrix-442565/report.json` | `input-s1.5-row-release-cached.png` in that directory | `exact-difference-1280x720.png` |
| 1280×720 | 2 | `/tmp/mgui-matrix-443901/report.json` | `input-s2-row-release-cached.png` in that directory | `exact-difference-1280x720.png` |
| 1920×1080 | 1 | `/tmp/mgui-matrix-444464/report.json` | `input-s1-row-release-cached.png` in that directory | `exact-difference-1920x1080.png` |
| 1920×1080 | 1.5 | `/tmp/mgui-matrix-443099/report.json` | `input-s1.5-row-release-cached.png` in that directory | `exact-difference-1920x1080.png` |
| 1920×1080 | 2 | `/tmp/mgui-matrix-441366/report.json` | `input-s2-row-release-cached.png` in that directory | `exact-difference-1920x1080.png` |

Final matrix results: `/tmp/mgui-t26-matrix-final.trx` and `/tmp/mgui-t26-matrix-final.log`. The relevant specialized lanes passed in the integrated 17/17 run: background parity, cached-brush XAML, composition, content boundary, decoration, lifetime, preparation, render-cache XAML, eligibility, actual sample, and text/image/font/inline-action input. The matrix-only caret/reporting correction changes neither those fixtures nor production Core/Shared source, so their eleven passes are reused without repeating them. The exact before/after source hashes document that distinction.

| Specialized evidence | Current report or log |
| --- | --- |
| Background effects, alpha, fallbacks, invalidation, budgets and world | `/tmp/mgui-background-parity-425097/report.json`: 86 pairs, 83 exact and the three previously documented per-channel one-byte cases |
| Composition, clip/scissor, alternate preview, callbacks, caller settings and opacity | `/tmp/mgui-composition-425379/report.json`: 48 pairs, 47 exact and the existing narrowly named opaque-scene rounding case |
| Nested boundaries, raw root changes, safe GPU retirement and teardown | `/tmp/mgui-lifetime-425793/report.json`: eight exact pairs, 15 groups |
| Actual XAML sample commands, modes, pulse and lifecycle | `/tmp/mgui-sample-433667/report.json`: 16 groups and captures |
| Style application, inline text actions/tooltips, custom support and first-miss/preparation protocols | Named tests in `/tmp/mgui-t26-integrated-native.trx` and `/tmp/mgui-t26-integrated-native.log`, linked to the source anchors in the requirement table |

`/tmp/mgui-t26-final-evidence.json` indexes every final cell, representative PNG, shared exact difference, specialized report and validation log. `/tmp/mgui-t26-integrated-source.json` records the final runtime source and binary hashes; `/tmp/mgui-t26-before-caret-source.json` records the specialized run's exact source/binaries. The final Release build passes with zero errors and three existing warnings. The pure helper contracts pass 3/3, including empty/malformed explicit-size rejection and all historical invocation defaults. The previous 372-pair old live-sibling regression and 20 pure XAML/trace contracts remain valid for their unchanged behavior.

Executed backend: MonoGame DesktopGL, Release, owned private Xvfb, Mesa 26.2.3 llvmpipe (LLVM 23.1.1, software), with current raw backend provenance in `/tmp/mgui-t26-integrated-backend.log`. This work makes no hardware FPS or timing claim. Other backends remain unverified. The final broad integration suite is owned by the separate integration audit; it was not repeated here.
