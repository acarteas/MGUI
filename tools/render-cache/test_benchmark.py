import copy
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

import benchmark


class StatisticsTests(unittest.TestCase):
    def test_nearest_rank_p95_and_even_median_have_known_answers(self):
        result = benchmark.distribution(list(range(20, 0, -1)))
        self.assertEqual(result, {"median": 10.5, "p95": 19, "min": 1, "max": 20, "count": 20})

    def test_tail_is_preserved_and_invalid_measurements_rejected(self):
        result = benchmark.distribution([2] * 19 + [500])
        self.assertEqual(result["median"], 2)
        self.assertEqual(result["p95"], 2)
        self.assertEqual(result["max"], 500)
        for values in ([], [-1], [float("nan")], [float("inf")]):
            with self.assertRaises(ValueError):
                benchmark.distribution(values)


class CaptureContractTests(unittest.TestCase):
    def setUp(self):
        self.capture = {
            "schemaVersion": 1, "mode": "normal", "Scenario": "animated-score", "Width": 1920,
            "Height": 1080, "uiScale": 1.5, "Seed": 1729, "warmupFrames": 120,
            "measuredPresentedFrames": 3, "totalPresentedFrames": 124,
            "gpuTimeMs": None, "gpuTimeUnavailableReason": "unsupported",
            "nativePixelValidation": {"worldSentinelsMatch": True, "changedUiPixels": 2000,
                                      "brightTextPixels": 200, "captureFrame": 123},
            "frames": []}
        for frame in range(120, 123):
            sample = {"Frame": frame, "State": {"SimulationFrame": frame, "WorldOffset": frame % 64,
                                                "Score": str(frame), "ScrollOffset": 0}}
            sample.update({phase: {"CpuMs": 1.25, "AllocatedBytes": 16} for phase in benchmark.PHASES})
            sample.update({"World": {"DrawCalls": 1, "Primitives": 1000, "Sprites": 500, "TargetSwitches": 0},
                           "Ui": {"DrawCalls": 4, "Primitives": 200, "Sprites": 100, "TargetSwitches": 0},
                           "Total": {"DrawCalls": 5, "Primitives": 1200, "Sprites": 600, "TargetSwitches": 0}})
            self.capture["frames"].append(sample)

    def validate(self, capture):
        return benchmark.validate_capture(capture, "animated-score", 1920, 1080, 1.5, 1729, 120, 3)

    def test_valid_capture_has_stable_state_fingerprint_independent_of_timing(self):
        expected = self.validate(self.capture)
        changed = copy.deepcopy(self.capture)
        changed["frames"][0]["UiSubmission"]["CpuMs"] = 20
        self.assertEqual(expected, self.validate(changed))
        changed["frames"][0]["State"]["Score"] = "different"
        self.assertNotEqual(expected, self.validate(changed))

    def test_rejects_incomplete_wrong_metadata_and_unproven_graphics(self):
        mutations = [lambda c: c.update(Seed=99), lambda c: c.update(warmupFrames=0),
                     lambda c: c.update(totalPresentedFrames=123), lambda c: c.update(uiScale=1),
                     lambda c: c["frames"].pop(),
                     lambda c: c["frames"][0]["Total"].update(DrawCalls=0),
                     lambda c: c["frames"][0].update(Frame=119),
                     lambda c: c["nativePixelValidation"].update(worldSentinelsMatch=False),
                     lambda c: c["nativePixelValidation"].update(brightTextPixels=0),
                     lambda c: c.update(gpuTimeMs=1),
                     lambda c: [f["State"].update(Score="frozen") for f in c["frames"]]]
        for mutate in mutations:
            capture = copy.deepcopy(self.capture)
            mutate(capture)
            with self.assertRaises(ValueError):
                self.validate(capture)

    def test_phase_and_world_counter_summaries_remain_separate(self):
        summary = benchmark.summarize(self.capture["frames"])
        self.assertEqual(summary["World.Primitives"]["median"], 1000)
        self.assertEqual(summary["Ui.Primitives"]["median"], 200)
        self.assertEqual(summary["Present.CpuMs"]["count"], 3)
        self.assertEqual(summary["UpdateLayout.AllocatedBytes"]["median"], 16)


class RunnerSafetyTests(unittest.TestCase):
    def test_existing_output_is_not_overwritten(self):
        with tempfile.TemporaryDirectory() as directory:
            sentinel = Path(directory) / "user-file"
            sentinel.write_text("keep")
            result = subprocess.run([sys.executable, str(Path(benchmark.__file__)), "--output", directory, "--skip-build"],
                                    capture_output=True, text=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertEqual(sentinel.read_text(), "keep")
            self.assertEqual(list(Path(directory).iterdir()), [sentinel])

    def test_native_process_timeout_is_reported(self):
        with self.assertRaises(subprocess.TimeoutExpired):
            benchmark.run([sys.executable, "-c", "import time; time.sleep(10)"], timeout=.05)


if __name__ == "__main__":
    unittest.main()
