# Maintained render-cache eligibility

T18 (`mgui-ch5.2.1`) implements the participation and volatility portion of the [approved contract](render-caching-design.md). It adds no panel boundary, automatic pixel cache, update shortcut or new layout pass. Ordinary rendering remains unchanged. The [complete runtime inventory](render-caching-invalidation-audit.md) and the text/image, interactive and decoration coverage documents define the audited built-in painters.

## Participation

`MGElement.SupportsRenderCaching` is a protected virtual getter. Its default uses an exact runtime-type allowlist of 50 audited concrete built-ins (including the T15 MGRenderCache addition), including only the three known generic definitions (`MGListBox<>`, `MGListView<>`, `MGComboBox<>`). Deriving from an admitted built-in does not inherit admission. Unknown custom subclasses default unsupported.

A custom renderer may override the getter to return true. This asserts deterministic, bounded, translation-safe painting without draw side effects, framebuffer dependencies, target switching or arbitrary untracked external state. Call `InvalidateVisual()` before drawing after an opaque external-state change, or after changing the result of the support getter. Continuously changing paint should remain ordinary rendering or declare `IsRenderVolatile=true`. Support never overrides user, intrinsic or source volatility, public draw hooks, resource lifetime, clipping, transform or unresolved-layout restrictions.

The exact `MGWindow`, `MGToolTip`, `MGContextMenu`, `MGOverlayHost`, `MGTreeViewItem` and `MGXAMLDesigner` types are excluded. Window/popup/modal layers are separate ordinary draw roots. OverlayHost retains its cross-owner tint/public callback ordering. TreeViewItem paints its expander after the base clip/visibility stage, so revision tracking alone cannot make it bounded. Eligible descendants can register independent boundaries without inheriting those ancestors' unsupported classification.

Additional local facts remain effective even on admitted types: parsed action/tooltip text is unsupported; nonnull context-menu visibility delegates and external-host resize grips are unsupported; UniformGrid public cell painters and wrapped menu arrows contribute callback counts. Clearing the last relevant fact or removing its painted child restores qualification. There are no callback method-name exemptions.

## Observation and propagation

`GetRenderEligibilitySnapshot()` is an internal, additive seam for the later panel implementation. Its first call registers the existing source observation with `EnableRenderSourceTracking()` and initializes eligibility over retained contributing paint edges. Subsequent calls read a struct of maintained counters and the root's current `LayoutBounds`; they do not enumerate descendants, rediscover sources, read custom support getters, inspect delegates or allocate.

The snapshot reports unsupported participants, volatile participants, public draw callbacks, unsupported active transforms, unbounded paint and unresolved layout. `IsEligible` requires all six counts to be zero. Source support/lifetime, declared and intrinsic volatility, `HasInteractiveTextRuns`, maintained control facts, active nonunit/nonfinite `RenderScale` and `ClipToBounds=false` contribute locally. Unsupported source paint bounds also reject capture. Logical tooltip/menu/nested-window links and inactive content do not become contributing paint edges.

Local semantic invalidation updates only the changed element's facts and sends changed counter contributions up existing paint edges. Parent dictionaries retain each distinct child's previous contribution, so removals and changing the last unsupported/volatile source subtract the old facts. Content/component aliases already deduplicated by the paint graph remain one contribution; distinct actual paint paths retain their own contributions. Structure mutations reconcile affected membership; explicit `InvalidateVisual()` also reconciles raw/custom membership and already-observed resource graphs. No idle graph hash, traversal or callback polling is introduced.

Layout revisions and unresolved measurement are distinct. `InvalidateLayout()` and `LayoutChanged()` mark layout pending before initialization's deferral return. Changed geometry may advance layout revisions without marking measurements unresolved. Normal `UpdateLayout()` completion and actual layout-valid transitions reconcile the fact, including independently arranged children, without forcing unrelated ancestor layouts. In-progress layout also disqualifies capture. None of these facts changes the normal layout schedule.

`PaintBounds` is the subtree root's existing clip bound, not a promise that all draw contexts qualify. Consumers must still validate positive geometry, normal direct observation, inherited transform/window scale, incoming opacity, settings, full inherited-clip containment, attachment and exact revisions before capture or composition. A content-only panel checks its captured content aggregate and its relevant owner/context separately; wrapper decorations and public callbacks outside that content capture retain their ordinary painter positions.

Background-only cached brushes continue to use their own inner source graph and relevant owner user/intrinsic volatility. An unknown owner class or an unrelated unsupported/volatile child does not disqualify the owner's background-only operation, because its normal self/content rendering remains live.

## Public callbacks

`OnBeginDraw`, `OnEndingDraw` and `OnEndDraw` retain their signatures, invocation order and normal frequency. Counted add/remove accessors preserve multicast/duplicate delegate semantics and make absent removals no-ops. Invocation lists are inspected only when a subscription actually changes. Every public callback anywhere in captured content disqualifies that capture. Normal draw invokes them at the existing stages; preparation/capture invokes none. The internal trusted built-in ending stage remains unchanged.

## Validation

`MGElementRenderEligibilityTests` covers all 56 current concrete built-ins, custom subclasses and explicit participation, user/intrinsic volatility, content/component alias removal and reparenting, exact multicast hook counts, nested observable and raw source changes, control/transform/clip facts, deferred layout and independently arranged child completion. A 1,000-element tree verifies zero idle child/source discovery, custom support getter reads and thread allocations after registration.

`MGUI.RenderCacheTestHost --eligibility` adds seven native groups: custom static/manual-invalidation capture RGBA parity; public callback normal frequency/capture suppression and live updates; running timer updates with intrinsic/user volatility and removal recovery; inactive exceptional content and independent popup descendants; control-specific callback/opaque state; parse-time interactive text recovery; and an independently warm background-only brush despite unknown/live subtree content. These tests do not claim a panel hit protocol before the panel exists.

Run current-source Release tests through the shared build lock:

```sh
flock --close /tmp/mgui-render-cache-dotnet.lock dotnet test MGUI.Tests/MGUI.Tests.csproj -c Release --nologo --filter 'FullyQualifiedName~MGElementRenderEligibilityTests|FullyQualifiedName~RenderEligibilityNativeTests' --logger 'console;verbosity=minimal'
```

The native wrapper invokes `tools/render-cache/test-native.py`, which owns a private Xvfb display and the graphics lock. Recorded environment: MonoGame DesktopGL Release, Mesa 26.2.3 llvmpipe (LLVM 23.1.1) software rendering and native RGBA readback. This is correctness evidence, not a hardware performance claim.

T15 adds the exact runtime `MGRenderCache` type to admission for nested content boundaries. Its own operation reads only `Content.GetRenderEligibilitySnapshot()` plus owner visibility, positive owner/content geometry, clip, resolved layout and volatility. Live wrapper decorations and callbacks are outside that capture. See [content boundaries](render-cache-boundary.md).
