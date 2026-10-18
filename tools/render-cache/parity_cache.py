#!/usr/bin/env python3
"""Untimed transient exact-pixel checks. Never use these readback runs as timing evidence."""
import argparse
import hashlib
import json
from pathlib import Path
import benchmark
import compare_cache


def validate_parity(captures):
    compare_cache.validate_equivalence(captures)
    normal = captures["normal"]
    change = normal["warmupFrames"] + normal["measuredPresentedFrames"] // 2
    expected = sorted({0, 1, 2, 3, normal["warmupFrames"] - 1,
                       *range(change - 4, min(change + 4, normal["totalPresentedFrames"] - 1) + 1)})
    for mode, capture in captures.items():
        if not capture["parityCaptureEnabled"] or capture["drawableWarmupPresents"] != 3:
            raise ValueError("Untimed parity/readied drawable metadata missing")
        if [frame["frame"] for frame in capture["parityFrames"]] != expected:
            raise ValueError("Missing named cold/change/recovery parity checkpoint")
        for first, second in zip(normal["parityFrames"], capture["parityFrames"], strict=True):
            if any(first[key] != second[key] for key in ("frame", "state", "comparison", "screenshotSha256")):
                raise ValueError(f"Transient exact pixels/state/geometry differ: {mode}, frame {first['frame']}")
            path = Path(second["screenshot"])
            if hashlib.sha256(path.read_bytes()).hexdigest() != second["screenshotSha256"]:
                raise ValueError("Transient artifact hash mismatch")
    if normal["Scenario"] in ("compare-change", "compare-dirty"):
        if len({f["comparison"]["FirstCellText"] for f in normal["parityFrames"]}) < 2:
            raise ValueError("Affected cell never changed")
    if normal["Scenario"] == "compare-churn":
        if len({tuple(f["comparison"][key] for key in ("Left", "Top", "Width", "Height")) for f in normal["parityFrames"]}) < 3:
            raise ValueError("Resize/reparent did not alter affected cell geometry")
    if normal["Scenario"] == "compare-scroll" and len({f["state"]["ScrollOffset"] for f in normal["parityFrames"]}) < 2:
        raise ValueError("Scrolling parity checkpoints did not scroll")
    if normal["Scenario"] in ("compare-change", "compare-churn"):
        for mode in ("background", "panel"):
            initial = next((f["cache"] for f in captures[mode]["parityFrames"] if f["cache"]["Builds"] > 0 and f["cache"]["Hits"] > 0), None)
            if initial is None:
                raise ValueError("Cold cache never reached an established hit")
            final = captures[mode]["parityFrames"][-1]["cache"]
            if initial["Builds"] <= 0 or final["Builds"] <= initial["Builds"] or final["Hits"] <= initial["Hits"]:
                raise ValueError("Mutation did not establish, rebuild and recover cached drawing")
            by_frame = {f["frame"]: f for f in captures[mode]["parityFrames"]}
            for mutation in ([change] if normal["Scenario"] == "compare-change" else [change - 2, change]):
                before, direct, recovered = (by_frame[index]["cache"] for index in (mutation - 1, mutation, mutation + 1))
                if direct["Builds"] != before["Builds"] or recovered["Builds"] <= direct["Builds"] or recovered["Hits"] <= direct["Hits"]:
                    raise ValueError("Mutation must draw directly then rebuild/hit after next Prepare")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--resume", action="store_true", help="Reuse captures only when native source and binaries are unchanged; preserve initial provenance")
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=args.resume)
    source = benchmark.fingerprint()
    if args.resume:
        previous = json.loads((output / "source.json").read_text())
        native = lambda data: {name: value for name, value in data["files"].items() if not name.endswith(".py")}
        if previous["commit"] != source["commit"] or native(previous) != native(source):
            raise ValueError("Cannot resume parity after native source changes")
        (output / "validation-source.json").write_text(json.dumps(source, indent=2))
    else:
        (output / "source.json").write_text(json.dumps(source, indent=2))
    host = benchmark.ROOT / "MGUI.RenderCacheTestHost/bin/Release/net8.0/MGUI.RenderCacheTestHost.dll"
    binaries = {p.name: {"sha256": hashlib.sha256(p.read_bytes()).hexdigest(), "modifiedNs": p.stat().st_mtime_ns}
                for p in host.parent.glob("*.dll")}
    if args.resume:
        first_capture = min(p.stat().st_mtime_ns for p in output.glob("*-normal.json"))
        if any(data["modifiedNs"] > first_capture for data in binaries.values()):
            raise ValueError("Cannot resume after native binary writes")
    report = {"hostBinaries": binaries, "resumedCaptures": [], "scope": "UNTIMED exact RGBA cold/miss/hit/mutation/recovery/churn parity", "source": "source.json", "workloads": []}
    with benchmark.native_display() as env:
        env["MGUI_COMPARISON_PARITY"] = "1"
        report["glxinfo"] = benchmark.run(["glxinfo", "-B"], env=env)
        for width, height, scale in benchmark.CONFIGURATIONS:
            for scenario in (*compare_cache.SCENARIOS, "compare-churn"):
                captures = {}
                for mode in compare_cache.MODES:
                    name = f"{scenario}-{width}x{height}-s{scale}-{mode}"
                    raw, screenshot = output / (name + ".json"), output / (name + ".png")
                    command = ["dotnet", str(host), scenario, str(width), str(height), str(scale), "1729", "12", "8", str(raw), str(screenshot), mode]
                    if args.resume and raw.is_file():
                        report["resumedCaptures"].append(raw.name)
                    else:
                        (output / (name + ".log")).write_text(benchmark.run(command, cwd=host.parent, env=env))
                    capture = json.loads(raw.read_text())
                    benchmark.validate_capture(capture, scenario, width, height, scale, 1729, 12, 8, mode)
                    captures[mode] = capture
                    print(name, flush=True)
                validate_parity(captures)
                report["workloads"].append({"scenario": scenario, "width": width, "height": height, "scale": scale,
                    "exact": True, "frames": [f["frame"] for f in captures["normal"]["parityFrames"]]})
    after = benchmark.fingerprint()
    if any(source[key] != after[key] for key in ("commit", "sourceSha256", "fixtureSha256", "dirtySha256")):
        raise ValueError("Source changed during parity capture")
    (output / "summary.json").write_text(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
