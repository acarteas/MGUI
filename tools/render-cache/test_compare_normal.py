import copy
import unittest

import compare_normal


class MatchedComparisonTests(unittest.TestCase):
    def test_noise_threshold_uses_reference_repeat_spread(self):
        normal = [{"median": value} for value in (.10, .11, .12)]
        disabled = [{"median": value} for value in (.139, .149, .159)]
        result = compare_normal.gate(normal, disabled, "median")
        self.assertAlmostEqual(.04, result["threshold"])
        self.assertTrue(result["passes"])
        self.assertFalse(compare_normal.gate(normal, [{"median": .151}] * 3, "median")["passes"])

    def test_subtotal_percentiles_sum_each_sample_before_summarizing(self):
        frames = []
        for ui, prepare in ((1, 9), (5, 2), (9, 1)):
            frame = {phase: {"CpuMs": 0, "AllocatedBytes": 0} for phase in compare_normal.PHASES}
            frame["UiSubmission"]["CpuMs"] = ui
            frame["Prepare"]["CpuMs"] = prepare
            frame.update({part: {counter: 0 for counter in compare_normal.benchmark.COUNTERS} for part in ("World", "Ui", "Total")})
            frames.append(frame)
        summary = compare_normal.summarize(frames)
        self.assertEqual(10, summary["UiPlusPrepare.CpuMs"]["median"])
        self.assertEqual(10, summary["InstrumentedSubtotal.CpuMs"]["median"])

    def test_historical_baseline_rejects_added_flushes_or_changed_output(self):
        capture = {"screenshotSha256": "same", "frames": [{"State": {"frame": 1},
                   "World": {"DrawCalls": 1}, "Ui": {"DrawCalls": 4}, "Total": {"DrawCalls": 5}, "UiSubmission": {"AllocatedBytes": 100}}]}
        compare_normal.validate_baseline(capture, copy.deepcopy(capture))
        for mutate in (lambda c: c.update(screenshotSha256="changed"),
                       lambda c: c["frames"][0]["Ui"].update(DrawCalls=5),
                       lambda c: c["frames"][0]["UiSubmission"].update(AllocatedBytes=108)):
            changed = copy.deepcopy(capture)
            mutate(changed)
            with self.assertRaises(ValueError):
                compare_normal.validate_baseline(changed, capture)

    def test_pair_requires_exact_pixels_state_graphics_and_zero_cache_work(self):
        frame = {"State": {"frame": 1}, "Cache": {"Allocations": 0}, "Prepare": {"AllocatedBytes": 0},
                 "World": {"DrawCalls": 1}, "Ui": {"DrawCalls": 2, "TargetSwitches": 0}, "Total": {"DrawCalls": 3}}
        capture = {"screenshotSha256": "same", "frames": [frame]}
        compare_normal.validate_pair(capture, copy.deepcopy(capture))
        for mutate in (lambda c: c.update(screenshotSha256="other"),
                       lambda c: c["frames"][0]["State"].update(frame=2),
                       lambda c: c["frames"][0]["Ui"].update(DrawCalls=3),
                       lambda c: c["frames"][0]["Cache"].update(Allocations=1),
                       lambda c: c["frames"][0]["Prepare"].update(AllocatedBytes=1)):
            other = copy.deepcopy(capture)
            mutate(other)
            with self.assertRaises(ValueError):
                compare_normal.validate_pair(capture, other)


if __name__ == "__main__":
    unittest.main()
