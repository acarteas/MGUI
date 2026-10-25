import copy
import unittest
import compare_cache


class CacheComparisonTests(unittest.TestCase):
    def test_benefit_requires_twenty_percent_noise_separation_and_p95(self):
        reference = [{"median": 1, "p95": 1.1}] * 3
        self.assertTrue(compare_cache.benefit(reference, [{"median": .7, "p95": 1.1}] * 3)["passesStableBenefitGate"])
        self.assertFalse(compare_cache.benefit(reference, [{"median": .85, "p95": 1.1}] * 3)["passesStableBenefitGate"])
        self.assertFalse(compare_cache.benefit(reference, [{"median": .7, "p95": 1.3}] * 3)["passesStableBenefitGate"])
        noisy = [{"median": m, "p95": 1.1} for m in (.9, 1, 1.1)]
        self.assertFalse(compare_cache.benefit(noisy, [{"median": .7, "p95": 1.1}] * 3)["passesStableBenefitGate"])

    def test_cold_cost_includes_first_direct_draw_and_next_capture(self):
        frames = [{"UiSubmission": {"CpuMs": 3}, "Prepare": {"CpuMs": 0}},
                  {"UiSubmission": {"CpuMs": 1}, "Prepare": {"CpuMs": 7}},
                  {"UiSubmission": {"CpuMs": 99}, "Prepare": {"CpuMs": 99}}]
        self.assertEqual(11, compare_cache.cold_cost({"coldFrames": frames}))

    def test_cold_window_includes_delayed_first_capture(self):
        frames = [{"UiSubmission": {"CpuMs": ui}, "Prepare": {"CpuMs": prepare}, "Cache": {"Builds": builds}}
                  for ui, prepare, builds in ((3, 0, 0), (4, 0, 0), (1, 7, 1), (99, 99, 1))]
        self.assertEqual((3, 2), compare_cache.cold_window({"coldFrames": frames}))
        self.assertEqual(15, compare_cache.cold_cost({"coldFrames": frames}))

    def test_exact_pixels_and_live_sibling_no_rebuild_are_required(self):
        frame = {"State": {"frame": 1}, "World": {"DrawCalls": 1}, "Ui": {"DrawCalls": 3, "TargetSwitches": 0}, "Cache": {"Builds": 0}}
        capture = {"Scenario": "compare-mixed", "screenshotSha256": "exact", "frames": [frame, copy.deepcopy(frame)]}
        captures = {mode: copy.deepcopy(capture) for mode in compare_cache.MODES}
        for mode in ("background", "panel"):
            for f in captures[mode]["frames"]:
                f["Cache"].update(Builds=1, Hits=1, Entries=1, RetainedBytes=4)
        compare_cache.validate_equivalence(captures)
        for mutate in (lambda c: c["panel"].update(screenshotSha256="different"),
                       lambda c: c["panel"]["frames"][0]["Cache"].update(Hits=0),
                       lambda c: c["panel"]["frames"][1]["Cache"].update(Builds=2),
                       lambda c: c["normal-batching"]["frames"][0]["Ui"].update(DrawCalls=4)):
            changed = copy.deepcopy(captures)
            mutate(changed)
            with self.assertRaises(ValueError):
                compare_cache.validate_equivalence(changed)


if __name__ == "__main__":
    unittest.main()
