# Downstream render-cache migration handoff

**Status:** MGUI-side fixture validation is complete against the T27 final-source report. This document prepares a separate consumer adoption; game-consumer parity and performance remain unproven. It maps the current MGUI API to the read-only audit of LoadedAndLocked.

## Scope

This document prepares a separate consumer task for `/home/acarteas/code/hex-game-engine`. It does not authorize or include edits in that repository. Cache and background implementation remains in MGUI; the consumer migration removes its duplicate adapter and connects the existing desktop lifecycle to MGUI.

Preserve game rules, panel layout, scale behavior, colors, effect parameters, texture margins, padding, and menu border values. The migration changes cache ownership and lifecycle only.

## Consumer touchpoints

| Current consumer location | Current behavior | Downstream migration |
| --- | --- | --- |
| `HexGameEngine.Core/Ui/Gameplay/XAML/CachedFillBrush.cs` | Defines the game-local XAML `CachedFillBrush`, a local runtime wrapper, per-GraphicsDevice cache bookkeeping, and static `PreparePending(MGDesktop)`. | Remove the duplicate XAML/runtime implementation after all use sites target MGUI's types. |
| `LoadedAndLocked/Ui/Gameplay/GameplayUiHostService.cs::PrepareDraw` | Calls `HexGameEngine.Ui.Gameplay.XAML.CachedFillBrush.PreparePending(_desktop)`. | Keep the host hook if useful to its caller, but delegate to `_desktop.PrepareRenderCaches()`. |
| `LoadedAndLocked/LoadedAndLockedGame.cs::DrawCore` | Calls gameplay `PrepareDraw` after material refresh and before choosing direct or composite presentation. Each path then selects/clears its scene target, draws the world, and reaches `DrawOverlayUi`, which calls gameplay `Draw`. | Preserve this ordering: preparation must finish after desktop Update and before either frame path clears or renders the scene. |
| `LoadedAndLocked/Ui/Gameplay/Theme/SharedStyles.xaml::HudPanelBorder` | Wraps the two-layer `CompositedFillBrush` in `hex:CachedFillBrush`; the second nine-slice uses `GamePanelLightingEffect`. | Use MGUI's XAML `CachedFillBrush` wrapper while keeping the existing brush tree and all values intact. Audit the effect source before selecting `ExplicitlyStatic`. |
| `HexGameEngine.Core.Tests/Rendering/CachedFillBrushTests.cs` | Tests the game-local runtime wrapper's direct draw/copy behavior. | Remove or replace local-implementation tests when the duplicate is deleted; MGUI owns cache API/resource behavior tests. |
| `LoadedAndLocked.Tests/Ui/Gameplay/PlanningGuidanceStripTests.cs` | Contains a local `CachedFillBrush` markup sample and asserts the materialized local runtime type, alongside panel/layout assertions. | Update markup/type references; retain the panel border, geometry, text, action and replay assertions. |
| `HexGameEngine.Architecture.Tests/Architecture/PresentationAudioConfigurationContentOwnershipManifestTests.cs` and `Docs/Design/PresentationAudioConfigurationContentOwnershipManifest.md` | Inventory the local brush file as Core/shared game infrastructure. | Remove that implementation from the game-owned inventory and test expectations. Continue to list product styles/screens under LoadedAndLocked. |

The current `SharedStyles.xaml` root already maps its default element namespace to `MGUI.Core.UI.XAML;assembly=MGUI.Core`, while `hex` maps game components. The markup can therefore use MGUI's `<CachedFillBrush>` directly; the `hex` namespace remains for other game-owned XAML types. No new runtime type registration is needed in the game. MGUI exposes the XAML wrapper as `MGUI.Core.UI.XAML.CachedFillBrush`, the runtime brush as `MGUI.Core.UI.Brushes.Fill_Brushes.MGCachedFillBrush`, and `MGRenderCacheSourceMode` beside the runtime brush.

Example for the reviewed effect-backed HUD background, conditional on the source audit below:

```xml
<CachedFillBrush SourceMode="ExplicitlyStatic">
    <CompositedFillBrush>
        <!-- Keep the current two NineSliceFillBrush children and values unchanged. -->
    </CompositedFillBrush>
</CachedFillBrush>
```

## Source assertion and invalidation

`Tracked` is the default. It uses known resource tracking and deliberately bypasses an effect source that has no tracking contract. `ExplicitlyStatic` is a caller assertion over the entire wrapped source graph. Use it for this HUD only after verifying that the lighting shader's pixels depend on bounded local geometry/UVs and declared parameters, with no time, screen-position, interaction, or other external dependency. Its output must remain deterministic, premultiplied, bounded to the supplied brush bounds, and free of draw side effects or target switches. Known source volatility, unsupported paint sites, dead resource lifetimes, and geometry/call-context constraints still bypass caching; the assertion cannot override them.

MGUI observes supported resource changes. The consumer must call the materialized `MGCachedFillBrush.InvalidateVisual()` before Draw after opaque changes such as writes to an `Effect` parameter, texture uploads, or raw collection/list edits that the source graph cannot observe. Locate the runtime wrapper through the owning border's background value or retain a typed reference where the effect is mutated. Do not add per-frame tree walks or hashes. If the lighting shader or its mutation points cannot satisfy and maintain this contract, leave that background on the ordinary direct-paint path until they can.

The read-only downstream audit touchpoint is `HexGameEngine.Content/Assets/Shaders/Ui/GamePanelLightingEffect.fx`. At consumer revision `b51ddb3a8323d33f2c61c1bb608543da9be491d4`, its inspected SHA-256 was `dca7e89d24276347e30fedd59c1d0d6c87cd3982e1ca9fcab24da66e715e2fe2`. In that source, `PSMain` derives its lighting from sampled `SourceCoordinate`, `ElementTextureCoordinateScale`/`Offset`, `ElementSize`, fixed lighting parameters and vertex alpha; it does not read `SV_POSITION` or a time input. Treat this as a source-level observation only, not proof of runtime effect immutability, consumer backend parity, or cache safety; do not select `ExplicitlyStatic` from this inspection alone. Other `LightDirection` uses serve world/debris effects, so searches by parameter name do not establish the HUD effect's complete mutation history. The consumer adoption task must trace the exact `GamePanelLightingEffect` instance assigned to the HUD brush, audit all parameter and referenced-resource mutation paths, invalidate opaque changes before Draw, and run native consumer pixel checks before enabling `ExplicitlyStatic`.

## Desktop lifecycle and draw order

The current consumer project references sibling `MGUI.Core`, `MGUI.Shared`, and `MGUI.FontStashSharp` projects directly. The UI host's `Update()` finishes with `_desktop.Update()`; the game calls `GameplayUiHostService.PrepareDraw()` before either scene render path. The minimal lifecycle mapping is:

```csharp
// Existing update path:
_desktop.Update();

// In GameplayUiHostService.PrepareDraw, before the game selects/clears its scene target:
_desktop.PrepareRenderCaches();

// Existing overlay position remains after world presentation:
_desktop.Draw();

// During owner/service teardown, before renderer/device teardown:
_desktop.DisposeRenderCaches();
```

Call preparation once after a completed desktop Update, before clearing or drawing the world, and outside every SpriteBatch, PrimitiveBatch, DrawTransaction, and host render-target session. In the current game, `DrawCore` invokes `PrepareDraw` before direct/composite paths bind targets and clear, which is the intended safe point. Do not move preparation into `Draw()` or after the world render. If preparation is omitted for an Update, or its call context is unsupported, draws use the direct path. A second preparation call for the same Update is a no-op; it preserves any ready caches from the first successful call. A source change invalidated after preparation but before Draw cannot use its stale entry and paints directly for that draw. Keep preparation before scene rendering: late preparation after the world has been drawn is unsupported host ordering and can discard scene pixels while switching render targets. Do not use it as a fallback path.

`MGDesktop.DisposeRenderCaches()` permanently disposes that desktop's cache service. Call it before abandoning the desktop or disposing its graphics device/renderer. The current `GameplayUiHostService.Dispose()` disposes its render host; the game disposes registered services before base game/device disposal. Place cache cleanup in the owner teardown path while the desktop and device are still valid. Do not rely on disposing the renderer to dispose desktop-owned caches.

## Project and package prerequisites

- With the current sibling-project setup, keep the MGUI Core, Shared, and FontStashSharp project references on one reviewed checkout that contains `MGCachedFillBrush`, the XAML `CachedFillBrush`, `MGDesktop.PrepareRenderCaches()`, and `MGDesktop.DisposeRenderCaches()`.
- If switching to packages, select a published, mutually compatible MGUI release that contains all of those APIs; coordinate Core, Shared, and FontStashSharp package versions. Do not guess a version before the release is published.
- The consumer targets .NET 10; MGUI.Core currently targets .NET 8, so confirm the chosen package/project output remains consumable by the target framework.
- Keep one compatible MonoGame API/backend assembly across the consumer and MGUI dependencies. The consumer currently references its sibling MonoGame framework project while MGUI.Core declares DesktopGL package dependencies as private assets; review the resolved graph for duplicate or mismatched MonoGame assemblies when moving from project references to packages.
- Rebuild the consumer projects after dependency changes using the repository's configured dependency-build path. This draft records prerequisites, not a completed compatibility build.

## Ordered consumer smoke checks

1. **World scene:** launch a real gameplay scene. Verify preparation completes before the scene clear/world draw in both direct and composite presentation modes; compare a frame with caching enabled and disabled to confirm the world is intact.
2. **HUD background:** inspect the layered nine-slice through first-use direct painting, a later cache hit, resize, and UI-scale change. Compare pixels and verify the same shape, lighting, and margins.
3. **Modal menus:** open the pause menu and at least one dialog. Verify modal ordering, button/hover interaction, and the panels' original borders and colors.
4. **Scaled HUD:** exercise supported UI-scale settings and window resize. Confirm HUD placement, clipping, text, and background match the uncached presentation.
5. **Live pulse:** trigger a scenario score update and observe the animated score pulse while the static panel background remains visually correct. Confirm the pulse still advances and that cache eligibility does not suppress normal updates.
6. **Invalidation and teardown:** change any audited opaque effect/texture/list input, invalidate before Draw, and verify the next frame shows the new pixels. Close the game normally and confirm cache disposal runs before renderer/device disposal.

Retain existing product assertions for HUD/menu style choice, border thickness, panel geometry and text layout, control input/observation, modal state, and score pulse. Only assertions tied to the deleted local implementation or its runtime type become redundant.

## Performance evidence and remaining acceptance

The API names and lifecycle above are reconciled with the finalized [render caching guide](render-caching.md) and [cached fill brush reference](cached-fill-brush.md) from T25 (commit `7892013`). T23's [equivalent rendering comparison](render-caching-comparison.md) (commit `0b9518d`) contains 216 accepted timing captures on its earlier frozen source (`dca1319ff5571e9228672883dd970e35eccfa1a2`): 11 of 12 stable or single-change panel/configuration pairs passed that report's benefit gate, while one layered 1080p/scale 1.5 case was inconclusive. The 36-background-cache mode was slower in those fixtures; continuously dirty and scrolling content showed no useful cache benefit. Of those captures, 120 resumed captures were journaled while the separate UI-capture workflow was active; the 96 original accepted captures have no retrospective load journal. The full set must not be described as measured under known shared load. Results used MonoGame DesktopGL with llvmpipe software rendering and do not predict consumer hardware, backend performance, or this game's HUD workload.

The [T27 final mixed workload and resource stability report](render-cache-stability.md) uses the post-T32 source at `64cbd488562e493eb0070156bf432d0428f2fdd7` (source SHA-256 `2a9492089dac6af87e457a77bc14bb6d42653f5140d7b1ad3389809b58c5c607`). Its fresh accepted run includes 54 ordinary runs, 63 mixed pixel preflights, 189 mixed timed runs, three 400-frame resource preflights, and three actual 10,000-presented-frame soaks. It records 14,200 exact cached/normal frame-hash comparisons and checks limits/accounting throughout all 30,000 soak frames. Cache allocations equal disposals and final retained bytes/entries/pending are zero: background 7,037/7,037, panel 2,228/2,228. The 34 input assets remained invariant. These checks support bounded resource behavior and correctness for these MGUI fixtures; they do not demonstrate game-consumer parity or hardware performance.

T27 also reports substantial submission losses for these small-row fixtures on the exercised software renderer, despite fewer panel UI draws and lower UI allocations. At 1920×1080/scale 1 idle HUD, UI+Prepare median/p95 was 0.3080/0.4924 ms normal, 2.9921/3.9463 ms background, and 2.5023/2.9490 ms panel; panel UI draws/textures fell from 34 to 13. The UI+Prepare loss gate failed in 18 of 21 cells for each cache mode. Continuous animation fell back to direct painting but showed additional Update allocation cost. These results argue against caching many small rows and show that fewer native draws do not establish lower CPU submission cost.

The T27 report retains the historical cross-date animated-720p Update flag and a small UI-timing uncertainty. T32 resolved both earlier nested-1080p Update flags. Its same-host old-to-fixed animated Update delta was 0.00505 ms, within a broad 0.10150 ms gate; this does not establish zero overhead. See the [Update regression report](render-cache-update-regression.md) for the evidence and limits.

T23's stable layered/static-panel benefits remain scoped to its prior frozen source and distinct fixtures; they are not current-source T27 results. The post-T32 T27 report supports MGUI fixture correctness and bounded resource behavior under its recorded software-renderer/shared-load conditions, with the small-row submission losses described above. Together these reports support testing a suitably expensive stable panel while benchmarking the actual consumer workload. The game consumer still has no parity or performance evidence here. A separate consumer adoption must verify its own build, native pixels, and workload; completion of MGUI fixture validation does not establish those results.

The shader inspection above remains only an adoption audit starting point. It does not establish that the game's effect instance is safe to assert static. The consumer task must finish tracing effect and resource mutation, issue explicit invalidation where needed, and run native pixel checks before opting in.
