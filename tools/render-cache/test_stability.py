import copy
import gzip
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import stability


def capture(frames=1, scenario="idle-hud"):
    request = {"Scenario": scenario, "Mode": "normal", "Width": 1280, "Height": 720, "Scale": 1,
               "Warmup": 120, "Frames": frames, "VerifyPixels": True, "Output": "capture.json", "Screenshot": "capture.png"}
    values = []
    for index in range(120, 120 + frames):
        counters = {key: 0 for key in stability.benchmark.COUNTERS}
        state = {"Frame": index, "ClockTicks": (index + 1) * 10_000_000 // 60, "Hovered": False,
                 "Pressed": False, "Clicks": 0, "PulseFrame": (index - 120) % 120, "ScoreScale": 1}
        frame = {"Frame": index, "State": state,
                 "Cache": {"RetainedBytes": 0, "Entries": 0, "Pending": 0, "ByteLimit": 100,
                           "EntryLimit": 2, "Statistics": {"Allocations": 0, "Disposals": 0, "Evictions": 0}},
                 "World": dict(counters), "Ui": dict(counters), "Total": dict(counters)}
        for phase in stability.PHASES:
            frame[phase] = {"CpuMs": .1, "AllocatedBytes": 0}
        values.append(frame)
    result = {"schemaVersion": 1, "configuration": "Release", "options": request, "frames": values, "measuredPresentedFrames": frames,
              "totalPresentedFrames": 124 + frames, "drawableWarmupFrames": 3,
              "actualDrawableVerified": True, "gpuTimeMs": None,
              "gpuTimeUnavailableReason": "No native timer query", "pixelHashes": ["a" * 64] * frames,
              "screenshotSha256": "b" * 64,
              "resourceChecks": {"IdleChecks": frames, "Teardown": {"IsDisposed": True, "RetainedBytes": 0, "Entries": 0, "Pending": 0,
                                               "Allocations": 0, "Disposals": 0}}}
    return result, request


class StabilityValidationTests(unittest.TestCase):
    def test_resume_reuses_complete_cohort_and_retains_incomplete_attempt(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "capture"
            store = stability.CohortStore(output, {"source": "frozen", "binaries": {"Core": "one"}})
            first = store.begin("idle")
            raw = first / "frame.gz"
            raw.write_bytes(b"validated complete capture")
            store.complete("idle", {"raw": str(raw), "value": 7})
            incomplete = store.begin("pulse")
            failed = incomplete / "partial.json"
            failed.write_bytes(b"incomplete matched cohort")
            resumed = stability.CohortStore(output, store.state["identity"], True)
            self.assertEqual(resumed.get("idle")["value"], 7)
            self.assertIsNone(resumed.get("pulse"))
            self.assertEqual(resumed.state["attempts"][-1]["status"], "interrupted")
            next_attempt = resumed.begin("pulse")
            self.assertNotEqual(next_attempt, incomplete)
            self.assertEqual(failed.read_bytes(), b"incomplete matched cohort")
            with self.assertRaisesRegex(ValueError, "never overwritten"):
                resumed.begin("idle")

    def test_resume_rejects_changed_context_artifacts_or_renderer(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "capture"
            identity = {"source": "one", "binaries": "one", "configuration": {"frames": 300}}
            store = stability.CohortStore(output, identity)
            attempt = store.begin("idle")
            image = attempt / "frame.png"
            image.write_bytes(b"validated pixels")
            store.complete("idle", {"screenshot": str(image)})
            for key, value in (("source", "two"), ("binaries", "two"), ("configuration", {"frames": 299})):
                with self.assertRaisesRegex(ValueError, "provenance changed"):
                    stability.CohortStore(output, {**identity, key: value}, True)
            image.write_bytes(b"different pixels")
            with self.assertRaisesRegex(ValueError, "artifact changed"):
                stability.CohortStore(output, identity, True).get("idle")
            renderer = "OpenGL vendor string: Mesa\nOpenGL renderer string: llvmpipe\nOpenGL version string:4.5"
            store.check_renderer(renderer)
            with self.assertRaisesRegex(ValueError, "Renderer changed"):
                store.check_renderer(renderer.replace("llvmpipe", "other"))

    def test_normal_resume_repeats_failed_whole_cohort_without_rerunning_completed_one(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "capture"
            identity = {"source": "fixed"}
            store = stability.CohortStore(output, identity)
            calls = []
            renderer = "OpenGL vendor string: Mesa\nOpenGL renderer string: llvmpipe\nOpenGL version string:4.5"

            def producer(command, timeout):
                scenario = command[command.index("--scenario") + 1]
                calls.append(scenario)
                if scenario == "hud" and calls.count("hud") == 1:
                    raise RuntimeError("interrupted native cohort")
                target = Path(command[command.index("--output") + 1])
                target.mkdir()
                with gzip.open(target / "raw.gz", "wt") as stream:
                    json.dump({"provenance": {}}, stream)
                (target / "image.png").write_bytes(b"test artifact, not native evidence")
                (target / "summary.json").write_text(json.dumps({"sourceSha256": "fixed", "environment": {"glxinfo": renderer},
                    "commands": [], "workloads": [{"runs": [{"raw": "raw.gz", "screenshot": "image.png"}]}]}))
                return "producer completed"

            with patch.object(stability.benchmark, "SCENARIOS", ("batched", "hud")), \
                    patch.object(stability.benchmark, "run", side_effect=producer), patch.object(stability, "assert_frozen"):
                with self.assertRaisesRegex(RuntimeError, "interrupted"):
                    stability.normal_cohorts(output, {"sourceSha256": "fixed"}, {"Core": "fixed"}, Path("host"), store)
                resumed = stability.CohortStore(output, identity, True)
                stability.normal_cohorts(output, {"sourceSha256": "fixed"}, {"Core": "fixed"}, Path("host"), resumed)
            self.assertEqual(calls, ["batched", "hud", "hud"])
            self.assertEqual([attempt["status"] for attempt in resumed.state["attempts"]], ["complete", "failed", "complete"])
            self.assertEqual(len(resumed.state["completed"]), 2)

    def test_child_timeouts_distinguish_diagnostic_normal_and_actual_soak(self):
        self.assertEqual(stability.capture_timeout("pulse", 300, True), 60)
        self.assertEqual(stability.capture_timeout("resource-soak", 10_000, True), 60)
        self.assertEqual(stability.capture_timeout("resource-soak", 400, False), 180)
        self.assertEqual(stability.capture_timeout("pulse", 300, False), 180)
        self.assertEqual(stability.capture_timeout("resource-soak", 10_000, False), 600)

    def test_default_binary_manifest_detects_core_drift_and_missing_font_dependency(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            names = ("MGUI.RenderCacheTestHost.dll", "MGUI.Core.dll", "MGUI.Shared.dll", "MGUI.FontStashSharp.dll",
                     "FontStashSharp.MonoGame.dll", "FontStashSharp.Base.dll", "MonoGame.Framework.dll")
            for name in names:
                (root / name).write_bytes(name.encode())
            (root / "Content").mkdir()
            (root / "Content/NotoSans-Regular.ttf").write_bytes(b"actual runtime font")
            (root / "Content/EffectParameters.xnb").write_bytes(b"actual compiled effect")
            host = root / names[0]
            before = stability.binary_fingerprint(host)
            self.assertIn("Content/NotoSans-Regular.ttf", before)
            self.assertIn("Content/EffectParameters.xnb", before)
            (root / "MGUI.Core.dll").write_bytes(b"changed")
            self.assertNotEqual(before, stability.binary_fingerprint(host))
            (root / "FontStashSharp.Base.dll").unlink()
            with self.assertRaisesRegex(ValueError, "complete current-source"):
                stability.binary_fingerprint(host)

    def test_normal_wrapper_cannot_relabel_different_binaries_as_current(self):
        with tempfile.TemporaryDirectory() as directory:
            report = Path(directory) / "summary.json"
            report.write_text(json.dumps({"mode": "normal-only-with-binaries", "sourceSha256": "same-source",
                                          "binaries": {"MGUI.Core.dll": "old"}}))
            with self.assertRaisesRegex(ValueError, "same current default binaries"):
                stability.load_normal_evidence(report, "same-source", {"MGUI.Core.dll": "new"}, Path(directory))

    def test_soak_checkpoint_mismatch_is_rejected_even_when_final_image_matches(self):
        normal, _ = capture()
        normal["sentinelCheckpoints"] = [{"Frame": 120, "PixelHash": "a" * 64}]
        candidate = copy.deepcopy(normal)
        candidate["sentinelCheckpoints"][0]["PixelHash"] = "b" * 64
        with self.assertRaisesRegex(ValueError, "checkpoint"):
            stability.validate_pair(normal, candidate)

    def test_rejects_no_op_cache_that_never_built_or_excluded_measured_frames(self):
        value, request = capture()
        request["Mode"] = "panel"
        value["resourceChecks"].update(warmStatistics={"Builds": 0, "Hits": 0}, warmBytes=0, warmEntries=0)
        with self.assertRaisesRegex(ValueError, "positive warm"):
            stability.validate_capture(value, request)
        value, request = capture()
        value["resourceChecks"]["IdleChecks"] = 0
        with self.assertRaisesRegex(ValueError, "excluded"):
            stability.validate_capture(value, request)

    def test_rejects_unpresented_or_incomplete_capture(self):
        value, request = capture()
        value["totalPresentedFrames"] -= 1
        with self.assertRaisesRegex(ValueError, "actual-presented"):
            stability.validate_capture(value, request)

    def test_rejects_wrong_clock_before_comparing_pixels(self):
        value, request = capture()
        value["frames"][0]["State"]["ClockTicks"] += 1
        with self.assertRaisesRegex(ValueError, "clock"):
            stability.validate_capture(value, request)

    def test_rejects_resource_over_budget_and_missing_charge(self):
        for resource in ({"RetainedBytes": 101}, {"Entries": 3}, {"Entries": 1}):
            value, request = capture()
            value["frames"][0]["Cache"].update(resource)
            with self.assertRaises(ValueError):
                stability.validate_capture(value, request)

    def test_rejects_leaked_or_undisposed_target_at_teardown(self):
        value, request = capture()
        value["resourceChecks"]["Teardown"]["Allocations"] = 1
        with self.assertRaisesRegex(ValueError, "teardown"):
            stability.validate_capture(value, request)

    def test_rejects_nan_timing_and_composition_target_switches(self):
        value, request = capture()
        value["frames"][0]["Prepare"]["CpuMs"] = float("nan")
        with self.assertRaisesRegex(ValueError, "CPU"):
            stability.validate_capture(value, request)
        value, request = capture()
        value["frames"][0]["Ui"]["TargetSwitches"] = 1
        with self.assertRaisesRegex(ValueError, "switched"):
            stability.validate_capture(value, request)

    def test_rejects_missing_pixel_frame_or_equal_pixels_at_different_state(self):
        normal, _ = capture()
        candidate = copy.deepcopy(normal)
        candidate["frames"][0]["State"]["Clicks"] = 1
        with self.assertRaisesRegex(ValueError, "state/input/clock"):
            stability.validate_pair(normal, candidate)
        candidate = copy.deepcopy(normal)
        candidate["pixelHashes"][0] = "c" * 64
        with self.assertRaisesRegex(ValueError, "RGBA"):
            stability.validate_pair(normal, candidate)
        normal["pixelHashes"] = []
        with self.assertRaisesRegex(ValueError, "Pixel preflight"):
            stability.validate_capture(normal, normal["options"])

    def test_rejects_synthetic_input_that_never_reached_control(self):
        value, request = capture(300, "hover-press")
        with self.assertRaisesRegex(ValueError, "actual hover"):
            stability.validate_capture(value, request)

    def test_rejects_pulse_without_visible_midpoint_or_overlong_scale(self):
        value, request = capture(300, "pulse")
        with self.assertRaisesRegex(ValueError, "midpoint"):
            stability.validate_capture(value, request)
        for frame in value["frames"]:
            frame["State"]["ScoreScale"] = 1.18 if frame["State"]["PulseFrame"] == 12 else 1
        value["frames"][24]["State"]["ScoreScale"] = 1.1
        with self.assertRaisesRegex(ValueError, "restore"):
            stability.validate_capture(value, request)

    def test_historical_trigger_requires_repeated_absolute_and_relative_regression(self):
        baseline = {"workloads": [{"scenario": "hud", "width": 1280, "height": 720, "uiScale": 1,
                    "runs": [{"summary": {"UpdateLayout.CpuMs": {"median": 1}, "UiSubmission.CpuMs": {"median": .1}}}] * 3}]}
        report = {"workloads": [{"scenario": "hud", "width": 1280, "height": 720, "uiScale": 1,
                  "runs": {"normal": [{"summary": {"UpdateLayout.CpuMs": {"median": value},
                                                     "UiPlusPrepare.CpuMs": {"median": .18}}}
                                      for value in (1.2, 1.21, 1.01)]}}]}
        signals = stability.historical_regressions(report, baseline)
        self.assertTrue(signals[0]["investigationRequired"])
        self.assertFalse(signals[1]["investigationRequired"])
        report["workloads"][0]["runs"]["normal"][1]["summary"]["UpdateLayout.CpuMs"]["median"] = 1.01
        self.assertFalse(stability.historical_regressions(report, baseline)[0]["investigationRequired"])


if __name__ == "__main__":
    unittest.main()
