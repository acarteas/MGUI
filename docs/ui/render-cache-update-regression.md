# Ordinary Update regression remediation (T32)

The final change replaces six `Components.Where(...).Select(...)` traversals with direct `List<MGComponentBase>` enumeration: two Update stages and four picking stages. Each loop reads the same priority predicate and `BaseElement` at the same point. Stage order, insertion order, child calls, independent reentrant enumerators and list-version failure behavior are preserved. The original child snapshots, geometry setters, visual revision propagation, cache invalidation and Draw code are unchanged.

This removes temporary LINQ iterators from every visited element, including empty component lists. Nested1080 Update allocations fall from exactly **1,350,520 to 627,000 bytes/frame**. The improvement offsets the reproduced ordinary Update regression with less normal work; it is not evidence that cache revision tracking has zero cost. No pooling, snapshot compatibility branches, revision specialization or runtime overrides are shipped.

## Validation and provenance

- Focused component traversal, revision and ownership contracts: **60/60 passed**, `/tmp/mgui-t32-components-focused.trx`.
- Full current `MGUI.Tests` suite, including real native lanes: **582/582 passed, zero skipped**, `/tmp/mgui-t32-components-full.trx` (4m55s).
- New component tests cover both Update priorities and insertion order, a fresh after-contents phase, mutation failure during either Update stage, independent reentrant Update, four picking priorities around content, and mutation failure in each picking stage. Pure managed probes establish traversal semantics; native suites establish input, pixels and graphics behavior.
- Frozen original T01 host/driver, seed1729, 120 warmup and 300 measured frames per run; three alternating repetitions per variant/cell. Original baseline assets are immutable. Each run matches baseline frame states, exact screenshot hash and every native world/UI/total counter; normal UI allocation floors do not increase.
- Native environment: .NET8.0.31, MonoGame DesktopGL3.8.4.1, private1920×1080 Xvfb, Mesa26.2.3 llvmpipe/LLVM23.1.1. These are CPU/wall phase measurements on software rendering, not hardware FPS or GPU time.
- All builds/tests/native timing use `/tmp/mgui-render-cache-dotnet.lock`; native helpers own the graphics lock/private display. Final source and default binary fingerprints remain invariant during capture.
- `/tmp/mgui-t32-source-delta.json` compares188 production source/project files with frozen T27: only `MGUI.Core/UI/MGElement.cs` differs. `/tmp/mgui-t32-preservation.json` records unchanged original user7 and T27-owned6 hashes. Main checkout is not modified by this review packet.

Final source fingerprint (code/assets, excluding this subsequently finalized report): `7602b91da0f7d82a678787af55b4feb1c648160fe57d1e6dbd467be67bee9fbc`.

| Final measured asset | SHA-256 |
| --- | --- |
| `MGUI.RenderCacheTestHost.dll` | `e20b5a978d9999619ebb829f062387009b104dec95b1c2c49632246b0354cf05` |
| `MGUI.Core.dll` | `0419ba8d2d12cc25d4373e3b33117fe7db5a76805f2b70fba9067d712301f561` |
| `MGUI.Shared.dll` | `8b53657517eaa31d2c48ddebf87438781513d9e1d8113ac50afd38f81dd53043` |
| `MGUI.FontStashSharp.dll` | `1bd8401d98874dc9e903b0446dddc649ea15b762e9b6212cfd953c56fc0b206c` |

The final cohort uses the exact three library DLLs exercised by the full suite. Its host is the byte-identical original host, rather than the newer test host. The first component cohort replaced Core only over T27's frozen dependency DLLs; Shared/Font source is identical, but their worktree-build hashes differ. Both captures retain their exact asset sets and are not relabeled.

## Source-bound ordinary comparison

Final18-run evidence: `/tmp/mgui-t32-components-final-native/summary.json`; source/default freeze: `/tmp/mgui-t32-final-native-freeze.json`; runner: `/tmp/mgui-t32-components-final-native.py`. The first12-run component cohort remains at `/tmp/mgui-t32-components-native/summary.json`. Raw compressed frames, PNGs, logs, commands, source manifests, binary hashes and load journals accompany each capture.

All times are milliseconds; entries are medians across three per-run median/p95 values. The instrumented subtotal is computed for each frame as Update+WorldSubmission+UiSubmission+Present before percentiles. The original host omits optional Prepare equally; subtotal excludes uninstrumented loop work and is not total frame latency or GPU execution time.

| Cell | Update median old→fixed | Update p95 old→fixed | UI median old→fixed | UI p95 old→fixed | Subtotal median old→fixed |
| --- | ---: | ---: | ---: | ---: | ---: |
| animated720 / s1 | 0.54820→0.55325 | 0.81700→0.76510 | 0.23740→0.25620 | 0.31790→0.35610 | 2.11905→2.11665 |
| nested1080 / s1 | 0.95865→0.73195 | 1.42770→1.12060 | 0.36585→0.38935 | 0.52860→0.58050 | 4.54015→4.34270 |
| nested1080 / s1.5 | 0.94130→0.73090 | 1.38670→1.06560 | 0.38890→0.39140 | 0.57050→0.56330 | 5.03430→4.70700 |

| Cell / variant | Update repeat medians | Update repeat p95s | Median spread |
| --- | --- | --- | ---: |
| animated720 / s1 / old | 0.56865 / 0.51790 / 0.54820 | 0.84000 / 0.77780 / 0.81700 | 0.05075 |
| animated720 / s1 / current | 0.54855 / 0.55325 / 0.56740 | 0.74440 / 0.91000 / 0.76510 | 0.01885 |
| nested1080 / s1 / old | 0.95865 / 0.97880 / 0.90855 | 1.42770 / 1.42970 / 1.35710 | 0.07025 |
| nested1080 / s1 / current | 0.73195 / 0.75895 / 0.71380 | 1.11160 / 1.12060 / 1.19380 | 0.04515 |
| nested1080 / s1.5 / old | 0.90680 / 0.97125 / 0.94130 | 1.36220 / 1.47530 / 1.38670 | 0.06445 |
| nested1080 / s1.5 / current | 0.73090 / 0.71115 / 0.74055 | 1.02950 / 1.06560 / 1.06930 | 0.02940 |

| Nested1080 cell | Paired Update median differences (fixed−old) | Final median gate | Final p95 gate |
| --- | --- | ---: | ---: |
| s1 | -0.22670 / -0.21985 / -0.19475 | 0.14050 | 0.14520 |
| s1.5 | -0.17590 / -0.26010 / -0.20075 | 0.12890 | 0.22620 |

All18 final median/p95 gates pass. Both nested cells improve in every pair in both component cohorts; this conclusion does not depend on accepting a wide positive-regression gate. The first cohort already measured Update deltas−0.13150/−0.17515ms at scales1/1.5, with all paired differences negative.

### UI and animated limits retained

The first component cohort failed scale1 UI median: +0.03380ms against0.02000ms. The final repeat still measures +0.02350ms against a wider0.03730ms gate; its UIp95 is +0.05190ms against0.05286ms. These results are retained, not evidence of zero UI overhead.

A bounded6-run scale1 control compares pre-T32 current Core with fixed Core, using the same original host and identical tested dependency libraries: `/tmp/mgui-t32-ui-control-native/summary.json`. Update improves1.00840→0.71845ms. UI median0.38460→0.38535ms has paired differences+0.00075/+0.02320/−0.00780ms; UIp95 increases0.05020ms within a noisy0.13240ms gate. This narrows median attribution but cannot exclude a small UI tail cost. Its subtotal median improves0.23745ms, and all exact-state/pixel/counter gates pass. The original-versus-current UI residual remains an explicit limit for downstream evidence.

Animated720 final Update median is+0.00505ms within a broad0.10150ms gate (pairs−0.02010/+0.03535/+0.01920ms). It remains inconclusive about absence of small cost and is not declared fixed.

## Retained attribution and failed prototypes

- T27 original reproduction and counted attribution: `/tmp/mgui-t27-causal-evidence-index.json`, `/tmp/mgui-t27-attribution-evidence-index.json`. Roughly251k direct geometry changes expand into1.69m visual-only invalidation executions over300 nested frames. The non-tiered setter probe explains only about28µs/frame, not the entire103–115µs native regression.
- A smaller visual-only helper improved a seven-node CPU probe but failed both native Update gates (`/tmp/mgui-t32-prototype-native`). JIT returns the stamp in registers; the hidden-return-buffer hypothesis was disproved. Helper changes were discarded.
- Diagnostic-only removal of geometry notifications still left a native regression (`/tmp/mgui-t32-ablation-native`). Disabling tiered compilation narrowed that difference while making both variants slower (`/tmp/mgui-t32-ablation-nontiered`). Neither diagnostic can be an accepted implementation or default-runtime timing proof.
- Native sampled stacks (`/tmp/mgui-t32-sample-native`) identify substantial child snapshot allocation plus component LINQ/cast work. Object probes show approximately192–200 added bytes per element, but do not establish a causal locality cost. JIT/profile artifacts are diagnostic, not acceptance timing.
- `ToArray` snapshots failed scale1.5 timing and changed Concat/Append/Prepend acquisition logs (`/tmp/mgui-t32-array-native`, `/tmp/mgui-t32-array-tests.trx`). A conservative pooled alternative passed its initial20 semantic tests but failed both native Update gates (`/tmp/mgui-t32-pool-native`). Both variants and their tests remain external diagnostics; neither is shipped.

The final scope is the six component loops and their focused semantic tests. Final T27 evidence must be rebuilt and recaptured against this source; older captures remain scoped to their original binaries.
