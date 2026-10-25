#!/usr/bin/env python3
"""Run an already built native test host under an owned display and graphics lock. Never builds while holding graphics."""
import fcntl
import os
import re
from pathlib import Path
import select
import shutil
import subprocess
import sys
import tempfile


def invocation(arguments):
    """Keep historical calls unchanged; an explicit display option is consumed by this helper only."""
    if not arguments:
        raise ValueError("Usage: test-native.py host.dll [--display-size=WIDTHxHEIGHT] [host arguments]")
    host = Path(arguments[0]).resolve()
    forwarded = []
    requested = None
    for argument in arguments[1:]:
        if argument.startswith("--display-size="):
            if requested is not None:
                raise ValueError("Only one private display size may be supplied")
            requested = argument.removeprefix("--display-size=")
        else:
            forwarded.append(argument)
    if requested is None and len(forwarded) == 4 and forwarded[0] == "--matrix":
        requested = f"{forwarded[1]}x{forwarded[2]}"
    size = requested if requested is not None else ("1280x900" if forwarded == ["--sample"] else "640x480")
    match = re.fullmatch(r"([1-9][0-9]{2,3})x([1-9][0-9]{2,3})", size)
    if not match or not (320 <= int(match[1]) <= 4096 and 240 <= int(match[2]) <= 4096):
        raise ValueError("Private display size must be WIDTHxHEIGHT in the range 320x240 through 4096x4096")
    if forwarded and forwarded[0] == "--matrix" and (len(forwarded) != 4 or size != f"{forwarded[1]}x{forwarded[2]}"):
        raise ValueError("Matrix private display must exactly match its requested physical surface")
    return host, forwarded, size + "x24"


def main():
    host, arguments, screen = invocation(sys.argv[1:])
    if not host.is_file():
        raise RuntimeError(f"Build the current native test host first: {host}")
    with open("/tmp/mgui-render-cache-graphics.lock", "a") as lock, tempfile.TemporaryFile() as diagnostics:
        fcntl.flock(lock, fcntl.LOCK_EX)

        server = subprocess.Popen(["Xvfb", f":{100 + os.getpid() % 30000}", "-displayfd", "1", "-screen", "0", screen, "-nolisten", "tcp"],
                                  stdout=subprocess.PIPE, stderr=diagnostics, text=True, start_new_session=True)
        try:
            if not select.select([server.stdout], [], [], 15)[0]:
                raise RuntimeError("Owned Xvfb did not become ready")
            display = server.stdout.readline().strip()
            if not display.isdigit() or display == "0" or server.poll() is not None:
                raise RuntimeError("Owned Xvfb failed to allocate a private display")
            env = dict(os.environ, DISPLAY=f":{display}", ALSOFT_DRIVERS="null", LIBGL_ALWAYS_SOFTWARE="1")
            if shutil.which("glxinfo"):
                details = subprocess.run(["glxinfo", "-B"], env=env, text=True, capture_output=True, timeout=15)
                print(details.stdout, file=sys.stderr, flush=True)
            result = subprocess.run(["dotnet", str(host), *arguments], cwd=host.parent, env=env, text=True,
                                    capture_output=True, timeout=90)
            print(result.stdout, end="", flush=True)
            print(result.stderr, end="", file=sys.stderr, flush=True)
            return result.returncode
        finally:
            if server.poll() is None:
                server.terminate()
            try:
                server.communicate(timeout=5)
            except subprocess.TimeoutExpired:
                server.kill()
                server.communicate()


if __name__ == "__main__":
    sys.exit(main())
