# Cached artwork and live siblings: native regression lane

`LiveSiblingFixture` in `MGUI.RenderCacheTestHost` is a reusable native consumer fixture. It uses ordinary `MGOverlayPanel` children, explicit `MGRenderCache` boundaries for static frame/labels, and ordinary live score, button, toggle, slider, text box and scroll-viewer siblings. An additional button is inside a cache boundary to verify input and updates during warm hits. There are no game rules or external game assets.

The fixture implements `IRenderHost`: every update advances exactly 20 ms and supplies synthetic `MouseState`/`KeyboardState` through the real renderer `InputTracker`. Independent enabled-cache and disabled-cache fixtures receive identical input and elapsed time. Their observable states and event sequences are compared before the uncached reference is painted; their native backbuffer pixels must agree exactly. No reference toggle can clear the tested fixture's cache state. Final UI drawing must allocate no cache targets and switch no targets; disposal must balance native cache allocation accounting.

The lane verifies:

- A synthetic score change starts a color and `RenderScale` pulse lasting exactly 400 ms. The real pointer hovers the score, activating the toolkit's conditional render scale. Every measured frame updates and paints the score while static capture/paint/build counts stay constant. Static elements continue receiving normal updates.
- Three visible painter orders put the score behind both static layers, between them, or after both. All three midpoint images have distinct hashes. Every animation frame matches its corresponding uncached layout.
- Real hover, press, release, selection, disabled clicks, toggle, incremental slider drag, mouse focus, typed keyboard input and select/replace behavior; tooltip delay and context-menu item activation; wheel scrolling and an exact fully visible row click selected in screen space after the normal offset update.
- A warmed interactive boundary directly repaints changed hover/press generations, rebuilds only after direct observation, continues normal updates on unchanged hits, and fires its button action normally.
- A live sibling's content-size change in `MGStackPanel` moves static content, rejects the old placement and rebuilds its newly observed geometry.
- An ancestor's conditional nonunit scale still invalidates descendants and bypasses, then recovers when its actual scale returns to one. A nested boundary under a button rejects a pending old-offset generation before preparation on a real press; changing `PressedContentOffset` while pressed invalidates the descendant before preparation. Selected primary state still updates inherited foreground.

Every case runs at layout UI scales 1 and 1.5 with a nonzero window origin. The test host remains 640 by 480 to fit the harness's owned Xvfb display. Warmup settles ancestor hover transitions before sibling-only measurements.

The real-input regression required a narrow runtime correction: secondary visual-state changes invalidate the element and its painted ancestors, rather than every descendant by default. Primary state still changes inherited foreground, and a changed effective `RenderScale` still changes descendant painting. `DoesVisualStateChangeDescendantRenderContext` allows a control to declare additional inherited context; overrides must preserve a true base result. `MGButton` uses its exact scaled, enabled-state-aware content offset for this decision and for active offset setter invalidation. This avoids rebuilding unrelated static siblings while retaining pre-preparation invalidation for real descendant changes.

Run current source under the shared build lock, then let the native harness own its display and graphics lock:

```sh
flock --close /tmp/mgui-render-cache-dotnet.lock dotnet build MGUI.Tests/MGUI.Tests.csproj -c Release --no-restore
python3 tools/render-cache/test-native.py MGUI.RenderCacheTestHost/bin/Release/net8.0/MGUI.RenderCacheTestHost.dll --live-siblings
flock --close /tmp/mgui-render-cache-dotnet.lock dotnet test MGUI.Tests/MGUI.Tests.csproj -c Release --no-build --no-restore
```

The xUnit entry point is `LiveSiblingNativeTests.StaticCachedArtAndLiveSiblingsPreserveAnimationPainterOrderAndRealInput`. The native lane reports 26 assertion groups and writes `report.json` and paired PNGs to the printed `ARTIFACTS` directory. The report records all comparison maxima, pixel hashes, pulse/counter traces and host/core DLL SHA-256 hashes. Initial validation produced 372 exact RGBA comparisons with zero differing pixels and no tolerance. Backend: MonoGame DesktopGL, Release, owned Xvfb, Mesa 26.2.3 llvmpipe (LLVM 23.1.1), software rendering; these are correctness checks and make no hardware performance claim.
