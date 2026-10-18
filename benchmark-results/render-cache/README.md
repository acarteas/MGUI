# Native rendering baseline

The isolated `MGUI.RenderCacheTestHost` measures normal MGUI rendering. It does not enable caching or change `MGUI.EffectTestHost` and its `RESULT:` protocol. The retained `baseline-2026-10-17` directory contains the initial normal-rendering comparison workload. Keep the fixture fingerprint and seed identical when comparing a later renderer, and record its changed source fingerprint.

Run from the repository root on Linux with .NET 8, Xvfb, and `glxinfo` installed:

```sh
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tools/render-cache -v
python3 tools/render-cache/benchmark.py --output benchmark-results/render-cache/baseline-2026-10-17
```

Every output directory must be new. For subsequent captures choose another directory, for example `/tmp/mgui-render-comparison`. The default command builds current source in Release using `flock --close /tmp/mgui-render-cache-dotnet.lock`, takes `/tmp/mgui-render-cache-graphics.lock`, starts its own Xvfb using an automatically allocated display, and cleans it up on success/failure. It never uses the user's display. Each native process has a 180-second timeout and its own process group for cleanup. `--skip-build` is only for an already verified current-source Release build. Optional `--scenario`, `--seed`, `--warmup`, `--frames` and `--repeats` allow diagnostic captures; default values define the retained comparison workload.

The six procedural, test-owned fixtures construct real MGUI controls and brushes:

| Scenario | Workload |
| --- | --- |
| `batched` | 48 translucent rectangle/text cells in eight rows, no custom effects |
| `layered` | The same 48 cells, each with layered solid, generated nine-slice texture and custom effect brushes |
| `hud` | Static two-row HUD with scores, health, energy, ammunition and status |
| `animated-score` | The same panel; only its score text changes each simulation frame |
| `scrolling` | 60 stable text rows inside an independently scrolling viewport |
| `nested-clips` | 60 clipped panels containing overflowing horizontal content, nested inside a scrolling clipped viewport |

All fixtures render over the same moving checkerboard world and translucent window. Assets are generated texture data, the existing MGUI test effect and the sample NotoSans font; no game content is used. Fixed seed `1729` sets grid colors. The simulation clock is `(presented frame + 1) / 60` seconds, independent of wall time, and records frame/score/scroll/world state. Raw state hashes must match between repeats. The UI does its regular update, layout, input observation and painting each frame. Default coverage is all six fixtures at 1920×1080 scales 1 and 1.5 and 1280×720 scale 1: 18 workloads, each repeated three times, each with 120 warmup and 300 measured presented frames.

`summary.json` records configuration, exact commands/working directories, CPU/OS/runtime, OpenGL vendor/renderer/version from the owned display, fingerprints, per-repeat and pooled summaries. `source.json` contains MGUI commit, dirty paths/status, source/asset SHA-256 manifest, fixture SHA-256 and dirty-file SHA-256 manifest. Fingerprints exclude generated results, binaries and Beads database state; dirty unrelated edits remain identified and hashed. The driver rejects source/dirty-content drift during capture. Each compressed raw `.json.gz` is lossless JSON with all 300 frame records and shared provenance. Read it with `gzip -dc <file.json.gz>` or Python's `gzip` module. The runner writes a PNG and stdout log for every run. The retained baseline commit includes the PNGs and five named validation/progress logs; empty per-run stdout logs are omitted. The baseline commit plus the checked-in host/driver source fingerprints identify the implementation even when the originating worktree was dirty.

Per-frame measurements separate:

- `UpdateLayout`: fixture state changes, MGUI update/input observation and layout, including any text/layout invalidation.
- `WorldSubmission`: clear and checkerboard SpriteBatch submission; its graphics counters are separate from UI counters.
- `UiSubmission`: `MGDesktop.Draw`, including batched work flushed by the draw transaction.
- `Present`: CPU time/allocations around `base.EndDraw()`, which actually presents the backbuffer. This may include driver blocking.
- `World`, `Ui`, `Total`: actual `GraphicsDevice.Metrics` draw calls, primitives, sprites and target switches, sampled before `Present` resets them.

CPU timings use `Stopwatch`; allocated bytes use `GC.GetAllocatedBytesForCurrentThread` and cover only the game thread within each phase. Instrumentation storage is preallocated. Harness serialization and screenshot work are outside measured frames. GPU time is explicitly `null` with a reason: MonoGame DesktopGL exposes no portable timer-query API. CPU submission or Present blocking must not be interpreted as GPU execution time. Vsync is disabled and the timestep is uncapped; neither frame rate nor a vsync interval is used as the benchmark metric.

A frame counts only after `base.EndDraw` returns. An extra unmeasured frame after the 120+300 frames reads back the native backbuffer, checks exact checkerboard pixels outside the UI, detects visible UI and bright text pixels, and writes the PNG. It is also presented, giving 421 actual presented frames per run. PNG dimensions and hashes are checked by the driver. Screenshots provide reviewable composition/clipping evidence, not a portable pixel golden assertion.

Each metric reports median and nearest-rank p95 (`ceil(n * .95)`), plus min/max/count. `pooledSummary` covers 900 measured samples per workload; `repeatMedianSummary` summarizes the three run medians. Raw records allow other analyses without rerunning. There are no golden wall-time assertions. Software-renderer results characterize this environment only; do not infer hardware FPS.

Validate the existing native effect protocol and all MGUI tests with serialized build/test execution:

```sh
flock --close /tmp/mgui-render-cache-dotnet.lock dotnet test MGUI.Tests/MGUI.Tests.csproj -c Release --nologo -v:q
```

Each native test host acquires the graphics lock and owns its private Xvfb display through the shared child runner. Do not wrap this command in `benchmark.native_display()` or an outer graphics lock: that would nest the same lock and deadlock. See [resource service validation](../../docs/ui/render-cache-resource-tests.md) for the ownership protocol and standalone native commands.

The additive normal/disabled comparison is documented in [normal rendering safeguards](../../docs/ui/render-cache-normal-tests.md). Run `python3 tools/render-cache/compare_normal.py --output <new-directory>` for alternating matched variants, separate Prepare accounting, strict pixel/state/native-counter equality and the design contract timing gates. The historical 54 runs remain immutable.
