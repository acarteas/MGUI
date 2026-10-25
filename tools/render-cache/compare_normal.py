#!/usr/bin/env python3
"""Matched normal/explicitly-disabled native rendering comparison; immutable baseline is never rewritten."""
import argparse
import gzip
import hashlib
import json
import platform
import statistics
import time
from pathlib import Path

import benchmark

PHASES = (*benchmark.PHASES, "Prepare")


def summarize(frames):
    result = benchmark.summarize(frames)
    for phase in ("Prepare", "UiPlusPrepare", "InstrumentedSubtotal"):
        members = ("Prepare",) if phase == "Prepare" else (("UiSubmission", "Prepare") if phase == "UiPlusPrepare" else PHASES)
        for metric in ("CpuMs", "AllocatedBytes"):
            result[f"{phase}.{metric}"] = benchmark.distribution([sum(frame[p][metric] for p in members) for frame in frames])
    return result


def gate(normal, disabled, percentile):
    values = [run[percentile] for run in normal]
    reference = statistics.median(values)
    candidate = statistics.median(run[percentile] for run in disabled)
    spread = max(values) - min(values)
    threshold = max(.02 if percentile == "median" else .05,
                    (.05 if percentile == "median" else .10) * reference, 2 * spread)
    return {"normal": reference, "disabled": candidate, "spread": spread, "threshold": threshold,
            "regression": candidate - reference, "passes": candidate - reference <= threshold}


def validate_pair(normal, disabled):
    if normal["screenshotSha256"] != disabled["screenshotSha256"]:
        raise ValueError("Normal/disabled native screenshots differ")
    for first, second in zip(normal["frames"], disabled["frames"], strict=True):
        if first["State"] != second["State"] or any(first[part] != second[part] for part in ("World", "Ui", "Total")):
            raise ValueError("Normal/disabled state or native graphics counters differ")
        for frame in (first, second):
            if any(frame["Cache"].values()) or frame["Ui"]["TargetSwitches"] or frame["Prepare"]["AllocatedBytes"]:
                raise ValueError("Unused cache performed resource work or steady Prepare allocation")


def validate_baseline(capture, baseline):
    if min(frame["UiSubmission"]["AllocatedBytes"] for frame in capture["frames"]) > min(frame["UiSubmission"]["AllocatedBytes"] for frame in baseline["frames"]):
        raise ValueError("Current normal steady UI allocation exceeds retained baseline")
    if capture["screenshotSha256"] != baseline["screenshotSha256"]:
        raise ValueError("Current normal output differs from retained baseline")
    for current, previous in zip(capture["frames"], baseline["frames"], strict=True):
        if current["State"] != previous["State"] or any(current[part] != previous[part] for part in ("World", "Ui", "Total")):
            raise ValueError("Current normal state or native batching differs from retained baseline")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--scenario", action="append", choices=benchmark.SCENARIOS)
    parser.add_argument("--configuration", action="append", type=int, choices=range(3), help="Index in baseline configuration table; default all")
    parser.add_argument("--warmup", type=int, default=120)
    parser.add_argument("--frames", type=int, default=300)
    parser.add_argument("--repeats", type=int, default=3)
    parser.add_argument("--seed", type=int, default=1729)
    parser.add_argument("--skip-build", action="store_true")
    parser.add_argument("--baseline", type=Path, default=benchmark.ROOT / "benchmark-results/render-cache/baseline-2026-10-17")
    args = parser.parse_args()
    if args.warmup < 0 or args.frames < 1 or args.repeats < 1:
        parser.error("Invalid frame/repeat counts")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    baseline_summary = json.loads((args.baseline / "summary.json").read_text())
    commands = []
    if not args.skip_build:
        build = ["flock", "--close", "/tmp/mgui-render-cache-dotnet.lock", "dotnet", "build", "MGUI.RenderCacheTestHost/MGUI.RenderCacheTestHost.csproj", "-c", "Release", "--nologo", "-v:q"]
        commands.append(build)
        (output / "build.log").write_text(benchmark.run(build, timeout=300))
    source = benchmark.fingerprint()
    (output / "source.json").write_text(json.dumps(source, indent=2))
    host = benchmark.ROOT / "MGUI.RenderCacheTestHost/bin/Release/net8.0/MGUI.RenderCacheTestHost.dll"
    report = {"schemaVersion": 1, "mode": "matched-normal-disabled", "configuration": "Release",
              "startedUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()), "source": "source.json",
              "commit": source["commit"], "sourceSha256": source["sourceSha256"], "fixtureSha256": source["fixtureSha256"],
              "seed": args.seed, "warmupFrames": args.warmup, "measuredFrames": args.frames, "repeats": args.repeats,
              "command": ["python3", "tools/render-cache/compare_normal.py", *benchmark.os.sys.argv[1:]],
              "commands": commands, "workloads": [], "environment": {"os": platform.platform(),
                  "cpu": next((line.split(":", 1)[1].strip() for line in Path("/proc/cpuinfo").read_text().splitlines() if line.startswith("model name")), "unknown")},
              "metricScope": "Phase-local game-thread managed allocation and CPU/wall times; instrumented subtotal excludes host loop; no GPU timing or hardware FPS claim"}
    with benchmark.native_display() as env:
        report["environment"].update(display=env["DISPLAY"], glxinfo=benchmark.run(["glxinfo", "-B"], env=env), dotnet=benchmark.run(["dotnet", "--info"]))
        for configuration in args.configuration if args.configuration is not None else range(3):
            width, height, scale = benchmark.CONFIGURATIONS[configuration]
            for scenario in args.scenario or benchmark.SCENARIOS:
                runs = {"normal": [], "disabled": []}
                state_hash = None
                historical = None
                historical_runs = None
                if (args.seed, args.warmup, args.frames) == (1729, 120, 300):
                    with gzip.open(args.baseline / f"{scenario}-{width}x{height}-s{scale}-r1.json.gz", "rt") as stream:
                        historical = json.load(stream)
                    historical_runs = next(workload["runs"] for workload in baseline_summary["workloads"] if (workload["scenario"], workload["width"], workload["height"], workload["uiScale"]) == (scenario, width, height, scale))
                for repeat in range(1, args.repeats + 1):
                    captures = {}
                    for mode in (("normal", "disabled") if repeat % 2 else ("disabled", "normal")):
                        name = f"{scenario}-{width}x{height}-s{scale}-r{repeat}-{mode}"
                        raw, screenshot = output / (name + ".json"), output / (name + ".png")
                        command = ["dotnet", str(host), scenario, str(width), str(height), str(scale), str(args.seed), str(args.warmup), str(args.frames), str(raw), str(screenshot), mode]
                        commands.append(command)
                        (output / (name + ".log")).write_text(benchmark.run(command, cwd=host.parent, env=env))
                        capture = json.loads(raw.read_text())
                        current = benchmark.validate_capture(capture, scenario, width, height, scale, args.seed, args.warmup, args.frames, mode)
                        if state_hash is not None and state_hash != current:
                            raise ValueError("Matched state hash diverged")
                        state_hash = current
                        if hashlib.sha256(screenshot.read_bytes()).hexdigest() != capture["screenshotSha256"]:
                            raise ValueError("Screenshot hash differs")
                        capture["provenance"] = {key: source[key] for key in ("commit", "sourceSha256", "fixtureSha256", "dirtySha256")}
                        with gzip.open(str(raw) + ".gz", "wt") as stream:
                            json.dump(capture, stream, separators=(",", ":"))
                        raw.unlink()
                        captures[mode] = capture
                        runs[mode].append({"raw": name + ".json.gz", "screenshot": name + ".png", "summary": summarize(capture["frames"])})
                        print(name, flush=True)
                    validate_pair(captures["normal"], captures["disabled"])
                    if historical is not None:
                        validate_baseline(captures["normal"], historical)
                gates = {f"{phase}.{percentile}": gate([run["summary"][phase + ".CpuMs"] for run in runs["normal"]], [run["summary"][phase + ".CpuMs"] for run in runs["disabled"]], percentile)
                         for phase in ("UpdateLayout", "UiPlusPrepare") for percentile in ("median", "p95")}
                historical_gates = ({f"{current_phase}.{percentile}": gate([run["summary"][historical_phase + ".CpuMs"] for run in historical_runs],
                    [run["summary"][current_phase + ".CpuMs"] for run in runs["normal"]], percentile)
                    for historical_phase, current_phase in (("UpdateLayout", "UpdateLayout"), ("UiSubmission", "UiPlusPrepare"))
                    for percentile in ("median", "p95")} if historical_runs is not None else None)
                report["workloads"].append({"scenario": scenario, "width": width, "height": height, "uiScale": scale, "stateSha256": state_hash,
                                            "runs": runs, "gates": gates, "exactPixelsStateCounters": True,
                                            "retainedBaselineExact": historical is not None, "historicalTimingGates": historical_gates,
                                            "historicalTimingScope": "Same recorded backend/hardware workload, different source and capture date; diagnostic regression signal, not a matched current-source overhead proof",
                                            "historicalAllocationDeltaBytes": ({phase: statistics.median(run["summary"][phase + ".AllocatedBytes"]["median"] for run in runs["normal"]) - benchmark.summarize(historical["frames"])[phase + ".AllocatedBytes"]["median"] for phase in benchmark.PHASES} if historical is not None else None)})
    after = benchmark.fingerprint()
    if any(source[key] != after[key] for key in ("commit", "sourceSha256", "fixtureSha256", "dirtySha256")):
        raise RuntimeError("Source changed during capture; discard and rerun")
    report["allTimingGatesPass"] = all(gate["passes"] for workload in report["workloads"] for gate in workload["gates"].values())
    report["completedUtc"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    (output / "summary.json").write_text(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
