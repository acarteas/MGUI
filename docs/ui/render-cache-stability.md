# Final mixed workload and resource stability validation

Validation completed on `64cbd488562e493eb0070156bf432d0428f2fdd7` (T32 component traversal remediation) plus the six T27 files. The fresh detached projection is `/tmp/mgui-t27-post-t32-final`; source SHA-256 is `2a9492089dac6af87e457a77bc14bb6d42653f5140d7b1ad3389809b58c5c607`, fixture SHA-256 `2530be6c077324c9e8b2d38a7c109a7ffddefce80c6b1d512bcdd8bda733fb9e`. Final report prose was written after capture and is excluded from the frozen runtime projection. All existing T23/T26 routes and the original seven user files are preserved.

Fresh accepted evidence contains **54 ordinary runs, 63 mixed pixel preflights, 189 mixed timed runs, three 400-frame resource preflights and three actual 10,000-presented-frame soaks**. All deterministic assertions pass, with **14,200 exact cached/normal frame-hash comparisons**:12,600 mixed,800 resource preflight and800 sampled soak. The ordinary54 also match immutable baseline state, final pixels and native counters. Root independently verified all888 accepted artifact hashes,189 timed state/image matches and30,000 soak-frame accounting checks in `/tmp/mgui-t27-root-final-evidence-review.json`, and visually spotchecked the accepted idle1080/scale1 panel image; that visual inspection adds no parity cases.

These small-row fixtures demonstrate substantial cache submission losses on the exercised software renderer despite reuse and allocation/draw-count benefits. They do not establish a blanket bitmap-cache performance benefit. Detailed results and retained ordinary-path investigation appear below.

`StabilityFixture`, `StabilityGame`, and `tools/render-cache/stability.py` add a separate `--stability <request.json>` entry point. They preserve the six original benchmark scenarios and the immutable54 T01 captures. The added modes use the same names as T23: `normal`, `background`, `panel`. `normal` installs no wrappers; `background` wraps the twelve fixed static fills; `panel` places each of the three static rows inside an explicit boundary. Score/button/scroll content remains ordinary sibling content. All modes use identical opaque art, labels, source colors, layout, input and clock. These small mixed fixtures extend the comparison; they do not replace T23's stable layered/effect workload or establish a blanket cache benefit.

Every workload runs normal Update/input/layout, then Prepare before scene rendering, then the moving checkerboard world and final UI, then actual Present. A frame becomes a sample only when `base.EndDraw()` returns. Three genuine drawable presents precede120 UI warmup frames,300 measured frames, and one extra unmeasured capture/present frame per run; three repetitions per mode use rotating variant order. Configurations are1920×1080 at layout scale1/1.5 and1280×720 at scale1. This uses layout UI scaling, not a window bitmap transform. Verification runs read back every measured frame after actual warmup presents; they are excluded from performance summaries. Before timed samples, native drawable/viewport dimensions, visible text, moving checkerboard and two independently known opaque far-corner world colors are checked. Exact RGBA frame hashes and final PNG hashes must match normal before measurements are accepted. Timed runs must reproduce that complete deterministic state sequence and final image.

| Workload | Fixed schedule and asserted evidence |
| --- | --- |
| `idle-hud` | Stable static rows and live HUD. Positive native builds/hits/entries/bytes must be established by the end of warmup; every measured frame has zero additional static builds, discovery visits or source getter reads. |
| `hover-press` | Real host mouse moves to the button; press on frames20–29 of each60frame cycle and release to click. Snapshot records actual VisualState/click count from InputTracker processing. Static siblings still perform zero additional builds/discovery. |
| `pulse` | Score text increments once per120frames; live foreground/scale follows a24frame sine pulse at deterministic60Hz. Midpoint reaches1.18scale/white, frame24 restores scale1/gold, exactly400ms after onset. Static siblings keep zero builds/discovery. |
| `text` | A static row text changes between equal-width labels every60frames; ordinary/background/panel modes must agree through miss, observed generation and recovery. |
| `scrolling` | Ordinary scroll sibling changes offset every frame through the real scroll viewer update/draw path. Static siblings remain warm. |
| `resize-scale` | Window dimensions and inherited layout scale change every60frames; all affected boundaries recover through normal observation, with matched geometry/pixels. |
| `continuous-animation` | Explicitly volatile static row/owners paint directly while source color changes every frame, exposing an unfavorable cache workload without lowering its update frequency. |
| `resource-soak` | Each25frame block selects one of16 operation phases;10000 measured presented frames repeat complete equivalent lifecycle cycles. Both background and panel variants must actually allocate/capture and evict under pressure. |

The soak repeats show/hide, normal recovery, resize/layout scale, same-window reparenting, disable/re-enable, entry pressure (`MaximumEntries=2`), byte pressure (`MaximumRetainedBytes=80000`), Clear, generation mutation, mutation after Prepare, and stable recovery/idle. Default32MiB/64entry/4096dimension policy is checked before pressure is applied. Every Update, post-Prepare, in-flight capture, and post-Draw check enforces both charged limits and `Allocations-Disposals==EntryCount`. A preallocated binding array inspects the actual native in-flight target without per-check array allocation; its bytes must already be charged. After Prepare, late mutation changes both static text and the first source to a contrasting opaque color. The old charged targets cannot be synchronously destroyed; the first row and changed source must both paint directly, and the exact changed-fill pixel must match. Each Clear and final desktop teardown must reach zero bytes/entries/pending and equal allocations/disposals. Repeated complete Clear cycles prevent bounded-LRU occupancy from hiding target growth. A400frame exact normal/cached pixel preflight covers all16 unique operation phases before the10000frame resource lane. All three modes then actually present10000 measured frames, with400 sampled native sentinel/full-pixel hashes at operation transitions; full state sequences and sampled pixels must match the normal oracle. Readbacks stay outside measured phases. This is resource evidence; no timings are claimed from the soak. Warm static performance checks start at the warmup baseline without excluding initial measured frames; cursor hover for hover/pulse schedules is established during warmup. Resource-soak recovery intervals separately establish their stable baseline eight frames after churn.

The raw schema records CPU median/p95 inputs and game-thread managed allocations for UpdateLayout, Prepare, WorldSubmission, UiSubmission and Present; native GraphicsMetrics draw/primitives/sprites/target counts for Prepare and final world/UI; native texture-change deltas; cumulative hits/misses/builds/evictions/allocations/disposals; current charged bytes/entries/pending; and deterministic state/operation fields. UI+Prepare and the instrumented phase subtotal are summed per raw sample before percentiles. The subtotal excludes uninstrumented host/check/readback work and is not complete frame latency or GPU time. Detailed effect/clip/state-transition diagnostics remain the separately scoped accepted T23 trace evidence; final mixed runs record real native draw/texture/target counts, which are not presented as all state breaks. GPU timing remains explicitly unavailable on the exercised DesktopGL API. Renderer/environment observations and load journals are attached to the evidence packet; no hardware FPS claim follows from llvmpipe CPU measurements.

## Reproduction and provenance

Release host/tests builds passed (host:four existing warnings,zero errors; tests:zero warnings/errors). All35 Python validators passed, including17 stability validators;73 focused pure trace/manager/source/ownership contracts passed with zero skips. T32 separately passed the full582-test suite before this new capture; T27 did not repeat that full suite.

The driver never builds or nests the dotnet lock. It owns a private1920×1080 Xvfb and the graphics lock. Actual backend:MonoGame DesktopGL3.8.4.1, .NET8.0.31, Mesa26.2.3 llvmpipe/LLVM23.1.1, Release/default build with diagnostic trace calls erased. There are no GPU timer measurements. Shared-load journals observe only known process statistics at1Hz; boundary uncertainty and correlation cannot establish causality or an idle machine. No hardware FPS, full-frame latency or other-backend claim is made.

Actual serial launch commands used an external load observer wrapper:

```sh
PYTHONDONTWRITEBYTECODE=1 flock --close /tmp/mgui-render-cache-dotnet.lock python3 /tmp/mgui-t27-post-t32-journaled-lane.py normal
PYTHONDONTWRITEBYTECODE=1 flock --close /tmp/mgui-render-cache-dotnet.lock python3 /tmp/mgui-t27-post-t32-journaled-lane.py mixed
```

The mixed lane was resumed with argument `mixed 2` under owned signals-only `strace -f --seccomp-bpf -ttt -e trace=none -e signal=SIGTERM,SIGINT,SIGHUP,SIGKILL`; no syscalls were traced. The first four complete cohorts were retained; all remaining17 workload cohorts and the resource cohort have this observer scope. The resulting instrumentation is an additional timing limitation, not a reason to discard slow cache results. The normal54 lane completed without interruption or tracing.

`/tmp/mgui-t27-post-t32-freeze.json` records explicit Release builds, exact own-source projection and all34 actual DLL/font/effect/runtime/configuration asset identities. Source/default assets are checked before and after captures; copied test Core/Shared/font DLLs equal host copies. Every raw mixed/soak artifact carries source/fixture/default binary provenance. The normal-report hash is bound into mixed checkpoint identity and was not changed after the mixed lane began.

Evidence paths:

- Complete provenance/artifact manifest:`/tmp/mgui-t27-post-t32-evidence-packet.json`.
- Normal54 summary:`/tmp/mgui-t27-post-t32-normal/summary.json`; raw-normal summary:`capture/summary.json` beneath that directory.
- Mixed/resource summary with all per-run, pooled and repeat-median phases, counters, allocations, gates and raw paths:`/tmp/mgui-t27-post-t32-mixed/summary.json`.
- Full63-row phase table:`/tmp/mgui-t27-post-t32-results.md`; all metric median/p95 columns:`/tmp/mgui-t27-post-t32-results.csv`.
- Load journals:`/tmp/mgui-t27-post-t32-load-normal-attempt-1.jsonl`, `...-load-mixed-attempt-1.jsonl`, `...-load-mixed-attempt-2.jsonl`.
- Build/pure logs:`/tmp/mgui-t27-post-t32-{host-build,tests-build,python,pure}.log` and pure TRX.

## Measured wins, losses and recovery

The following idle-HUD values are pooled median/p95 milliseconds across900 measured frames (three300-frame runs). Subtotal is summed per sample before percentiles. UI bytes include Prepare. Full tables retain all seven workloads at all three configurations and repeat spread; these selected rows are not a substitute for those results.

| Size / scale | Mode | Update | UI+Prepare | Instrumented subtotal | Update/UI bytes | UI draws/textures |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| 1920×1080 / 1 | normal | 0.1222/0.2258 | 0.3080/0.4924 | 3.8382/4.8289 | 62472/45720 | 34/34 |
| 1920×1080 / 1 | background | 0.1022/0.1731 | 2.9921/3.9463 | 4.1558/5.6548 | 62472/44784 | 34/34 |
| 1920×1080 / 1 | panel | 0.1106/0.2192 | 2.5023/2.9490 | 3.6412/4.7647 | 64728/27336 | 13/13 |
| 1920×1080 / 1.5 | normal | 0.1257/0.2407 | 0.3198/0.5015 | 4.3261/5.4957 | 62472/45720 | 34/34 |
| 1920×1080 / 1.5 | background | 0.1162/0.1839 | 5.8271/6.9164 | 7.5618/9.1685 | 62472/44784 | 34/34 |
| 1920×1080 / 1.5 | panel | 0.1248/0.2263 | 3.0438/3.7823 | 4.7058/6.0577 | 64728/27336 | 13/13 |
| 1280×720 / 1 | normal | 0.0880/0.3814 | 0.2711/0.9618 | 2.3055/3.5946 | 62472/45720 | 34/34 |
| 1280×720 / 1 | background | 0.0821/0.1312 | 4.2081/4.9115 | 5.1928/6.2476 | 62472/44784 | 34/34 |
| 1280×720 / 1 | panel | 0.0980/0.3971 | 1.7531/2.3091 | 2.7061/3.7618 | 64728/27336 | 13/13 |

The panel reduces idle UI draws/textures34→13 and UI+Prepare allocations45,720→27,336 bytes/frame, while Update allocations rise62,472→64,728. Background-only keeps34 draws and uses44,784 UI+Prepare bytes. Positive warm capture is established before measuring:background12 builds/1,416 hits/12 entries/189,952 bytes; panel3 builds/354 hits/3 entries/208,960 bytes at720/scale1 and1080/scale1. Every measured stable idle/hover/pulse/scroll sibling check has zero new builds/discovery/source reads. Cached idle Prepare itself is small (1080/scale1 median0.0053/0.0029ms background/panel), but UI submission is much slower. This is an observed software-renderer loss; its full scheduling/GPU cause was not isolated.

UI+Prepare median and p95 loss gates fail in18of21 cells for each cached mode. Continuous animation falls back to direct painting with zero measured builds and34 draws:at1080/scale1 its UI+Prepare medians are0.3155/0.3190/0.3249ms normal/background/panel, while Update allocations62,536/69,520/69,128 bytes expose tracking overhead. Cache-mode Update median gates fail in1background and3panel cells; panel Update p95 fails in2cells. These are opt-in workload costs, retained in the summaries rather than converted into machine-sensitive CI failures.

T23's expensive stable layered/static-panel fixture showed separately scoped benefits on its prior frozen source ([comparison report](render-caching-comparison.md)). Those fixtures and source/DLL identities differ from the cheap rows here; their wins are not relabeled current T27 measurements. Fewer native draw calls alone do not prove lower CPU submission time.

Initial warmup capture timings are not sampled by this mixed host. Initial build counts and charged occupancy are recorded, and accepted T23 cold/transition traces remain separately scoped. The current text/resize schedules expose later recovery costs directly. At1080/scale1, each text repeat builds5 entries in either cached mode; resize/scale builds48background or12panel entries. Conditional Prepare timing on actual build frames is derived from retained raw frames in `/tmp/mgui-t27-post-t32-build-frame-summary.json`:

| Schedule / mode | Build frames across3runs | Prepare median/p95 (ms) |
| --- | ---: | ---: |
| Text / background |15|0.3489/0.4895|
| Text / panel |15|0.4202/0.5475|
| Resize/scale / background |12|2.7815/3.6090|
| Resize/scale / panel |12|1.0749/1.3803|

These sparse costs would be hidden by an ordinary all-frame p95; the full raw Prepare samples remain available. No new measurement was run to produce this conditional summary.

## Resource and deterministic results

| Requirement | Final evidence |
| --- | --- |
| Presented frames and matched state/pixels |63mixed preflights+3resource preflights all-frame exact;189timed state sequences/final PNGs exact; all3soaks actually present10,000 measured frames,400 sampled full-pixel/sentinel checkpoints each.14,200 cached/normal comparisons total. |
| Real input and exact400ms pulse |Actual hover/press/release/click state passes at all3configurations; pulse midpoint/restoration and24ticks/400ms pass without changing Update frequency. |
| Positive warm reuse |Eligible idle/hover/pulse/scroll establishes nonzero native builds/hits/entries/bytes; zero further builds/discovery/source reads starts at the warmup baseline with no measured-frame exclusion. |
| Late generation and retained charge |Each10k soak includes25after-Prepare mutations of text and contrasting first-fill color. Direct source paint, exact changed pixel and deferred retired charge pass; no stale cached generation is sampled. |
| Bounded retired/in-flight resources |Every Update/postPrepare/inflight/postDraw phase passes effective byte/entry limits and allocation-disposal accounting, including entry limit2 and byte limit80,000. Default maximum32MiB/64entries/4096dimensions checked before pressure. |
| Repeated Clear and teardown |Every Clear reaches zero; final bytes/entries/pending0, allocations equal disposals. Normal0/0; background7,037/7,037; panel2,228/2,228. |
|10k cache churn |Background92,725 measured hits/7,025 builds/5,650 evictions; panel23,925 hits/2,225 builds/1,850 evictions. Warm allocations precede these measured intervals. Peak charged bytes239,904/264,032 and entries12/3 stay below default limits; every pressure frame separately obeys its reduced limits. |
| In-flight and recovery proof |7,037 background and11,140 panel in-flight assertions (assertions may repeat within a capture),25retirement checks and800stable-recovery checks per mode. Soak timing is not claimed as performance evidence. |
| Validator/compile |Current35Python/73focused pure contracts pass; explicit current host/tests builds pass. Invalid clock/input/pulse/resources/binary/renderer/checkpoint evidence is rejected. |

## Ordinary-path regression and retained history

Historical cross-date normal comparison still flags animated720 Update:immutable baseline median0.43425ms versus current repeat medians0.54285/0.54825/0.55215. The raw `regressionInvestigationRequired` flag remains true; it was investigated rather than manually cleared. T27's earlier identical-original-host cohort had wide animated spread, and T32's final original-host comparison measured old0.54820→fixed0.55325ms (+0.00505ms inside a broad0.10150ms gate). This explains why the cross-date trigger is not proof of a new0.1ms source cost; it does not prove zero small overhead. No new contradictory result required another capture.

The earlier two nested1080 Update flags were reproduced with an identical T01 executable and original/current libraries, then remediated in closed Bead `mgui-9sb.3.5` by commit `64cbd488562e493eb0070156bf432d0428f2fdd7`. Current nested allocation is627,000bytes/frame instead of1,350,520, and both historical flags are gone. The original visual revision notifications and child snapshots remain. T32's final Update improvement, retained first UI failure, positive final UI residual within spread, and noisy tail/control limits are documented in [the remediation report](render-cache-update-regression.md). This is not a zero-overhead claim.

Pre-fix54ordinary runs and one complete mixed cohort remain under `/tmp/mgui-t27-final-normal` and `/tmp/mgui-t27-final-mixed`; `/tmp/mgui-t27-causal-evidence-index.json` and `/tmp/mgui-t27-attribution-evidence-index.json` retain18original/current causal runs, two counted native cells and CPU probes. They are not reused as post-T32 checkpoints. Preliminary fixture failures (nullable foreground, open PNG stream, entering hover before stable layout) were corrected before final freeze and their logs remain retained. Preliminary720p5000comparisons and the checkpoint/resume smoke are separate evidence, not added to final14,200.

The fresh mixed attempt1 stopped with unexplained SIGTERM after196.34s, with four complete cohorts retained and scrolling incomplete. All144complete artifact hashes and frozen identities were verified; the incomplete cohort was repeated whole. The original cause remains unestablished. Initial process inspection incorrectly used sandbox `/proc`, which cannot see host PIDs; the corrected host-context audit found and terminated only the exact owned private-Xvfb orphan, then confirmed cleanup. `/tmp/mgui-t27-post-t32-mixed-interruption-1.json` preserves this correction. Recovery's signals-only log records normal helper-owned Xvfb termination and successful wrapper exit; it does not retrospectively explain the first SIGTERM.

Atomic `cohorts.json` checkpoints require identical source, default binaries, fixture/configuration, normal-report hash and renderer, and verify retained raw/image/log hashes. Normal cohorts contain nine runs; mixed cohorts contain all three preflights and nine timed runs; resource acceptance covers all three400frame preflights and all three10ksoaks. Failed/interrupted attempts are retained, never silently promoted. Child deadlines remain60sdiagnostic/180sordinary/600sactual10k; no timeout was widened. Both locks and all recorded owned host identities were verified released after capture.
