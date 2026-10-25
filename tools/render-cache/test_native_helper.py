"""Pure argument-contract checks; these never start a display or build native code."""
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("native_helper", Path(__file__).with_name("test-native.py"))
helper = importlib.util.module_from_spec(spec)
spec.loader.exec_module(helper)


class NativeInvocationTests(unittest.TestCase):
    def test_old_calls_keep_their_display_and_forwarded_arguments(self):
        for arguments, expected in (([], "640x480x24"), (["--composition"], "640x480x24"),
                                    (["--sample"], "1280x900x24")):
            _, forwarded, screen = helper.invocation(["fixture.dll", *arguments])
            self.assertEqual(arguments, forwarded)
            self.assertEqual(expected, screen)

    def test_matrix_sizes_its_owned_display_to_the_surface(self):
        for width, height in ((1280, 720), (1920, 1080)):
            for scale in ("1", "1.5", "2"):
                arguments = ["--matrix", str(width), str(height), scale]
                _, forwarded, screen = helper.invocation(["fixture.dll", *arguments])
                self.assertEqual(arguments, forwarded)
                self.assertEqual(f"{width}x{height}x24", screen)

    def test_explicit_size_is_validated_and_not_forwarded_to_dotnet(self):
        _, forwarded, screen = helper.invocation(["fixture.dll", "--display-size=1920x1080", "--composition"])
        self.assertEqual(["--composition"], forwarded)
        self.assertEqual("1920x1080x24", screen)
        for size in ("", "0x0", "319x240", "320x239", "4097x1080", "1280x720x24", "-1280x720", "foo"):
            with self.assertRaises(ValueError):
                helper.invocation(["fixture.dll", "--display-size=" + size])
        with self.assertRaises(ValueError):
            helper.invocation(["fixture.dll", "--display-size=1280x720", "--display-size=1280x720"])
        with self.assertRaises(ValueError):
            helper.invocation(["fixture.dll", "--display-size=640x480", "--matrix", "1280", "720", "1"])


if __name__ == "__main__":
    unittest.main()
