#!/usr/bin/env python3
"""Matched mixed-state captures and a native10000-presented-frame resource soak.

Run only after an explicit current-source Release build. Timing results from
verification/readback runs are never included in the performance summaries.
"""
import argparse
import gzip
import hashlib
import json
import math
from pathlib import Path
import platform
import statistics
import struct
import time

import benchmark
import compare_normal

SCENARIOS = ("idle-hud", "hover-press", "pulse", "text", "scrolling", "resize-scale", "continuous-animation")
MODES = ("normal", "background", "panel")
PHASES = (*benchmark.PHASES, "Prepare")


def binary_fingerprint(host):
    required = {host.name, "MGUI.Core.dll", "MGUI.Shared.dll", "MGUI.FontStashSharp.dll",
                "FontStashSharp.MonoGame.dll", "FontStashSharp.Base.dll", "MonoGame.Framework.dll"}
    files = {path.name: hashlib.sha256(path.read_bytes()).hexdigest() for path in host.parent.glob("*.dll")}
    if not required.issubset(files):
        raise ValueError("Explicitly build the complete current-source default Release host first")
    for path in host.parent.rglob("*"):
        if path.is_file() and (".so" in path.name or path.suffix in (".dylib", ".ttf", ".xnb")
                               or path.name.endswith((".runtimeconfig.json", ".deps.json"))):
            files[str(path.relative_to(host.parent))] = hashlib.sha256(path.read_bytes()).hexdigest()
    return files


class CohortStore:
    """Only complete, validated matched cohorts may be reused after interruption."""
    def __init__(self, output, identity, resume=False):
        self.output = output
        self.path = output / "cohorts.json"
        if resume:
            self.state = json.loads(self.path.read_text())
            if self.state["identity"] != identity:
                raise ValueError("Resume source/default binaries/configuration/provenance changed")
            for attempt in self.state["attempts"]:
                if attempt["status"] == "running":
                    attempt["status"] = "interrupted"
                    attempt["reason"] = "No complete cohort checkpoint; retained and excluded from accepted data"
            self.save()
        else:
            output.mkdir(parents=True, exist_ok=False)
            self.state = {"identity": identity, "startedUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                          "completed": {}, "attempts": []}
            self.save()

    def save(self):
        temporary = self.path.with_suffix(".json.tmp")
        temporary.write_text(json.dumps(self.state, indent=2))
        temporary.replace(self.path)

    def get(self, key):
        record = self.state["completed"].get(key)
        if record is None:
            return None
        for name, expected in record["artifacts"].items():
            path = Path(name)
            if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != expected:
                raise ValueError("Completed cohort artifact changed: " + name)
        return record["payload"]

    def begin(self, key):
        if key in self.state["completed"]:
            raise ValueError("Complete cohorts must be reused, never overwritten")
        parent = self.output / "cohorts" / key
        parent.mkdir(parents=True, exist_ok=True)
        number = 1
        while (parent / f"attempt-{number:03}").exists():
            number += 1
        path = parent / f"attempt-{number:03}"
        path.mkdir()
        self.state["attempts"].append({"key": key, "path": str(path), "status": "running"})
        self.save()
        return path

    def complete(self, key, payload):
        artifacts = {}

        def inspect(value):
            if isinstance(value, dict):
                for name, item in value.items():
                    if name in ("raw", "screenshot", "captureLog") and isinstance(item, str):
                        path = Path(item)
                        path = path if path.is_absolute() else self.output / path
                        artifacts[str(path)] = hashlib.sha256(path.read_bytes()).hexdigest()
                    else:
                        inspect(item)
            elif isinstance(value, list):
                for item in value:
                    inspect(item)
        inspect(payload)
        attempt = next(value for value in reversed(self.state["attempts"]) if value["key"] == key and value["status"] == "running")
        attempt["status"] = "complete"
        self.state["completed"][key] = {"payload": payload, "artifacts": artifacts}
        self.save()

    def fail(self, key, error):
        for attempt in reversed(self.state["attempts"]):
            if attempt["key"] == key and attempt["status"] == "running":
                attempt.update(status="failed", reason=str(error))
                self.save()
                return

    def check_renderer(self, text):
        identity = [line.strip() for line in text.splitlines() if any(name in line for name in (
            "OpenGL renderer string:", "OpenGL version string:", "OpenGL vendor string:"))]
        if len(identity) != 3:
            raise ValueError("Incomplete native renderer identity")
        if self.state.get("renderer") is not None and self.state["renderer"] != identity:
            raise ValueError("Renderer changed between matched/resumed cohorts")
        self.state["renderer"] = identity
        self.save()


def assert_frozen(source, binaries, host):
    if benchmark.fingerprint() != source or binary_fingerprint(host) != binaries:
        raise ValueError("Source or default binary/runtime assets changed during the capture")


def normal_cohorts(output, source, binaries, host, store):
    records = []
    for scenario in benchmark.SCENARIOS:
        key = "normal-" + scenario
        record = store.get(key)
        if record is None:
            attempt = store.begin(key)
            directory = attempt / "capture"
            command = ["python3", str(benchmark.ROOT / "tools/render-cache/benchmark.py"), "--skip-build",
                       "--scenario", scenario, "--output", str(directory)]
            log = attempt / "normal-driver.log"
            try:
                log.write_text(benchmark.run(command, timeout=1800))
                assert_frozen(source, binaries, host)
                normal_path = directory / "summary.json"
                normal = json.loads(normal_path.read_text())
                store.check_renderer(normal["environment"]["glxinfo"])
                normal["binaries"] = binaries
                normal["binaryProvenanceScope"] = "Stability driver fingerprints source/default DLL/runtime assets before and after each complete nine-run normal scenario cohort"
                for workload in normal["workloads"]:
                    for run in workload["runs"]:
                        raw = directory / run["raw"]
                        with gzip.open(raw, "rt") as stream:
                            value = json.load(stream)
                        value.setdefault("provenance", {})["binaries"] = binaries
                        value["provenance"]["binaryProvenanceScope"] = normal["binaryProvenanceScope"]
                        with gzip.open(raw, "wt") as stream:
                            json.dump(value, stream, separators=(",", ":"))
                        run["raw"] = str(raw)
                        run["screenshot"] = str(directory / run["screenshot"])
                normal_path.write_text(json.dumps(normal, indent=2))
                record = {"report": normal, "captureLog": str(log), "command": command}
                store.complete(key, record)
            except BaseException as error:
                if not log.exists():
                    log.write_text(str(error))
                store.fail(key, error)
                raise
        records.append(record)
    assert_frozen(source, binaries, host)
    merged = {**records[0]["report"], "workloads": [workload for record in records for workload in record["report"]["workloads"]],
              "commands": [command for record in records for command in record["report"]["commands"]],
              "startedUtc": store.state["startedUtc"], "completedUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())}
    capture = output / "capture"
    capture.mkdir(exist_ok=True)
    (capture / "summary.json").write_text(json.dumps(merged, indent=2))
    (output / "summary.json").write_text(json.dumps({"mode": "normal-only-with-binaries", "sourceSha256": source["sourceSha256"],
        "binaries": binaries, "normalReport": "capture/summary.json", "cohortCheckpoint": "cohorts.json",
        "cohortCheckpointSha256": hashlib.sha256(store.path.read_bytes()).hexdigest()}, indent=2))


def validate_capture(capture, request):
    if capture["schemaVersion"] != 1 or capture["options"] != request or capture["configuration"] != "Release":
        raise ValueError("Stability request/provenance differs from native capture")
    frames = capture["frames"]
    if (capture["measuredPresentedFrames"] != request["Frames"] or len(frames) != request["Frames"]
            or capture["drawableWarmupFrames"] != 3
            or capture["totalPresentedFrames"] != 3 + request["Warmup"] + request["Frames"] + 1
            or not capture["actualDrawableVerified"]):
        raise ValueError("Incomplete actual-presented frames or unverifiable drawable")
    if capture["gpuTimeMs"] is not None or not capture["gpuTimeUnavailableReason"]:
        raise ValueError("GPU timing availability was not reported honestly")
    teardown = capture["resourceChecks"]["Teardown"]
    if (not teardown["IsDisposed"] or any(teardown[key] for key in ("RetainedBytes", "Entries", "Pending"))
            or teardown["Allocations"] != teardown["Disposals"]):
        raise ValueError("Resource teardown did not reach zero")
    checks = capture["resourceChecks"]
    if request["Mode"] != "normal" and (checks["warmStatistics"]["Builds"] <= 0
                                          or checks["warmStatistics"]["Hits"] <= 0
                                          or checks["warmBytes"] <= 0 or checks["warmEntries"] <= 0):
        raise ValueError("Eligible cache never established positive warm builds, hits, entries and bytes")
    if request["Scenario"] in ("idle-hud", "pulse", "hover-press", "scrolling") and checks["IdleChecks"] < len(frames):
        raise ValueError("Warm static checks excluded measured frames")
    for index, frame in enumerate(frames, request["Warmup"]):
        if frame["Frame"] != index or frame["State"]["Frame"] != index:
            raise ValueError("Frame continuity diverged")
        if frame["State"]["ClockTicks"] != (index + 1) * 10_000_000 // 60:
            raise ValueError("Simulation clock diverged")
        for phase in PHASES:
            if any(not isinstance(value, (int, float)) or not math.isfinite(value) or value < 0
                   for value in frame[phase].values()):
                raise ValueError("Invalid phase-local CPU/allocation sample")
        cache, counters = frame["Cache"], frame["Cache"]["Statistics"]
        if not 0 <= cache["RetainedBytes"] <= cache["ByteLimit"] or not 0 <= cache["Entries"] <= cache["EntryLimit"]:
            raise ValueError("Charged resource budgets exceeded")
        if counters["Allocations"] - counters["Disposals"] != cache["Entries"]:
            raise ValueError("Allocated minus disposed targets differ from charged entries")
        if frame["Ui"]["TargetSwitches"]:
            raise ValueError("Final UI composition switched render targets")
        for key in benchmark.COUNTERS:
            if frame["World"][key] + frame["Ui"][key] != frame["Total"][key]:
                raise ValueError("World/UI graphics counter attribution diverged")
        if request["Mode"] == "normal" and (cache["Entries"] or cache["Pending"] or counters["Allocations"]):
            raise ValueError("Ordinary rendering performed opt-in resource work")
    if request["VerifyPixels"] and (len(capture["pixelHashes"]) != len(frames)
                                    or any(not value or len(value) != 64 for value in capture["pixelHashes"])):
        raise ValueError("Pixel preflight omitted a frame")
    states = [frame["State"] for frame in frames]
    if request["Frames"] >= 300:
        scenario = request["Scenario"]
        if scenario == "hover-press" and not (any(s["Hovered"] for s in states)
                                               and any(s["Pressed"] for s in states) and states[-1]["Clicks"] >= 1):
            raise ValueError("Host mouse schedule did not reach actual hover, press and click")
        if scenario == "pulse":
            for state in states:
                if state["PulseFrame"] >= 24 and state["ScoreScale"] != 1:
                    raise ValueError("400ms score pulse did not restore its scale")
                if state["PulseFrame"] == 12 and state["ScoreScale"] < 1.17:
                    raise ValueError("400ms score pulse did not reach its visible midpoint")
        field = {"text": "StaticText", "scrolling": "Scroll", "resize-scale": "Scale",
                 "continuous-animation": "SourceColor"}.get(scenario)
        if field and len({state[field] for state in states}) < 2:
            raise ValueError("Requested changing workload remained static")
    if request["Scenario"] == "resource-soak" and request["Frames"] >= 400:
        if len(capture["sentinelCheckpoints"]) != math.ceil(request["Frames"] / 25):
            raise ValueError("Soak omitted sampled native sentinel/pixel checkpoints")
        if set(state["Operation"] for state in states) != set(range(16)):
            raise ValueError("Resource soak omitted scheduled operations")
        if not checks["LateMutationChecks"] or not checks["RetirementChecks"] or not checks["IdleChecks"]:
            raise ValueError("Resource soak omitted deterministic generation/retirement/idle assertions")
        if not checks["latePixelChecks"]:
            raise ValueError("Resource soak omitted the exact changed-fill pixel check after Prepare")
        if request["Mode"] != "normal" and (not checks["InFlightChecks"] or not counters["Evictions"]
                                              or not counters["Allocations"]):
            raise ValueError("Resource soak never exercised actual candidates and budget eviction")
    return benchmark.digest(states)


def validate_pair(normal, candidate):
    if [f["State"] for f in normal["frames"]] != [f["State"] for f in candidate["frames"]]:
        raise ValueError("Equivalent mode state/input/clock diverged")
    if normal["pixelHashes"] != candidate["pixelHashes"]:
        raise ValueError("Matched normal/cached exact RGBA preflight failed")
    if normal["screenshotSha256"] != candidate["screenshotSha256"]:
        raise ValueError("Matched final screenshot differs")
    if normal.get("sentinelCheckpoints") != candidate.get("sentinelCheckpoints"):
        raise ValueError("Matched resource soak pixel checkpoint differs")


def summarize(frames, warm_statistics=None):
    result = compare_normal.summarize(frames)
    for part in ("PrepareGraphics",):
        for metric in benchmark.COUNTERS:
            result[f"{part}.{metric}"] = benchmark.distribution([frame[part][metric] for frame in frames])
    for metric in ("TextureChanges", "PrepareTextureChanges"):
        result[metric] = benchmark.distribution([frame[metric] for frame in frames])
    for metric in ("RetainedBytes", "Entries", "Pending"):
        result["Cache." + metric] = benchmark.distribution([frame["Cache"][metric] for frame in frames])
    first, last = warm_statistics or frames[0]["Cache"]["Statistics"], frames[-1]["Cache"]["Statistics"]
    result["cacheCounterInterval"] = {key: last[key] - first[key] for key in first if key != "LastBypassReason"}
    return result


def comparison_gate(normal, candidate, percentile):
    result = compare_normal.gate(normal, candidate, percentile)
    result["candidate"] = result.pop("disabled")
    return result


def capture_timeout(scenario, frames, diagnostic):
    if diagnostic:
        return 60
    return 600 if scenario == "resource-soak" and frames >= 10_000 else 180


def historical_regressions(normal_report, baseline):
    """Investigation trigger, not a portable timing assertion or same-source proof."""
    results = []
    for workload in normal_report["workloads"]:
        old = next(value for value in baseline["workloads"]
                   if all(value[key] == workload[key] for key in ("scenario", "width", "height", "uiScale")))
        for previous, current in (("UpdateLayout", "UpdateLayout"), ("UiSubmission", "UiPlusPrepare")):
            reference = statistics.median(run["summary"][previous + ".CpuMs"]["median"] for run in old["runs"])
            medians = [run["summary"][current + ".CpuMs"]["median"] for run in workload["runs"]["normal"]]
            repeated = sum(value - reference > .1 and value > reference * 1.05 for value in medians) >= 2
            results.append({"scenario": workload["scenario"], "width": workload["width"], "height": workload["height"],
                            "uiScale": workload["uiScale"], "stage": current, "baselineMedian": reference,
                            "currentRepeatMedians": medians, "investigationRequired": repeated})
    return results


def load_normal_evidence(path, source_sha, binaries, baseline_path):
    """Accept a normal-only54 capture or the normal half of an existing108 capture.

    Raw Prepare samples are read even when the original normal-only summary did
    not summarize that additive phase. An older source is never called current.
    """
    report = json.loads(path.read_text())
    if report.get("mode") == "normal-only-with-binaries":
        if report["sourceSha256"] != source_sha or report["binaries"] != binaries:
            raise ValueError("Normal-only wrapper does not identify the same current default binaries/source")
        path = path.parent / report["normalReport"]
        report = json.loads(path.read_text())
    if report.get("binaries") != binaries:
        raise ValueError("Normal evidence does not identify the same frozen default binaries")
    baseline = json.loads((baseline_path / "summary.json").read_text())
    keys = ("scenario", "width", "height", "uiScale")
    expected = {tuple(value[key] for key in keys) for value in baseline["workloads"]}
    actual = [tuple(value[key] for key in keys) for value in report["workloads"]]
    if (report["sourceSha256"] != source_sha or len(actual) != len(expected) or set(actual) != expected
            or report["seed"] != 1729 or report["warmupFrames"] != 120 or report["measuredFrames"] != 300):
        raise ValueError("Normal evidence must cover all18 historical workloads on this exact current frozen source")
    normalized = {"workloads": []}
    for workload in report["workloads"]:
        runs = workload["runs"] if isinstance(workload["runs"], list) else workload["runs"]["normal"]
        if len(runs) < 3:
            raise ValueError("Normal evidence needs at least three repeated captures")
        scenario, width, height, scale = (workload[key] for key in keys)
        with gzip.open(baseline_path / f"{scenario}-{width}x{height}-s{scale}-r1.json.gz", "rt") as stream:
            historical = json.load(stream)
        normalized_runs = []
        for run in runs:
            with gzip.open(path.parent / run["raw"], "rt") as stream:
                value = json.load(stream)
            if value.get("provenance", {}).get("binaries") != binaries:
                raise ValueError("Normal raw capture omitted matching default binary provenance")
            benchmark.validate_capture(value, scenario, width, height, scale, 1729, 120, 300, "normal")
            compare_normal.validate_baseline(value, historical)
            if any("Prepare" not in frame for frame in value["frames"]):
                raise ValueError("Current normal evidence omitted additive Prepare samples")
            normalized_runs.append({**run, "summary": compare_normal.summarize(value["frames"])})
        normalized["workloads"].append({**{key: workload[key] for key in keys}, "runs": {"normal": normalized_runs}})
    return normalized, baseline


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--resume", action="store_true", help="Reuse only complete artifact-verified cohorts with identical source/default binaries/renderer/configuration")
    parser.add_argument("--scenario", action="append", choices=SCENARIOS)
    parser.add_argument("--configuration", action="append", type=int, choices=range(3))
    parser.add_argument("--warmup", type=int, default=120)
    parser.add_argument("--frames", type=int, default=300)
    parser.add_argument("--repeats", type=int, default=3)
    parser.add_argument("--normal-report", type=Path, help="Final-source binary-provenant normal-only54 summary, or normal half of existing108 report")
    parser.add_argument("--capture-normal-only", action="store_true", help="Capture54 current normal runs with before/after binary provenance; no mixed runs")
    parser.add_argument("--diagnostic", action="store_true", help="Reduced captures are not acceptance evidence")
    parser.add_argument("--verification-only", action="store_true")
    parser.add_argument("--soak-only", action="store_true")
    args = parser.parse_args()
    if args.warmup < 8 or args.frames < 1 or args.repeats < 1:
        parser.error("Invalid frame/repeat counts")
    if not args.diagnostic and (args.warmup < 120 or args.frames < 300 or args.repeats < 3):
        parser.error("Acceptance captures require120warmup/300samples/3repeats")
    if not args.diagnostic and not args.normal_report and not args.capture_normal_only:
        parser.error("Acceptance evidence requires a final-source full normal comparison report")
    if not args.diagnostic and (args.scenario or args.configuration or args.verification_only or args.soak_only):
        parser.error("Partial selections require --diagnostic; they are not the full acceptance matrix")
    if args.capture_normal_only and (args.warmup != 120 or args.frames != 300 or args.repeats != 3
                                    or args.scenario or args.configuration or args.verification_only or args.soak_only or args.normal_report):
        parser.error("Normal-only capture uses the standard54-run baseline parameters without mixed selections")
    output = args.output.resolve()
    source = benchmark.fingerprint()
    host = benchmark.ROOT / "MGUI.RenderCacheTestHost/bin/Release/net8.0/MGUI.RenderCacheTestHost.dll"
    binaries = binary_fingerprint(host)
    identity = {key: source[key] for key in ("commit", "sourceSha256", "fixtureSha256", "dirtySha256")}
    identity.update(binaries=binaries, configuration={key: getattr(args, key) for key in (
        "warmup", "frames", "repeats", "diagnostic", "verification_only", "soak_only", "capture_normal_only", "scenario", "configuration")},
        normalReportSha256=hashlib.sha256(args.normal_report.read_bytes()).hexdigest() if args.normal_report else None)
    store = CohortStore(output, identity, args.resume)
    (output / "source.json").write_text(json.dumps(source, indent=2))
    if args.capture_normal_only:
        normal_cohorts(output, source, binaries, host, store)
        return
    report = {"schemaVersion": 1, "command": ["python3", "tools/render-cache/stability.py", *benchmark.os.sys.argv[1:]],
              "commit": source["commit"], "sourceSha256": source["sourceSha256"], "fixtureSha256": source["fixtureSha256"],
              "binaries": binaries,
              "configuration": "Release", "diagnostic": args.diagnostic, "commands": [], "preflight": [],
              "workloads": [], "soaks": [], "normalRegressionSignals": [], "environment": {
                  "os": platform.platform(), "cpu": next((line.split(":", 1)[1].strip()
                      for line in Path("/proc/cpuinfo").read_text().splitlines() if line.startswith("model name")), "unknown")},
              "percentileMethod": "nearest rank ceil(n*p); median averages middle two values for even sample counts",
              "metricScope": "Game-thread phase-local managed allocations and CPU/wall time; per-raw-frame subtotal excludes host loop; no GPU/hardware FPS claim",
              "startedUtc": store.state["startedUtc"]}
    if args.normal_report:
        normal, baseline = load_normal_evidence(args.normal_report, source["sourceSha256"], binaries,
                                               benchmark.ROOT / "benchmark-results/render-cache/baseline-2026-10-17")
        report["normalRegressionSignals"] = historical_regressions(normal, baseline)
        report["normalReport"] = str(args.normal_report.resolve())
        report["normalReportSha256"] = hashlib.sha256(args.normal_report.read_bytes()).hexdigest()
        report["normalEvidenceScope"] = "Current frozen source, normal-only54 captures consumed; raw state/pixels/counters match immutable baseline; raw Prepare included"

    active_directory = output
    active_key = ""

    def capture(env, name, scenario, mode, width, height, scale, frames, verify):
        raw, image = active_directory / (name + ".json"), active_directory / (name + ".png")
        request = {"Scenario": scenario, "Mode": mode, "Width": width, "Height": height, "Scale": scale,
                   "Warmup": args.warmup, "Frames": frames, "VerifyPixels": verify,
                   "Output": str(raw), "Screenshot": str(image)}
        configuration = active_directory / (name + ".request.json")
        configuration.write_text(json.dumps(request, indent=2))
        command = ["dotnet", str(host), "--stability", str(configuration)]
        report["commands"].append({"cwd": str(host.parent), "argv": command})
        log = active_directory / (name + ".log")
        try:
            log.write_text(benchmark.run(command, cwd=host.parent, env=env,
                                         timeout=capture_timeout(scenario, frames, args.diagnostic)))
        except BaseException as error:
            log.write_text(str(error))
            store.fail(active_key, error)
            raise
        value = json.loads(raw.read_text())
        state_hash = validate_capture(value, request)
        png = image.read_bytes()
        if (png[:8] != b"\x89PNG\r\n\x1a\n" or struct.unpack(">II", png[16:24]) != (width, height)
                or hashlib.sha256(png).hexdigest() != value["screenshotSha256"]):
            raise ValueError("Native screenshot dimensions/hash differs")
        value["provenance"] = {key: source[key] for key in ("commit", "sourceSha256", "fixtureSha256", "dirtySha256")}
        value["provenance"]["binaries"] = binaries
        with gzip.open(str(raw) + ".gz", "wt") as stream:
            json.dump(value, stream, separators=(",", ":"))
        raw.unlink()
        print(name, flush=True)
        return value, {"raw": str(raw) + ".gz", "screenshot": str(image), "captureLog": str(log), "stateSha256": state_hash,
                       "summary": summarize(value["frames"], value["resourceChecks"]["warmStatistics"]),
                       "resourceChecks": value["resourceChecks"]}

    with benchmark.native_display() as env:
        env["LIBGL_ALWAYS_SOFTWARE"] = "1"
        report["environment"].update(glxinfo=benchmark.run(["glxinfo", "-B"], env=env),
                                      dotnet=benchmark.run(["dotnet", "--info"]), display=env["DISPLAY"])
        store.check_renderer(report["environment"]["glxinfo"])
        for configuration in args.configuration if args.configuration is not None else range(3):
            width, height, scale = benchmark.CONFIGURATIONS[configuration]
            for scenario in () if args.soak_only else args.scenario or SCENARIOS:
                prefix = f"{scenario}-{width}x{height}-s{scale}"
                active_key = "mixed-" + prefix
                saved = store.get(active_key)
                if saved is not None:
                    report["preflight"].extend(saved["preflight"])
                    report["commands"].extend(saved["commands"])
                    if saved["workload"] is not None:
                        report["workloads"].append(saved["workload"])
                    print("Reused complete cohort " + active_key, flush=True)
                    continue
                active_directory = store.begin(active_key)
                command_start = len(report["commands"])
                preflight_start = len(report["preflight"])
                matched = {}
                for mode in MODES:
                    value, artifact = capture(env, prefix + "-verify-" + mode, scenario, mode, width, height, scale, args.frames, True)
                    matched[mode] = value
                    report["preflight"].append(artifact)
                for mode in MODES[1:]:
                    validate_pair(matched["normal"], matched[mode])
                if args.verification_only:
                    assert_frozen(source, binaries, host)
                    store.complete(active_key, {"preflight": report["preflight"][preflight_start:], "workload": None,
                                                "commands": report["commands"][command_start:]})
                    continue
                runs = {mode: [] for mode in MODES}
                pooled = {mode: [] for mode in MODES}
                for repeat in range(args.repeats):
                    for mode in MODES[repeat % 3:] + MODES[:repeat % 3]:
                        value, artifact = capture(env, prefix + f"-r{repeat + 1}-" + mode, scenario, mode,
                                                  width, height, scale, args.frames, False)
                        if artifact["stateSha256"] != benchmark.digest([f["State"] for f in matched[mode]["frames"]]):
                            raise ValueError("Measured state differs from pixel-validated preflight")
                        if value["screenshotSha256"] != matched[mode]["screenshotSha256"]:
                            raise ValueError("Measured final pixels differ from pixel-validated preflight")
                        runs[mode].append(artifact)
                        pooled[mode].extend(value["frames"])
                gates = {mode: {phase + "." + percentile: comparison_gate(
                    [run["summary"][phase + ".CpuMs"] for run in runs["normal"]],
                    [run["summary"][phase + ".CpuMs"] for run in runs[mode]], percentile)
                    for phase in ("UpdateLayout", "UiPlusPrepare") for percentile in ("median", "p95")}
                    for mode in MODES[1:]}
                pooled_summaries = {mode: summarize(pooled[mode]) for mode in MODES}
                for summary in pooled_summaries.values():
                    del summary["cacheCounterInterval"]
                report["workloads"].append({"scenario": scenario, "width": width, "height": height, "uiScale": scale,
                                            "exactPreflight": True, "runs": runs, "noiseAwareComparisons": gates,
                                            "pooledSummary": pooled_summaries,
                                            "repeatMedianSummary": {mode: {
                                                key: benchmark.distribution([run["summary"][key]["median"] for run in runs[mode]])
                                                for key in runs[mode][0]["summary"] if key != "cacheCounterInterval"}
                                                for mode in MODES}})
                assert_frozen(source, binaries, host)
                store.complete(active_key, {"preflight": report["preflight"][preflight_start:], "workload": report["workloads"][-1],
                                            "commands": report["commands"][command_start:]})
        if not args.scenario:
            active_key = "resource-cycle-and-soak"
            saved = store.get(active_key)
            if saved is not None:
                report["preflight"].extend(saved["preflight"])
                report["soaks"].extend(saved["soaks"])
                report["commands"].extend(saved["commands"])
                print("Reused complete cohort " + active_key, flush=True)
            else:
                active_directory = store.begin(active_key)
                command_start = len(report["commands"])
                preflight_start = len(report["preflight"])
                preflight = {}
                for mode in MODES:
                    value, artifact = capture(env, "soak-verify-" + mode, "resource-soak", mode, 1280, 720, 1, 400, True)
                    preflight[mode] = value
                    report["preflight"].append(artifact)
                for mode in MODES[1:]:
                    validate_pair(preflight["normal"], preflight[mode])
                if not args.verification_only:
                    soaks = {}
                    for mode in MODES:
                        value, artifact = capture(env, "soak-10000-" + mode, "resource-soak", mode, 1280, 720, 1, 10_000, False)
                        soaks[mode] = value
                        report["soaks"].append(artifact)
                    for mode in MODES[1:]:
                        validate_pair(soaks["normal"], soaks[mode])
                assert_frozen(source, binaries, host)
                store.complete(active_key, {"preflight": report["preflight"][preflight_start:], "soaks": report["soaks"],
                                            "commands": report["commands"][command_start:]})
    after = benchmark.fingerprint()
    if any(source[key] != after[key] for key in ("commit", "sourceSha256", "fixtureSha256", "dirtySha256")):
        raise ValueError("Frozen source changed during capture; discard and rerun")
    if binary_fingerprint(host) != binaries:
        raise ValueError("Default DLL/runtime assets changed during capture; discard and rerun")
    report["cohortCheckpoint"] = "cohorts.json"
    report["cohortCheckpointSha256"] = hashlib.sha256(store.path.read_bytes()).hexdigest()
    report["attempts"] = store.state["attempts"]
    report["completedUtc"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    report["regressionInvestigationRequired"] = any(value["investigationRequired"] for value in report["normalRegressionSignals"])
    (output / "summary.json").write_text(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
