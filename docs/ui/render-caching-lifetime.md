# Nested cache lifetime validation

Nested content boundaries and cached brushes use the existing direct-capture policy: an outer capture paints its descendants normally with every nested lookup/request suppressed. Each boundary still observes direct final painting and receives descendant revisions. Two/three nested boundaries therefore prepare independently without sampling their own or another cache target; an outer hit can skip the whole flattened content subtree.

`LifetimeNativeTests` runs the standalone `--lifetime` native host lane. No production changes were needed for this matrix. Managed removals reuse `InvalidateRemovedRenderSubtree` and its content-observation retirement. A removed contributing subtree loses all nested observer registrations and pending generations immediately. Native targets remain accounted until the next safe preparation or explicit manager cleanup. A contextual inactive branch is covered even though its logical Parent remains unchanged.

An explicit public draw of a detached boundary is a separate observation, provided its fixed ParentWindow remains attached and normal layout is valid. Removal first invalidates layout, which bypasses admission. The fixture then arranges the detached boundary through the ordinary layout path and verifies that a new explicit direct observation, followed by preparation, can be reused. This does not revive the removed tree's former observation.

## Running the matrix

Build current Linux dependencies and run the focused test under the shared dotnet lock:

```sh
flock --close /tmp/mgui-render-cache-dotnet.lock dotnet test MGUI.Tests/MGUI.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~LifetimeNativeTests -v minimal
```

For standalone evidence from that build:

```sh
flock --close /tmp/mgui-render-cache-dotnet.lock python3 tools/render-cache/test-native.py MGUI.RenderCacheTestHost/bin/Release/net8.0/MGUI.RenderCacheTestHost.dll --lifetime
```

The native helper owns the graphics lock and a private Xvfb display and tears them down. Never wrap the test suite or this helper in an outer graphics lock. Its renderer metadata accompanies the result. `ARTIFACTS:` names the directory containing full-frame direct/cached PNG pairs, exact RGBA comparison counts, scene resource statistics, scope limitations, and host/core assembly SHA256 provenance.

## Coverage and evidence

The 15 required result groups cover two/three-level nesting; child mutation and idle painting without subtree discovery; a visibly contributing nested cached brush; inserted/removed volatile and unsupported descendants; pending/warm managed detach, inactive branch and content replacement; explicit detached direct painting; same-window reparenting with both owners invalidated; raw Windows Remove/Clear/reinsert observed at each Update/Prepare/Draw entry; nested/modal window attachment removal; bounded pre-preparation eviction and post-preparation competing-request LRU eviction; zero budget; nested capture invalidation/detach/Clear/exception; two desktops sharing one device; actual device Reset; independent desktop budget/disposal; and weak cache manager/registration ownership.

The post-preparation eviction case retains the recently composited outer target under a one-entry budget, then publishes a genuine competing native target. It requires that specific outer target to be disposed while the boundary revision is unchanged. Before disabling any cache for the reference, its next draw must paint the leaf directly, record no hit, and queue only the admitted fresh observation. A capture exception must release its candidate and leave the device unbound without publishing. Other valid queued observations may be retained or conservatively dropped; the test does not prescribe that internal queue policy.

The recorded Release run uses MonoGame DesktopGL, Mesa 26.2.3 llvmpipe (LLVM 23.1.1, software renderer). All eight full-frame comparisons are exact RGBA with zero allowed channel error. The 29 scene lifecycles allocate and dispose 163 manager-owned targets, with equality required at each teardown. The 154 targets observed by the probe/native allocation helper each raise exactly one disposal event. Scene accounting also covers targets that never paint the probe, such as the nested brush. The separate weak-owner native target is checked through its own manager allocation/disposal equality.

The full current-source `MGUI.Tests` Release suite passes 553/553, including resource, preparation, content-boundary, composition, and lifetime lanes. Software rendering establishes pixel/resource correctness and makes no hardware FPS claim.

Weak-reference checks deliberately use CPU owner/operation identities with a live native device to isolate cache-owned roots. They verify that device events do not retain an abandoned manager, ready targets do not retain their owner, dead operations do not survive pending work, and preparation sweeps the corresponding native resources. They do not claim whole-UI collection: known global element/DataContext observer retention (`mgui-9b9`) and disposed binding registry retention (`mgui-pzh`) are separate work. Actual managed UI detach/reparent/root behavior is verified natively regardless. Native device Reset is exercised; raw context/driver removal is not simulated or claimed.
