# Normal and disabled rendering safeguards

T22 keeps ordinary rendering and `IsCacheEnabled=false` boundaries on the existing DrawTransaction batching path. `BenchmarkFixture` retains its original normal workload and accepts an optional `disabled` mode that inserts a zero-padding `MGRenderCache` around the same root. The content, simulation clock, layout scale, geometry and scene remain identical.

## Deterministic checks

The `--normal-guards` native host runs on its own 640×480 display and asserts the actual backbuffer dimensions. It checks 144 exact RGBA and graphics-counter pairs across all six benchmark scenarios, two layout scales and twelve simulation frames. Every paint and preparation must leave cache allocations, builds, target switches, bytes, entries and pending work at zero. The comparison includes custom effects, nine-slice painting and nested clipping, so added state breaks appear in native draw-call, primitive and sprite counters.

The same lane checks 10-, 100- and 1000-node normal/disabled trees after warmup: no cache source discovery, resource reads, subscription changes or content observer slots. A separate 100-frame idle Update/Draw check permits no revision notification or generation change and measures exactly zero empty-Prepare allocation. Thirty-four real mouse/keyboard frames compare ordinary versus disabled-root wrapping at layout scales 1 and 1.5, including hover, press, release, selection, disabled clicks, keyboard focus and text entry. Existing T19 tests cover the wider slider/scroll/popup interaction matrix.

An equivalent ordinary `MGContentPresenter` provides the allocation attribution control for a disabled wrapper. The report retains all 100 Draw-allocation samples for each. Their steady allocation floors must match; native/runtime work may occasionally allocate, so total Draw-allocation sums are not a cache-only assertion. The native profile retains five 10,000-notification allocation bursts and requires a zero steady floor; a native aggregate can include unattributed runtime/process allocation in one burst, and all raw totals remain recorded. The pure ownership test independently requires exactly zero bytes for 10,000 semantic visual notifications through an unobserved tree, and a reentrant parent-removal/nested-mutation test preserves propagation correctness.

Native evidence is written to `/tmp/mgui-normal-guards-<pid>.json` and announced as `REPORT:`. The xUnit gate reads the same `RESULT:` report. Uninitialized test objects are limited to pure revision/ownership, draw-argument copying and CPU identity/counter tests; they never prove native pixels or GPU resource behavior.

## Measured fixes

The initial pure notification probe allocated 640,000 bytes for 10,000 leaf → branch → root visual mutations: two 32-byte reverse-parent arrays per mutation. Paint-parent membership changes now publish immutable arrays once; notifications iterate that stable snapshot, preserving reentrant safety without per-notification allocation.

The first paired native capture found exactly 48 bytes in each of 300 unused Prepare samples, in both modes. MonoGame 3.8.4.1 `GetRenderTargets()` allocates an empty array and the manager allocated a preparation scope. The desktop now uses the pinned public `GraphicsDevice.RenderTargetCount` property. After root reconciliation, frame-token and caller-context checks, a manager with zero pending requests and zero entries can mark the frame prepared without a scope. EntryCount includes retired and in-flight targets, so nonempty services still run the existing retirement, validation and preparation path. The full native/pure suite exercises warm hits, bound targets, reset, retirement, Clear and Dispose.

A second diagnostic capture completed 65 runs before being stopped for a measured normal-path allocation regression. On x64, the added inherited-rasterizer flag enlarged ElementDrawArgs from 40 to 48 bytes. Existing draw-event objects and layered-brush captures therefore grew: UI allocation increased by 576 bytes/frame for batched cells, 960 for layered cells and 160 for the HUD, despite identical pixels and counters. The flag now occupies existing padding in the generic DrawBaseArgs context (24 bytes); ElementDrawArgs returns to 40 bytes. Both opacity helpers preserve the flag through with-copies. Pure x64 size/copy tests and native ancestor/callback tests with normalized and half opacity preserve the fallback contract. The matched runner rejects any increase in the normal UI steady allocation floor versus the immutable baseline.

After argument packing, a further pair retained a constant 16-byte normal UI increase (70,744 versus 70,728 bytes/frame for batched cells). A warmed raw-object probe measured DrawTransaction at 136 bytes instead of the 120-byte footprint restored by the fix. The renderer now caches its original device's lifetime counter once; transactions keep inline flags rather than a per-instance counter reference and capture origin. Capture-only origins use a weak sidecar removed on Dispose. Ordinary clipping does not access that sidecar. Device identity remains stable through host-device replacement or batch disposal, shared-device counts remain correct, and Dispose remains idempotent and releases counts on EndDraw failure. CPU identity/counter tests use fake devices without claiming GPU behavior; real preparation/composition tests exercise capture clipping and cleanup.

The failed single-burst native report `/tmp/mgui-normal-guards-165764.json` retains an unattributed 5,728-byte aggregate during 10,000 notifications. The separate pure exact-zero test passed in that same full suite; a focused native rerun measured zero. The final harness records five raw bursts and checks their steady floor, without dropping outliers or attributing the nonzero total to a proven cache allocation.

The interrupted `/tmp/mgui-t22-matched-final-20261018` directory is pre-packing diagnostic evidence, not a complete final benchmark; its name does not imply acceptance. The earlier `/tmp/mgui-t22-matched-20261018` contains the first pair that exposed the 48-byte Prepare regression.

## Matched timing procedure

```sh
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tools/render-cache -v
flock --close /tmp/mgui-render-cache-dotnet.lock dotnet test MGUI.Tests/MGUI.Tests.csproj -c Release --nologo -v:q
python3 tools/render-cache/compare_normal.py --output /tmp/mgui-normal-comparison
```

The comparison directory must be new. The normal benchmark command and its nine-argument host CLI remain supported. The new host mode is an optional tenth `normal|disabled` argument; frame records add `Prepare` and `Cache` fields. The historical 54-run baseline is read-only.

Default comparison coverage is eighteen workloads × three repetitions × two modes: 108 native runs, each with 120 warmup and 300 measured presented frames. Variant order alternates each repetition. Exact screenshot hashes, frame states and all world/UI/total native counters must match across modes and the retained baseline. No cache target work or steady Prepare allocation is permitted. Diagnostic frame counts can skip historical matching but still require matched-mode equivalence.

The runner retains per-run compressed JSON, PNGs, logs, complete source/dirty fingerprints, commands, runtime, CPU and owned-display renderer information. It rejects source drift. Use an isolated source snapshot when other agents are editing, and prevent concurrent builds during timing with the shared dotnet lock around `--skip-build`; do not hold that lock around a script that will acquire it for a build. The benchmark itself owns the graphics lock and private 1920×1080 display. Do not add an outer graphics lock.

Per-sample UI-plus-Prepare and the sum of UpdateLayout, Prepare, WorldSubmission, UiSubmission and Present are computed before percentiles. The latter is an instrumented CPU/wall-phase subtotal, not complete frame latency or GPU time. Update/layout/input work remains included. The runner reports the design contract's median/p95 noise gates for current normal versus current disabled, and separately reports current normal versus historical normal timing and allocation differences. Historical timing uses the same recorded workload/backend/hardware but a different source and capture date; it is a regression signal rather than a matched current-source proof. Wide noise bands are inconclusive evidence of overhead. No hardware FPS claim follows from llvmpipe.

## Recorded timing evidence

The complete 108-run capture `/tmp/mgui-t22-matched-complete-20261018` preceded the visual-only CPU specialization. All exact pixel/state/native-counter gates, zero cache resource gates, zero Prepare allocation gates and historical normal UI allocation-floor gates passed. Current normal versus disabled timing passed 68 of 72 gates; the four failures below are retained. The three library assembly hashes and full source/dirty fingerprints are recorded in the capture.

Times below are milliseconds, the median across the three per-run medians/p95 values; each cell is normal → disabled. These are equivalent geometries within each row. Rows represent different workloads and do not establish equivalence between batching and layering.

| Workload | Size / scale | Update median | Update p95 | UI + Prepare median | UI + Prepare p95 |
|---|---|---:|---:|---:|---:|
| batched | 1920×1080 / 1 | 0.1285 → 0.1257 | 0.1975 → 0.1739 | 1.9533 → 1.9073 | 2.3702 → 2.3238 |
| layered | 1920×1080 / 1 | 0.1356 → 0.1358 | 0.1905 → 0.1790 | 2.6156 → 2.6296 | 3.8561 → 3.8263 |
| hud | 1920×1080 / 1 | 0.0909 → 0.0853 | 0.2292 → 0.2022 | 0.1130 → 0.1119 | 0.2687 → 0.3098 |
| animated-score | 1920×1080 / 1 | 0.3581 → 0.4083 | 0.6080 → 0.7019 | 0.2119 → 0.1957 | 0.2785 → 0.2694 |
| scrolling | 1920×1080 / 1 | 0.1232 → 0.1237 | 0.4883 → 0.4880 | 0.1996 → 0.2035 | 0.7444 → 0.7165 |
| nested-clips | 1920×1080 / 1 | 0.8497 → 0.8387 | 1.0468 → 1.0649 | 0.3398 → 0.3383 | 0.4011 → 0.3972 |
| batched | 1920×1080 / 1.5 | 0.0983 → 0.0942 | 0.1381 → 0.1461 | 1.7323 → 1.7856 | 2.1832 → 2.1911 |
| layered | 1920×1080 / 1.5 | 0.1048 → 0.1036 | 0.1521 → 0.1450 | 2.4984 → 2.4869 | 3.4440 → 3.4949 |
| hud | 1920×1080 / 1.5 | 0.0675 → 0.0668 | 0.1263 → 0.1273 | 0.0997 → 0.0991 | 0.1781 → 0.1716 |
| animated-score | 1920×1080 / 1.5 | 0.2225 → 0.2287 | 0.5271 → 0.5589 | 0.1247 → 0.1268 | 0.2661 → 0.2603 |
| scrolling | 1920×1080 / 1.5 | 0.1120 → 0.1080 | 0.4695 → 0.6259 **flag** | 0.2063 → 0.2047 | 0.7501 → 0.9528 **flag** |
| nested-clips | 1920×1080 / 1.5 | 0.8251 → 0.8325 | 1.0120 → 1.0739 | 0.3504 → 0.3488 | 0.4143 → 0.4115 |
| batched | 1280×720 / 1 | 0.0927 → 0.0859 | 0.3737 → 0.3293 | 1.3897 → 1.2862 | 3.3216 → 2.7778 |
| layered | 1280×720 / 1 | 0.0741 → 0.0749 | 0.1080 → 0.1116 | 2.1997 → 2.1946 | 3.2481 → 3.2374 |
| hud | 1280×720 / 1 | 0.1293 → 0.1337 | 0.1776 → 0.1826 | 0.2346 → 0.2140 | 0.2922 → 0.2984 |
| animated-score | 1280×720 / 1 | 0.5759 → 0.6439 | 0.7794 → 0.9016 | 0.2549 → 0.2685 | 0.3384 → 0.3660 |
| scrolling | 1280×720 / 1 | 0.1087 → 0.1290 **flag** | 0.5383 → 0.6390 | 0.2687 → 0.3664 **flag** | 0.8222 → 1.1238 |
| nested-clips | 1280×720 / 1 | 0.7064 → 0.7355 | 0.9182 → 0.9951 | 0.3251 → 0.3339 | 0.4354 → 0.4929 |

| Flagged current comparison | Delta (ms) | Threshold (ms) |
|---|---:|---:|
| scrolling 1920×1080 s1.5, UpdateLayout.p95 | 0.15640 | 0.05220 |
| scrolling 1920×1080 s1.5, UiPlusPrepare.p95 | 0.20270 | 0.07501 |
| scrolling 1280×720 s1, UpdateLayout.median | 0.02025 | 0.02000 |
| scrolling 1280×720 s1, UiPlusPrepare.median | 0.09760 | 0.05290 |

Targeted unchanged-source captures `/tmp/mgui-t22-repeat-scrolling-20261018` and `/tmp/mgui-t22-repeat-historical720-20261018` passed all 16 matched timing gates. The original four flags remain recorded; moving flags and wide spread limit claims about wrapper overhead. The historical 720p batched UI median flag disappeared on repeat, while nested-clip Update median remained approximately 0.0624 ms above the historical capture.

The bounded causality cohort `/tmp/mgui-t22-causal-nested720-20261018` recreated original T01 commit `51822f1`: all 174 retained library/fixture/tool/font/effect source hashes matched. Both variants used the byte-identical original driver and fixture, seed 1729, 120 warmup and 300 measured frames, with three alternating runs. The current library binaries matched the tested frozen source. State, pixels and native counters matched each other and the immutable baseline exactly. Prepare is omitted equally, so this isolates library Update/UI cost rather than the full new host protocol. Old Update medians were 0.62295/0.63000/0.65170 ms and current medians 0.73725/0.76260/0.72845 ms; the median difference +0.10725 ms exceeded the 0.05750 ms noise gate. UI median passed, UI p95 did not. This was treated as a real investigation signal.

An isolated counted projection measured 250,930 direct visual invalidations and 1,693,525 executions including ancestor propagation over 300 nested-clip frames. Every execution was visual-only with eligibility tracking absent; 234,600 origin changes and 16,030 actual-bound changes accounted for nearly all direct notifications. VisualState never changed. Tier1 disassembly showed Origin/VisualState setters inlined in both old and current code.

The specialized path retains each notification, stamp deduplication, epoch checks, overflow fallback, parent snapshot order and event behavior. Layout, structure, dependencies and active eligibility use the general path. A CPU-only seven-node notification probe measured warmed 39.95–41.12 ns before versus 25.50–25.90 ns after, with exactly zero measured bytes in all five repeats after taking the allocation snapshot before Console formatting. This predicts roughly 13 microseconds saved per fixture frame; it does not explain or claim to remove the entire original timing difference. Both noisy exploratory native cohorts (`/tmp/mgui-t22-causal-fast720-20261018`, `/tmp/mgui-t22-causal-current-fast720-20261018`) remain retained, with their raw spreads; passing wide gates alone is inconclusive.

The promoted specialization passed the final integrated suite: **566/566 tests, zero skipped** (`/tmp/mgui-t22-fast-integrated.trx`). The final bounded cohort `/tmp/mgui-t22-causal-final720-20261018` used the exact tested libraries and the original T01 driver. All source/build fingerprints remained stable and all pixels/state/native counters matched.

| Variant / run | Update median (ms) | Update p95 (ms) | UI median (ms) | UI p95 (ms) |
|---|---:|---:|---:|---:|
| old / 1 | 0.64640 | 0.97610 | 0.30650 | 0.37490 |
| old / 2 | 0.65860 | 0.95640 | 0.29995 | 0.38260 |
| old / 3 | 0.64090 | 0.94760 | 0.30500 | 0.37710 |
| current / 1 | 0.67750 | 0.91650 | 0.31765 | 0.39340 |
| current / 2 | 0.67890 | 0.86420 | 0.32185 | 0.38490 |
| current / 3 | 0.68880 | 0.91280 | 0.32255 | 0.40360 |

| Final old/current gate | Delta (ms) | Threshold (ms) | Outcome |
|---|---:|---:|---|
| UpdateLayout.median | +0.03250 | 0.03540 | pass |
| UpdateLayout.p95 | -0.04360 | 0.09564 | pass |
| UiSubmission.median | +0.01685 | 0.02000 | pass |
| UiSubmission.p95 | +0.01630 | 0.05000 | pass |

The final Update median retains a measured +0.03250 ms cost (0.64640 → 0.67890 ms), inside the predeclared 0.03540 ms spread-aware gate. It is close to that boundary and is not evidence of zero CPU overhead. The original +0.10725 ms result, exploratory cohorts, full 108-run result and targeted follow-ups remain part of the record. Different cohort conditions also affect the difference, so only the CPU probe isolates the approximately 13 µs specialization benefit. The full 108 runs were not repeated after this narrow visual-notification specialization; the final 566-test native/pure suite and bounded causal workload validate its correctness and affected CPU path.
