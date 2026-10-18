#!/usr/bin/env python3
"""Native MGUI baseline runner. Standard library only; see benchmark-results/render-cache/README.md."""
import argparse
from contextlib import contextmanager
import fcntl
import gzip
import hashlib
import json
import math
import os
from pathlib import Path
import platform
import select
import signal
import statistics
import struct
import tempfile
import subprocess
import time

ROOT = Path(__file__).resolve().parents[2]
SCENARIOS = ("batched", "layered", "hud", "animated-score", "scrolling", "nested-clips")
CONFIGURATIONS = ((1920, 1080, 1), (1920, 1080, 1.5), (1280, 720, 1))
PHASES = ("UpdateLayout", "WorldSubmission", "UiSubmission", "Present")
COUNTERS = ("DrawCalls", "Primitives", "Sprites", "TargetSwitches")


def run(command, *, cwd=ROOT, env=None, timeout=180):
    """Kill the owned process group on timeout, including native child processes."""
    process = subprocess.Popen(command, cwd=cwd, env=env, stdout=subprocess.PIPE,
                               stderr=subprocess.STDOUT, text=True, start_new_session=True)
    try:
        output, _ = process.communicate(timeout=timeout)
    except BaseException:
        if process.poll() is None:
            try:
                os.killpg(process.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
        process.communicate()
        raise
    if process.returncode:
        raise RuntimeError(f"Command failed ({process.returncode}): {command!r}\n{output}")
    return output


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":")).encode()).hexdigest()


def fingerprint():
    listed = run(["git", "ls-files", "-z", "--cached", "--others", "--exclude-standard"]).split("\0")
    suffixes = {".cs", ".csproj", ".props", ".targets", ".xaml", ".fx", ".mgcb", ".ttf", ".spritefont", ".png"}
    files = {}
    for name in sorted(set(listed)):
        path = ROOT / name
        if (name and not name.startswith(("benchmark-results/", ".beads/"))
                and path.is_file() and (path.suffix.lower() in suffixes or name.startswith("tools/render-cache/") and path.suffix == ".py")):
            files[name] = hashlib.sha256(path.read_bytes()).hexdigest()
    fixture_files = {name: value for name, value in files.items()
                     if name.startswith(("MGUI.RenderCacheTestHost/", "tools/render-cache/"))
                     or name in ("MGUI.EffectTestHost/Content/EffectParameters.fx",
                                 "MGUI.Samples/Content/Fonts/ttf/NotoSans-Regular.ttf")}
    status = run(["git", "status", "--porcelain=v1", "--untracked-files=all"]).splitlines()
    dirty_files = {}
    for item in run(["git", "status", "--porcelain=v1", "-z", "--untracked-files=all"]).split("\0"):
        name = item[3:]
        path = ROOT / name
        if name and not name.startswith(("benchmark-results/", ".beads/")) and path.is_file():
            dirty_files[name] = hashlib.sha256(path.read_bytes()).hexdigest()
    return {"dirtyPaths": [line[3:] for line in status], "dirtyFiles": dirty_files, "dirtySha256": digest(dirty_files),
            "commit": run(["git", "rev-parse", "HEAD"]).strip(),
            "dirtyStatusPorcelain": status,
            "sourceSha256": digest(files), "fixtureSha256": digest(fixture_files), "files": files}


@contextmanager
def native_display():
    """Own a private X server for the complete suite; never borrow the user's DISPLAY."""
    with open("/tmp/mgui-render-cache-graphics.lock", "a") as lock, tempfile.TemporaryFile() as diagnostics:
        fcntl.flock(lock, fcntl.LOCK_EX)
        server = subprocess.Popen(["Xvfb", f":{100 + os.getpid() % 30000}", "-displayfd", "1", "-screen", "0", "1920x1080x24", "-nolisten", "tcp"],
                                  stdout=subprocess.PIPE, stderr=diagnostics, text=True, start_new_session=True)
        try:
            if not select.select([server.stdout], [], [], 15)[0]:
                raise RuntimeError("Private Xvfb did not become ready within 15 seconds")
            display = server.stdout.readline().strip()
            if not display.isdigit() or display == "0" or server.poll() is not None:
                raise RuntimeError("Private Xvfb failed to allocate a display")
            env = dict(os.environ, DISPLAY=f":{display}", ALSOFT_DRIVERS="null")
            yield env
        finally:
            if server.poll() is None:
                server.terminate()
            try:
                server.communicate(timeout=5)
            except subprocess.TimeoutExpired:
                server.kill()
                server.communicate()


def distribution(values):
    if not values or any(not math.isfinite(value) or value < 0 for value in values):
        raise ValueError("Expected finite nonnegative samples")
    ordered = sorted(values)
    return {"median": statistics.median(ordered), "p95": ordered[math.ceil(len(ordered) * .95) - 1],
            "min": ordered[0], "max": ordered[-1], "count": len(ordered)}


def validate_capture(capture, scenario, width, height, scale, seed, warmup, frames):
    if (capture["schemaVersion"], capture["mode"], capture["Scenario"], capture["Width"], capture["Height"], capture["uiScale"]) != (1, "normal", scenario, width, height, scale):
        raise ValueError("Capture metadata differs from requested workload")
    if capture["Seed"] != seed or capture["warmupFrames"] != warmup:
        raise ValueError("Capture seed/warmup differs from requested workload")
    if capture["measuredPresentedFrames"] != frames or capture["totalPresentedFrames"] != warmup + frames + 1 or len(capture["frames"]) != frames:
        raise ValueError("Capture has incomplete presented frames")
    if capture["gpuTimeMs"] is not None or not capture["gpuTimeUnavailableReason"]:
        raise ValueError("Expected explicitly unavailable GPU timing")
    pixels = capture["nativePixelValidation"]
    if not pixels["worldSentinelsMatch"] or pixels["changedUiPixels"] < 100 or pixels["brightTextPixels"] < 20 or pixels["captureFrame"] != warmup + frames:
        raise ValueError("Native backbuffer evidence does not contain world and visible UI/text")
    for index, frame in enumerate(capture["frames"], warmup):
        if frame["Frame"] != index or frame["State"]["SimulationFrame"] != index or frame["State"]["WorldOffset"] != index % 64:
            raise ValueError("Frame continuity / deterministic workload state violated")
        for phase in PHASES:
            for value in frame[phase].values():
                if not isinstance(value, (int, float)) or not math.isfinite(value) or value < 0:
                    raise ValueError("Invalid CPU/allocation sample")
        for key in COUNTERS:
            world, ui, total = (frame[part][key] for part in ("World", "Ui", "Total"))
            if min(world, ui, total) < 0 or world + ui != total:
                raise ValueError("World/UI graphics counter attribution violated")
        if min(frame["Ui"]["DrawCalls"], frame["World"]["DrawCalls"], frame["Ui"]["Primitives"], frame["World"]["Primitives"]) <= 0:
            raise ValueError("Expected both world and UI graphics submissions")
    states = [frame["State"] for frame in capture["frames"]]
    if frames > 2 and scenario == "animated-score" and len({state["Score"] for state in states}) != frames:
        raise ValueError("Animated score did not change independently each frame")
    if frames > 2 and scenario in ("scrolling", "nested-clips") and len({state["ScrollOffset"] for state in states}) < 2:
        raise ValueError("Scrolling workload did not move")
    return digest(states)


def summarize(frames):
    result = {}
    for phase in PHASES:
        for metric in ("CpuMs", "AllocatedBytes"):
            result[f"{phase}.{metric}"] = distribution([frame[phase][metric] for frame in frames])
    for part in ("World", "Ui", "Total"):
        for metric in COUNTERS:
            result[f"{part}.{metric}"] = distribution([frame[part][metric] for frame in frames])
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True, help="New result directory; existing paths are never overwritten")
    parser.add_argument("--scenario", action="append", choices=SCENARIOS, help="Default: all six")
    parser.add_argument("--warmup", type=int, default=120)
    parser.add_argument("--frames", type=int, default=300)
    parser.add_argument("--repeats", type=int, default=3)
    parser.add_argument("--seed", type=int, default=1729)
    parser.add_argument("--skip-build", action="store_true", help="Use only after an explicit current-source Release build")
    args = parser.parse_args()
    if args.warmup < 0 or args.frames < 1 or args.repeats < 1:
        parser.error("warmup must be nonnegative; frames and repeats must be positive")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    commands = []
    if not args.skip_build:
        build = ["flock", "--close", "/tmp/mgui-render-cache-dotnet.lock", "dotnet", "build",
                 "MGUI.RenderCacheTestHost/MGUI.RenderCacheTestHost.csproj", "-c", "Release", "--nologo", "-v:q"]
        commands.append({"cwd": str(ROOT), "argv": build})
        (output / "build.log").write_text(run(build, timeout=300))
    source = fingerprint()
    (output / "source.json").write_text(json.dumps(source, indent=2))
    host = ROOT / "MGUI.RenderCacheTestHost/bin/Release/net8.0/MGUI.RenderCacheTestHost.dll"
    report = {"schemaVersion": 1, "mode": "normal", "startedUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
              "command": ["python3", "tools/render-cache/benchmark.py", *os.sys.argv[1:]],
              "sourceSha256": source["sourceSha256"], "fixtureSha256": source["fixtureSha256"], "dirtySha256": source["dirtySha256"],
              "commit": source["commit"], "configuration": "Release", "seed": args.seed,
              "warmupFrames": args.warmup, "measuredFrames": args.frames, "repeats": args.repeats,
              "percentileMethod": "nearest rank ceil(n*p); median averages two middle values for even n",
              "allocationScope": "GC.GetAllocatedBytesForCurrentThread; phase-local managed allocations on the game thread only",
              "clock": "deterministic simulation (presented frame + 1)/60 seconds; uncapped wall-clock execution",
              "gpuTimeMs": None, "gpuTimeUnavailableReason": "No portable MonoGame DesktopGL timer query API",
              "environment": {"os": platform.platform(), "cpu": next((line.split(':', 1)[1].strip() for line in Path('/proc/cpuinfo').read_text().splitlines() if line.startswith('model name')), "unknown")},
              "workloads": [], "commands": commands}
    with native_display() as env:
        report["environment"]["display"] = env["DISPLAY"]
        report["environment"]["glxinfo"] = run(["glxinfo", "-B"], env=env)
        report["environment"]["dotnet"] = run(["dotnet", "--info"])
        for width, height, scale in CONFIGURATIONS:
            for scenario in args.scenario or SCENARIOS:
                repeats = []
                pooled = []
                state_hash = None
                for repeat in range(1, args.repeats + 1):
                    name = f"{scenario}-{width}x{height}-s{scale}-r{repeat}"
                    raw = output / (name + ".json")
                    screenshot = output / (name + ".png")
                    command = ["dotnet", str(host), scenario, str(width), str(height), str(scale), str(args.seed),
                               str(args.warmup), str(args.frames), str(raw), str(screenshot)]
                    commands.append({"cwd": str(host.parent), "argv": command})
                    log = run(command, cwd=host.parent, env=env)
                    (output / (name + ".log")).write_text(log)
                    capture = json.loads(raw.read_text())
                    current_hash = validate_capture(capture, scenario, width, height, scale, args.seed, args.warmup, args.frames)
                    if state_hash is not None and state_hash != current_hash:
                        raise ValueError("Repeated workload state diverged")
                    state_hash = current_hash
                    image_bytes = screenshot.read_bytes()
                    if image_bytes[:8] != b"\x89PNG\r\n\x1a\n" or struct.unpack(">II", image_bytes[16:24]) != (width, height):
                        raise ValueError("Screenshot PNG dimensions differ from requested backbuffer")
                    if hashlib.sha256(image_bytes).hexdigest() != capture["screenshotSha256"]:
                        raise ValueError("Native screenshot hash differs")
                    capture["provenance"] = {"commit": source["commit"], "sourceSha256": source["sourceSha256"],
                                             "fixtureSha256": source["fixtureSha256"], "dirtySha256": source["dirtySha256"],
                                             "dirtyPaths": source["dirtyPaths"], "sourceManifest": "source.json",
                                             "renderer": report["environment"]["glxinfo"], "configuration": "Release"}
                    with gzip.open(str(raw) + ".gz", "wb") as compressed:
                        compressed.write(json.dumps(capture, separators=(",", ":")).encode())
                    raw.unlink()
                    repeats.append({"repeat": repeat, "raw": name + ".json.gz", "screenshot": name + ".png", "summary": summarize(capture["frames"])})
                    pooled.extend(capture["frames"])
                    print(f"{name}: {args.frames} measured / {capture['totalPresentedFrames']} presented", flush=True)
                pooled_summary = summarize(pooled)
                report["workloads"].append({"scenario": scenario, "width": width, "height": height, "uiScale": scale,
                                            "stateSha256": state_hash, "runs": repeats, "pooledSummary": pooled_summary,
                                            "repeatMedianSummary": {key: distribution([item["summary"][key]["median"] for item in repeats]) for key in pooled_summary}})
    after = fingerprint()
    if (source["sourceSha256"], source["fixtureSha256"], source["dirtySha256"], source["commit"]) != (after["sourceSha256"], after["fixtureSha256"], after["dirtySha256"], after["commit"]):
        raise RuntimeError("Source changed during capture; discard this baseline and rerun")
    report["completedUtc"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    (output / "summary.json").write_text(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
