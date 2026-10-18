#!/usr/bin/env python3
"""Run an already built native test host under an owned display and graphics lock. Never builds while holding graphics."""
import fcntl
import os
from pathlib import Path
import select
import shutil
import subprocess
import sys
import tempfile


def main():
    host = Path(sys.argv[1]).resolve()
    if not host.is_file():
        raise RuntimeError(f"Build the current native test host first: {host}")
    with open("/tmp/mgui-render-cache-graphics.lock", "a") as lock, tempfile.TemporaryFile() as diagnostics:
        fcntl.flock(lock, fcntl.LOCK_EX)
        server = subprocess.Popen(["Xvfb", f":{100 + os.getpid() % 30000}", "-displayfd", "1", "-screen", "0", "640x480x24", "-nolisten", "tcp"],
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
            result = subprocess.run(["dotnet", str(host), *sys.argv[2:]], cwd=host.parent, env=env, text=True,
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
