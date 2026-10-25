#!/usr/bin/env python3
"""Four equivalent-pixel modes on explicitly named cache comparison fixtures."""
import argparse
import gzip
import hashlib
import json
import math
import platform
import statistics
import time
from pathlib import Path

import benchmark
from compare_normal import gate, summarize

SCENARIOS = ("compare-cells", "compare-layered", "compare-mixed", "compare-dirty", "compare-scroll", "compare-change")
MODES = ("normal", "normal-batching", "background", "panel")


def validate_equivalence(captures):
    normal = captures["normal"]
    for mode, capture in captures.items():
        if capture["screenshotSha256"] != normal["screenshotSha256"]:
            raise ValueError(f"Exact RGBA parity failed: {mode}")
        for first, second in zip(normal["frames"], capture["frames"], strict=True):
            if first["State"] != second["State"] or first.get("Comparison") != second.get("Comparison") or first["World"] != second["World"]:
                raise ValueError(f"State/world parity failed: {mode}")
            if second["Ui"]["TargetSwitches"]:
                raise ValueError("Final UI composition switched targets")
            if mode in ("normal", "normal-batching"):
                if first["Ui"] != second["Ui"] or any(second["Cache"].values()):
                    raise ValueError("Unchanged normal batching differs or allocates cache resources")
    if normal["Scenario"] in ("compare-cells", "compare-layered", "compare-mixed", "compare-change"):
        for mode in ("background", "panel"):
            cache = captures[mode]["frames"][0]["Cache"]
            if min(cache.get("Builds", 0), cache.get("Hits", 0), cache.get("Entries", 0), cache.get("RetainedBytes", 0)) <= 0:
                raise ValueError("Eligible stable cache never established positive targets/builds/hits")
    if normal["Scenario"] == "compare-mixed":
        for mode in ("background", "panel"):
            builds = [frame["Cache"]["Builds"] for frame in captures[mode]["frames"]]
            if len(set(builds)) != 1:
                raise ValueError("Live sibling rebuilt stable content")


def benefit(reference, candidate):
    med = gate(reference, candidate, "median")
    tail = gate(reference, candidate, "p95")
    saving = med["normal"] - med["disabled"]
    return {"referenceMedianMs": med["normal"], "candidateMedianMs": med["disabled"],
            "savingMs": saving, "gainFraction": saving / med["normal"],
            "referenceRepeatMedianSpreadMs": med["spread"], "p95": tail,
            "passesStableBenefitGate": saving >= .20 * med["normal"] and saving > 2 * med["spread"] and tail["passes"]}


def cold_window(capture):
    first_build = next((index for index, frame in enumerate(capture["coldFrames"])
                        if frame.get("Cache", {}).get("Builds", 0) > 0), None)
    return first_build + 1 if first_build is not None else 2, first_build


def cold_cost(capture):
    # Include every initial direct frame through the first successful capture.
    count, _ = cold_window(capture)
    return sum(frame[p]["CpuMs"] for frame in capture["coldFrames"][:count] for p in ("UiSubmission", "Prepare"))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--scenario", action="append", choices=SCENARIOS)
    parser.add_argument("--configuration", action="append", type=int, choices=range(3))
    parser.add_argument("--warmup", type=int, default=120)
    parser.add_argument("--frames", type=int, default=300)
    parser.add_argument("--repeats", type=int, default=3)
    parser.add_argument("--seed", type=int, default=1729)
    parser.add_argument("--skip-build", action="store_true")
    args = parser.parse_args()
    if args.warmup < 2 or args.frames < 3 or args.repeats < 1:
        parser.error("Need at least two warmup, three measured frames and one repeat")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    commands = []
    if not args.skip_build:
        command = ["flock", "--close", "/tmp/mgui-render-cache-dotnet.lock", "dotnet", "build", "MGUI.RenderCacheTestHost/MGUI.RenderCacheTestHost.csproj", "-c", "Release", "--nologo", "-v:q"]
        commands.append(command)
        (output / "build.log").write_text(benchmark.run(command, timeout=300))
    source = benchmark.fingerprint()
    (output / "source.json").write_text(json.dumps(source, indent=2))
    host = benchmark.ROOT / "MGUI.RenderCacheTestHost/bin/Release/net8.0/MGUI.RenderCacheTestHost.dll"
    binaries = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in host.parent.glob("*.dll")}
    report = {"schemaVersion": 1, "mode": "four-mode-cache-comparison", "configuration": "Release",
              "startedUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()), "source": "source.json",
              "commit": source["commit"], "sourceSha256": source["sourceSha256"], "fixtureSha256": source["fixtureSha256"],
              "binaries": binaries, "seed": args.seed, "warmupFrames": args.warmup, "measuredFrames": args.frames,
              "repeats": args.repeats, "commands": commands, "workloads": [],
              "environment": {"os": platform.platform()},
              "batchingDecision": "No batching change justified; normal-batching executes the identical normal path. Diagnostic traces are captured separately, never timed here.",
              "metricScope": "Game-thread submission/update/Prepare CPU and Present wall time, not GPU execution or hardware FPS. Subtotal excludes host-loop work. Cold through first successful build includes JIT/font startup noise; report each repeat."}
    with benchmark.native_display() as env:
        report["environment"].update(display=env["DISPLAY"], glxinfo=benchmark.run(["glxinfo", "-B"], env=env), dotnet=benchmark.run(["dotnet", "--info"]))
        for configuration in args.configuration if args.configuration is not None else range(3):
            width, height, scale = benchmark.CONFIGURATIONS[configuration]
            for scenario in args.scenario or SCENARIOS:
                runs = {mode: [] for mode in MODES}
                for repeat in range(1, args.repeats + 1):
                    captures = {}
                    # Alternate forward/reverse and rotate starting mode to avoid a fixed thermal order.
                    order = MODES[repeat - 1:] + MODES[:repeat - 1]
                    if repeat % 2 == 0:
                        order = tuple(reversed(order))
                    for mode in order:
                        name = f"{scenario}-{width}x{height}-s{scale}-r{repeat}-{mode}"
                        raw, screenshot = output / (name + ".json"), output / (name + ".png")
                        command = ["dotnet", str(host), scenario, str(width), str(height), str(scale), str(args.seed), str(args.warmup), str(args.frames), str(raw), str(screenshot), mode]
                        commands.append(command)
                        (output / (name + ".log")).write_text(benchmark.run(command, cwd=host.parent, env=env))
                        capture = json.loads(raw.read_text())
                        benchmark.validate_capture(capture, scenario, width, height, scale, args.seed, args.warmup, args.frames, mode)
                        if capture["stateTrace"] is not None or capture["parityCaptureEnabled"]:
                            raise ValueError("Trace build cannot supply performance evidence")
                        if hashlib.sha256(screenshot.read_bytes()).hexdigest() != capture["screenshotSha256"]:
                            raise ValueError("Screenshot hash differs")
                        capture["provenance"] = {key: source[key] for key in ("commit", "sourceSha256", "fixtureSha256", "dirtySha256")}
                        with gzip.open(str(raw) + ".gz", "wt") as stream:
                            json.dump(capture, stream, separators=(",", ":"))
                        raw.unlink()
                        captures[mode] = capture
                        cold = capture["coldFrames"]
                        cold_count, first_build = cold_window(capture)
                        if mode in ("background", "panel") and scenario in ("compare-cells", "compare-layered", "compare-mixed", "compare-change") and first_build is None:
                            raise ValueError("Eligible stable capture did not build within retained cold samples")
                        frames = capture["frames"]
                        cache_delta = {key: frames[-1]["Cache"][key] - frames[0]["Cache"][key] for key in frames[0]["Cache"]}
                        runs[mode].append({"raw": name + ".json.gz", "screenshot": name + ".png",
                            "stateSha256": benchmark.digest([f["State"] for f in frames]), "summary": summarize(frames),
                            "coldInitialUiMs": cold_cost(capture),
                            "coldCaptureOccurred": first_build is not None,
                            "coldFrameCount": cold_count, "coldFirstBuildFrame": first_build,
                            "coldUiFrameCostsMs": [sum(f[p]["CpuMs"] for p in ("UiSubmission", "Prepare")) for f in cold],
                            "coldScope": "Initial direct frames through first successful build; first two direct-only frames when no capture occurred", "cacheFirst": frames[0]["Cache"],
                            "cacheLast": frames[-1]["Cache"], "cacheDeltaLastMinusFirst": cache_delta,
                            "textureChanges": benchmark.distribution([f["TextureChanges"] for f in frames])})
                        print(name, flush=True)
                    validate_equivalence(captures)
                reference_mode = min(("normal", "normal-batching"), key=lambda mode: statistics.median(run["summary"]["UiPlusPrepare.CpuMs"]["median"] for run in runs[mode]))
                comparisons = {}
                for mode in ("background", "panel"):
                    result = benefit([run["summary"]["UiPlusPrepare.CpuMs"] for run in runs[reference_mode]], [run["summary"]["UiPlusPrepare.CpuMs"] for run in runs[mode]])
                    cold_deltas = [candidate["coldInitialUiMs"] - sum(reference["coldUiFrameCostsMs"][:candidate["coldFrameCount"]])
                                   for candidate, reference in zip(runs[mode], runs[reference_mode], strict=True)]
                    cold = statistics.median(cold_deltas)
                    result.update(coldIncrementalMs=cold, coldIncrementalMsPerRun=cold_deltas, amortizationFrames=math.ceil(max(0, cold) / result["savingMs"]) if result["savingMs"] > 0 and result["passesStableBenefitGate"] and all(run["coldCaptureOccurred"] for run in runs[mode]) else None,
                                  amortizationScope="Initial frames through first build with JIT/font startup; median of per-repeat incremental cost versus normal over the identical frame count; null when no cold capture occurred or benefit is non-amortizing/inconclusive")
                    comparisons[mode] = result
                report["workloads"].append({"scenario": scenario, "width": width, "height": height, "uiScale": scale,
                    "runs": runs, "referenceMode": reference_mode, "comparisons": comparisons, "exactPixelsAndState": True})
                (output / "summary.partial.json").write_text(json.dumps(report, indent=2))
    after = benchmark.fingerprint()
    if any(source[key] != after[key] for key in ("commit", "sourceSha256", "fixtureSha256", "dirtySha256")):
        raise RuntimeError("Source changed during capture")
    if binaries != {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in host.parent.glob("*.dll")}:
        raise RuntimeError("Binaries changed during capture")
    report["completedUtc"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    (output / "summary.json").write_text(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
