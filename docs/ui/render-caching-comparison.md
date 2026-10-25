# Equivalent rendering: batching and selective cache comparison

## Decision

No batching change justified. Keep normal rendering available and opt into a content boundary only when its static region is expensive enough to repay capture. In these fixtures, a whole stable panel is the useful cache granularity; wrapping 36 small cell backgrounds is slower despite fewer native draw calls. This is not evidence against caching a few expensive game HUD backgrounds. Continuously dirty and scrolling content remains live and shows no useful cache benefit. Eleven of twelve stable or single-change panel/configuration pairs pass the benefit gate. Layered 1080p/scale 1.5 is inconclusive: its 0.98545 ms median saving does not exceed twice the 1.36780 ms reference repeat-median spread, despite a large observed median improvement. That group predates the external workflow; no load attribution or amortization claim is made for it.

## Scope and reproducibility

All results use the frozen source rooted at `dca1319ff5571e9228672883dd970e35eccfa1a2` plus this task's manifest files in `/tmp/mgui-t23-validation`. The original six T01 fixtures, nine-argument entry point, normal/disabled ten-argument behavior, and immutable 54-run baseline remain intact. New `compare-*` fixtures are intentionally distinct: 36 cells (six rows of six), an opaque panel/cell base, optional translucent nine-slice and effect decoration, and an ordinary score sibling outside the panel. Every mode within a fixture paints identical pixels. The original 48-cell batched/layered fixtures have different pixels from each other and are not treated as equivalent optimization variants.

Four modes are measured: normal, normal-batching (identical implementation, separately captured), background (36 cached fill sites), and panel (one explicit `MGRenderCache`, with disabled static-assertion brush wrappers allowing the audited effect source inside the boundary). The mixed score changes every Update outside the boundary; dirty changes a cell every Update; scroll moves its viewport every Update; change edits one cell halfway through measurement. Update and input handling remain active in every mode. Untimed churn additionally resizes a cell and reparents cells between rows. T27 owns long mixed/churn/soak performance coverage.

The shader assertion is limited to `EffectParameters.fx` with `CustomInt=102`: that branch returns fixed premultiplied `CustomColor * input.Color`, before the fallback reading time, position, UV and interaction values. The owned texture is fixed; painting stays within the local rectangle. The benchmark does not assert an arbitrary effect static. Known volatility and resource lifetime checks still apply.

Backend: MonoGame DesktopGL 3.8.4.1, Release, llvmpipe software rendering on AMD Ryzen 7 5800X, Linux 7.2.7. This is CPU submission/update and software-renderer evidence, not hardware FPS or GPU execution time. Each of 18 workloads has four modes × three repeats, 120 warmup frames and 300 measured/presented frames: 216 captures. Variant ordering alternates/rotates. The legacy totalPresentedFrames field counts 421 UI presents (120 warmup + 300 measured + one screenshot); the three clear/Present drawable warmups make 424 actual physical presents. Untimed parity similarly records 21 UI presents plus three drawable presents (24 total). A private 1920×1080 Xvfb display is owned by the helper. Before constructing the comparison UI, three clear/Present frames establish the requested drawable dimensions. The original fixtures retain their original startup.

Every summed phase is calculated per raw frame before percentiles. Table entries are the median of three run medians / median of three run p95s, in milliseconds. UI+Prepare includes pre-scene preparation. Update, WorldSubmission, UiSubmission, Prepare and Present are separately retained; their sum is an instrumented subtotal, not total frame latency. Present is wall time and is not a GPU timer.

Raw evidence and provenance:

- [216-run summary](/tmp/mgui-t23-matched-20261018/summary.json), [source fingerprint](/tmp/mgui-t23-matched-20261018/source.json); each summary run links its compressed raw samples and PNG. All native DLL hashes and commands are recorded.
- [Transient exact-pixel report](/tmp/mgui-t23-transient-parity/summary.json), [original source](/tmp/mgui-t23-transient-parity/source.json), [corrected analysis source](/tmp/mgui-t23-transient-parity/validation-source.json). Twenty-four initial captures were revalidated, not recaptured, after fixing an analysis assumption about first-hit frame. Native source stayed identical and no DLL was rewritten; binary hashes/timestamps are retained.
- [Separate diagnostic trace summary](/tmp/mgui-t23-state-traces/summary.json), [trace source](/tmp/mgui-t23-state-traces/source.json). Its C#/project/shader inputs equal the timing source; `DefineConstants=MGUI_RENDER_TRACE` and different binary hashes identify the instrumented build. Trace timings are never used below.
- [Hardware record](/tmp/mgui-t23-hardware.json), [frozen own-file manifest](/tmp/mgui-t23-freeze.json).

## Interrupted capture and shared system load

The original run stopped on a 180-second native timeout in mixed/1080p/scale 1.5/background. The failed log and two completed captures from that incomplete group are retained; those two are excluded and repeated to restore an intact matched group. Ninety-six captures from eight complete groups are reused. All 98 completed captures predate the separate external UI-capture workflow, and the stalled command began before that workflow too; the timeout cannot be attributed to it. The helper cleaned up its own child/display. An unchanged bounded 60-second diagnostic retry completed in 4.02 seconds and is excluded from accepted timing. No external process was stopped or modified.

The remaining 120 captures run while the independent workflow remains active. A read-only, approximately one-second journal records known process trees, CPU ticks and system load; it does not read command lines or environment. These are matched comparisons under shared system load, not a pristine idle-machine benchmark. Per-run process windows and external CPU/load observations are linked below. Sampling misses some process-boundary ticks, and correlation cannot establish causality. Cases whose measured spread defeats the benefit gate are inconclusive for benefit; slower cases remain documented losses.

- [Preserved timeout log](/tmp/mgui-t23-matched.log), [bounded retry diagnostic](/tmp/mgui-t23-bounded-retry/diagnostic.json), [resume and merge script](/tmp/mgui-t23-resume.py).
- [Load journal](/tmp/mgui-t23-load-resume.jsonl), [journal metadata and hashes](/tmp/mgui-t23-load-resume.jsonl.metadata.json), [per-run load correlation and process transitions](/tmp/mgui-t23-load-correlation.json).

The resumed journal spans 371 samples: external-tree CPU use has median 1.018 logical-core equivalents (observed interval range 0.879–2.182), and one-minute system load has median 6.160 (range 2.880–10.245) on 16 logical CPUs. These are distinct measurements: system load includes this benchmark and all runnable/uninterruptible work. The observed external native process changes from PID 375890 to PID 392826 around 14:28:45–14:28:46 UTC; no case name is inferred. The per-run correlation file records this transition against individual captures. The original 96 accepted runs have no retrospective journal.

## Four-mode UI plus Prepare

For each row, the reference is whichever identical normal implementation has the smaller repeat-median. A stable benefit requires ≥20% improvement, absolute saving greater than twice that reference's repeat-median spread, and p95 nonregression. The p95 allowance is `max(0.05 ms, 10% of reference p95, twice reference repeat-p95 spread)`. The raw summary retains every threshold, spread, gain and per-run value. “Pass” below is a timing gate, not a recommendation for dirty/scrolling workloads.

| Fixture / configuration | Normal med / p95 | Normal-batching med / p95 | Background med / p95 | Panel med / p95 | Background / reference | Panel / reference | Benefit gate B / P |
|---|---:|---:|---:|---:|---:|---:|---|
| cells / 1920×1080 s1 | 1.0254 / 1.1660 | 1.0265 / 1.1887 | 3.6102 / 4.0037 | 0.0678 / 0.1482 | 3.52× | 0.07× | fail / pass |
| layered / 1920×1080 s1 | 1.0189 / 1.7346 | 1.0069 / 1.7526 | 3.7478 / 4.1589 | 0.0647 / 0.1611 | 3.72× | 0.06× | fail / pass |
| mixed / 1920×1080 s1 | 1.0551 / 2.0325 | 1.0347 / 1.9809 | 3.6647 / 4.3041 | 0.1067 / 0.1742 | 3.54× | 0.10× | fail / pass |
| dirty / 1920×1080 s1 | 1.0219 / 1.8521 | 1.0269 / 1.9388 | 3.6992 / 4.9370 | 1.0737 / 3.0141 | 3.62× | 1.05× | fail / fail |
| scroll / 1920×1080 s1 | 0.7339 / 1.0009 | 0.7505 / 1.0051 | 0.8246 / 1.2350 | 0.7384 / 0.9822 | 1.12× | 1.01× | fail / fail |
| change / 1920×1080 s1 | 1.0259 / 1.7099 | 1.0320 / 1.7245 | 3.8419 / 4.3093 | 0.0696 / 0.1527 | 3.74× | 0.07× | fail / pass |
| cells / 1920×1080 s1.5 | 1.0345 / 1.1688 | 1.0540 / 1.4009 | 4.4447 / 5.6233 | 0.0663 / 0.1288 | 4.30× | 0.06× | fail / pass |
| layered / 1920×1080 s1.5 | 1.1448 / 1.7311 | 1.0486 / 1.6975 | 4.3787 / 4.8333 | 0.0631 / 0.1436 | 4.18× | 0.06× | fail / fail |
| mixed / 1920×1080 s1.5 | 1.1657 / 1.8652 | 1.1787 / 1.8608 | 4.6988 / 5.3476 | 0.0784 / 0.2018 | 4.03× | 0.07× | fail / pass |
| dirty / 1920×1080 s1.5 | 1.1780 / 1.9006 | 1.1745 / 1.8719 | 5.0063 / 5.9860 | 1.2180 / 1.5895 | 4.26× | 1.04× | fail / fail |
| scroll / 1920×1080 s1.5 | 0.8509 / 1.8333 | 0.8379 / 1.7430 | 0.9067 / 1.9755 | 0.8667 / 1.7530 | 1.08× | 1.03× | fail / fail |
| change / 1920×1080 s1.5 | 1.2059 / 1.9011 | 1.2109 / 1.8746 | 4.6700 / 5.3315 | 0.0792 / 0.1495 | 3.87× | 0.07× | fail / pass |
| cells / 1280×720 s1 | 0.9903 / 1.4048 | 0.9889 / 1.3655 | 2.4541 / 2.8723 | 0.1113 / 0.1526 | 2.48× | 0.11× | fail / pass |
| layered / 1280×720 s1 | 0.9426 / 1.9316 | 0.9394 / 1.9372 | 2.1146 / 3.5225 | 0.1032 / 0.1442 | 2.25× | 0.11× | fail / pass |
| mixed / 1280×720 s1 | 0.9491 / 2.7107 | 0.9597 / 2.4316 | 1.1310 / 2.2415 | 0.1106 / 0.1585 | 1.19× | 0.12× | fail / pass |
| dirty / 1280×720 s1 | 0.9749 / 3.0375 | 0.9799 / 2.0899 | 2.8495 / 4.0400 | 1.0815 / 3.4023 | 2.92× | 1.11× | fail / fail |
| scroll / 1280×720 s1 | 0.6844 / 2.3103 | 0.7598 / 2.3056 | 0.7393 / 1.1083 | 0.7165 / 2.1934 | 1.08× | 1.05× | fail / fail |
| change / 1280×720 s1 | 0.9411 / 1.8764 | 0.9590 / 2.0988 | 1.0255 / 2.0687 | 0.1074 / 0.1448 | 1.09× | 0.11× | fail / pass |

## Phase, allocation and resource interpretation

At 1080p/scale 1, the following repeat-median values show where time and resources went. Full distributions for all configurations are in the raw summary. Allocation columns sum UI+Prepare per sample before taking medians. Cache counters are cumulative; a build delta is last minus first measured frame (299 intervals). Entries/bytes are end-of-interval retained resources. Native UI calls and TextureChanges are measured GraphicsDevice deltas; texture changes include backend binding behavior, not an inferred number of batches.

| Fixture / mode | Update ms | Prepare ms | UI ms | Present ms | Instrumented subtotal ms | UI+Prepare bytes/frame | UI calls / texture changes | Builds Δ | Entries / retained bytes |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| cells / normal | 0.0801 | 0.0010 | 1.0244 | 2.4451 | 3.9887 | 57,240 | 75 / 75 | 0 | 0 / 0 |
| cells / normal-batching | 0.0794 | 0.0010 | 1.0250 | 2.4579 | 3.9973 | 57,240 | 75 / 75 | 0 | 0 / 0 |
| cells / background | 0.0851 | 0.0095 | 3.6010 | 0.5052 | 4.4980 | 54,384 | 75 / 75 | 0 | 36 / 846,720 |
| cells / panel | 0.0996 | 0.0022 | 0.0656 | 2.1953 | 2.7145 | 6,960 | 3 / 3 | 0 | 1 / 1,295,920 |
| layered / normal | 0.0811 | 0.0010 | 1.0174 | 2.7472 | 4.1411 | 126,360 | 147 / 147 | 0 | 0 / 0 |
| layered / normal-batching | 0.0791 | 0.0010 | 1.0059 | 2.7334 | 4.1007 | 126,360 | 147 / 147 | 0 | 0 / 0 |
| layered / background | 0.0814 | 0.0101 | 3.7374 | 0.5061 | 4.6325 | 54,384 | 75 / 75 | 0 | 36 / 846,720 |
| layered / panel | 0.0960 | 0.0021 | 0.0629 | 2.2122 | 2.7341 | 6,960 | 3 / 3 | 0 | 1 / 1,295,920 |
| mixed / normal | 0.3470 | 0.0013 | 1.0539 | 2.8676 | 4.5503 | 126,360 | 147 / 147 | 0 | 0 / 0 |
| mixed / normal-batching | 0.3420 | 0.0013 | 1.0334 | 2.8293 | 4.5056 | 126,360 | 147 / 147 | 0 | 0 / 0 |
| mixed / background | 0.3425 | 0.0103 | 3.6550 | 0.5348 | 4.8462 | 54,384 | 75 / 75 | 0 | 36 / 846,720 |
| mixed / panel | 0.4945 | 0.0070 | 0.1006 | 2.4265 | 3.4809 | 6,960 | 3 / 3 | 0 | 1 / 1,295,920 |
| dirty / normal | 0.3624 | 0.0012 | 1.0208 | 2.8344 | 4.5265 | 126,360 | 147 / 147 | 0 | 0 / 0 |
| dirty / normal-batching | 0.3703 | 0.0012 | 1.0253 | 2.8470 | 4.5473 | 126,360 | 147 / 147 | 0 | 0 / 0 |
| dirty / background | 0.5227 | 0.0115 | 3.6888 | 0.5890 | 5.1902 | 56,384 | 77 / 77 | 0 | 35 / 823,200 |
| dirty / panel | 0.5194 | 0.0010 | 1.0729 | 2.9028 | 4.8140 | 127,296 | 147 / 147 | 0 | 0 / 0 |
| scroll / normal | 0.1058 | 0.0010 | 0.7327 | 2.4809 | 3.6357 | 89,112 | 100 / 100 | 0 | 0 / 0 |
| scroll / normal-batching | 0.1176 | 0.0011 | 0.7494 | 2.5268 | 3.6907 | 89,112 | 100 / 100 | 0 | 0 / 0 |
| scroll / background | 0.1392 | 0.0011 | 0.8223 | 2.6648 | 3.9423 | 89,112 | 100 / 100 | 0 | 0 / 0 |
| scroll / panel | 0.1313 | 0.0008 | 0.7373 | 2.4802 | 3.6403 | 90,048 | 100 / 100 | 0 | 0 / 0 |
| change / normal | 0.0825 | 0.0010 | 1.0251 | 2.7509 | 4.1475 | 126,360 | 147 / 147 | 0 | 0 / 0 |
| change / normal-batching | 0.0861 | 0.0011 | 1.0309 | 2.7651 | 4.1764 | 126,360 | 147 / 147 | 0 | 0 / 0 |
| change / background | 0.0820 | 0.0098 | 3.8310 | 0.5176 | 4.7209 | 54,384 | 75 / 75 | 1 | 36 / 846,720 |
| change / panel | 0.1015 | 0.0023 | 0.0675 | 2.2245 | 2.7510 | 6,960 | 3 / 3 | 1 | 1 / 1,295,920 |

Stable background mode retains 36 targets; panel mode retains one. Mixed live-score updates do not rebuild those stable targets, and every stable cache mode is required to show positive builds, hits, entries and retained bytes. The cost difference is consistent with a much larger number of cache sites, but these measurements do not isolate a causal implementation hotspot. No production cache optimization was added to manufacture a win.

Dirty panel content invalidates its previous observation before Prepare can capture it; it stays direct with no published stale target. Background mode can reuse 35 unchanged cells while the changing cell stays direct. Scrolling changes the observation context continuously and also stays direct in this fixture. Normal and normal-batching create no cache resources. Final scene/UI composition has zero target switches in every capture; capture switches occur only in Prepare. Hit/miss/eviction/disposal/allocation/build/switch/entry/pending/byte counters are retained for every sample. A lookup miss counter is not synonymous with every direct fallback.

## Cold capture and amortization

Cold samples retain the first eight UI frames after drawable warmup. The window ends at the first observed successful build, not at an assumed fixed frame. In this run stable capture first succeeds on UI frame 2, so three UI frames are included. For each repeat, the normal comparator is summed over that candidate's identical window; the table reports the median incremental cost. This includes initial direct painting, capture and startup/JIT/font effects. It is an observed cold estimate, not an isolated GPU allocation cost. Positive warm saving gives `ceil(max(0, cold incremental ms) / warm UI+Prepare saving)` only when the benefit gate passes and a cold capture actually occurred. A dash denotes no amortizing/established benefit.

| Fixture / configuration | Background first-build frames | Background incremental ms / amortization frames | Panel first-build frames | Panel incremental ms / amortization frames |
|---|---|---:|---|---:|
| cells / 1920×1080 s1 | 2,2,2 | 37.930 / — | 2,2,2 | 28.563 / 30 |
| layered / 1920×1080 s1 | 2,2,2 | 39.889 / — | 2,2,2 | 32.043 / 35 |
| mixed / 1920×1080 s1 | 2,2,2 | 40.994 / — | 2,2,2 | 31.597 / 35 |
| dirty / 1920×1080 s1 | 2,2,2 | 43.054 / — | none,none,none | 24.852 / — |
| scroll / 1920×1080 s1 | none,none,none | 22.398 / — | none,none,none | 22.519 / — |
| change / 1920×1080 s1 | 2,2,2 | 42.100 / — | 2,2,2 | 31.336 / 33 |
| cells / 1920×1080 s1.5 | 2,2,2 | 40.204 / — | 2,2,2 | 29.441 / 31 |
| layered / 1920×1080 s1.5 | 2,2,2 | 56.776 / — | 2,2,2 | 34.821 / — |
| mixed / 1920×1080 s1.5 | 2,2,2 | 47.712 / — | 2,2,2 | 33.316 / 31 |
| dirty / 1920×1080 s1.5 | 2,2,2 | 44.304 / — | none,none,none | 24.469 / — |
| scroll / 1920×1080 s1.5 | none,none,none | 17.718 / — | none,none,none | 20.073 / — |
| change / 1920×1080 s1.5 | 2,2,2 | 45.725 / — | 2,2,2 | 37.291 / 34 |
| cells / 1280×720 s1 | 2,2,2 | 36.046 / — | 2,2,2 | 24.889 / 29 |
| layered / 1280×720 s1 | 2,2,2 | 42.898 / — | 2,2,2 | 31.929 / 39 |
| mixed / 1280×720 s1 | 2,2,2 | 37.282 / — | 2,2,2 | 32.399 / 39 |
| dirty / 1280×720 s1 | 2,2,2 | 40.952 / — | none,none,none | 22.949 / — |
| scroll / 1280×720 s1 | none,none,none | 23.194 / — | none,none,none | 23.973 / — |
| change / 1280×720 s1 | 2,2,2 | 41.295 / — | 2,2,2 | 30.826 / 37 |

A no-build cold path is explicitly direct-only, not “direct plus capture.” Changed-child raw samples show the mutation frame draws the new content directly, followed by a new capture/hit after the next Prepare. Untimed resize/reparent probes establish the same transition. These correctness probes are separate from the warm comparison and T27's longer churn workload.

## Actual batching trace and correctness

`DrawTransaction.BeginDraw` already retains the active context; equal draw settings and equal clip targets are skipped. Actual traces show per-element clip changes, texture alternation and effect scopes. Layered drawing preserves solid → nine-slice → effect → text painter order, including alpha and effect configuration immediately before its existing callback site. Removing these meaningful boundaries would require stronger assumptions about overlap, clips or shared effect state. There is one empty startup begin/end sequence per frame; it submits no native draw, and no measured safe hotspot justified changing the existing drawing contract. The normal-batching mode therefore deliberately has identical code and counters.

| Trace fixture / mode | Begin / end | Clip changes | Effect settings changes | Effect configurations | Native UI draw calls | Native texture changes |
|---|---:|---:|---:|---:|---:|---:|
| batched / normal | 50 / 50 | 116 | 0 | 0 | 97 | 97 |
| layered / normal | 146 / 146 | 116 | 96 | 48 | 193 | 193 |
| compare-cells / normal | 40 / 40 | 92 | 0 | 0 | 75 | 75 |
| compare-cells / normal-batching | 40 / 40 | 92 | 0 | 0 | 75 | 75 |
| compare-cells / background | 40 / 40 | 92 | 0 | 0 | 75 | 75 |
| compare-cells / panel | 4 / 4 | 8 | 0 | 0 | 3 | 3 |
| compare-layered / normal | 112 / 112 | 92 | 72 | 36 | 147 | 147 |
| compare-layered / normal-batching | 112 / 112 | 92 | 72 | 36 | 147 | 147 |
| compare-layered / background | 40 / 40 | 92 | 0 | 0 | 75 | 75 |
| compare-layered / panel | 4 / 4 | 8 | 0 | 0 | 3 | 3 |

The trace is compile-time opt-in. The default-build test decodes IL to prove no DrawTransaction/effect-binding calls reference the trace class and that the trace class has no storage. The existing native allocation guard preserves the 120-byte transaction allocation. Trace builds are separately fingerprinted and their comparison screenshots exactly match the default build. No effect/shader/callback/painter-order batching rewrite was made.

Untimed validation covers 21 workload/configuration pairs and 1176 mode/frame checkpoints with exact RGBA, cell text/bounds, score/scroll state and matching world submissions. It includes cold direct frames 0/1, first hit 2, pre-change/change/recovery, continuously dirty/scrolling frames, resize and reparent. No pixel tolerance or alpha exception is used. Root independently checked first-UI-frame far-corner world pixels at all three drawable sizes. Performance runs reject both trace builds and parity-readback mode.

Validation scope: the integrated 567/567 suite passed before the final host-only drawable/parity/churn refinements. Current-source focused tests passed 11/11 (normal-path native safeguards, draw settings/effects, and default trace erasure); Python tests passed 15/15. The current host is additionally covered by the 1,176 exact transient checkpoints, 30 diagnostic traces and 216 warm captures. No production change followed the integrated pass. Final combined-route integration is owned by T29. [Integrated TRX](/tmp/mgui-t23-integrated-final.trx), [current focused TRX](/tmp/mgui-t23-final-focused.trx).

## Validation commands

```bash
flock --close /tmp/mgui-render-cache-dotnet.lock dotnet test MGUI.Tests/MGUI.Tests.csproj -c Release --nologo -v:q
python3 -m unittest discover -s tools/render-cache -p 'test_*.py'
flock --close /tmp/mgui-render-cache-dotnet.lock python3 tools/render-cache/parity_cache.py --output /tmp/mgui-t23-transient-parity
flock --close /tmp/mgui-render-cache-dotnet.lock python3 tools/render-cache/compare_cache.py --output /tmp/mgui-t23-matched-20261018 --skip-build
```

Build current Release source separately before `--skip-build`; the dotnet lock stays held through captures, while each helper owns its graphics lock/display. The separate trace host was built with `-p:DefineConstants=MGUI_RENDER_TRACE`; `/tmp/mgui-t23-trace-capture.py` records the 30 diagnostic captures. The initial parity analysis retry used `--resume`, preserving original raw images/source and rejecting native source/binary rewrites.
