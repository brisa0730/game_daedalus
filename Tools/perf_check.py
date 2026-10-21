"""WebGL performance check for daedalus (scaffold plan, Step 6).

Usage:
    python Tools/perf_check.py [--skip-build]

Builds Assets/Dev/PerfCheck as a release WebGL build with Unity 6000.6.7f1, serves it on
localhost, opens it in Chrome, and waits for the page to post its measurements and a
screenshot. Writes TestResults/perf-check.json and TestResults/perf-check.png.

Gate: frameIntervalP95Ms <= 20 (primary), fpsMean >= 58, sweepTiles >= 500.
The dev project must be closed in the Unity Editor while this runs.
"""

import argparse
import functools
import http.server
import json
import os
import shutil
import subprocess
import sys
import tempfile
import threading
import time
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
RESULTS = REPO / "TestResults"
BUILD = RESULTS / "perf-build"
EDITOR_VERSION = "6000.6.7f1"
CHROME = Path(os.environ.get("CHROME", r"C:\Program Files\Google\Chrome\Application\chrome.exe"))
BUILD_TIMEOUT_SECONDS = 60 * 60
PAGE_TIMEOUT_SECONDS = 120
GATE_P95_MS = 20.0
GATE_FPS = 58.0
GATE_SWEEP_TILES = 500.0


def unity_exe():
    root = Path(os.environ.get("UNITY_EDITORS", r"C:\Program Files\Unity\Hub\Editor"))
    exe = root / EDITOR_VERSION / "Editor" / "Unity.exe"
    if not exe.exists():
        sys.exit(f"Unity {EDITOR_VERSION} not found at {exe}")
    return exe


def build():
    # Start from an empty folder: Unity keeps unchanged files (and their old timestamps),
    # so only an emptied folder proves the build output is this run's.
    shutil.rmtree(BUILD, ignore_errors=True)
    if BUILD.exists():
        sys.exit(f"FAIL  could not empty {BUILD} (a file is in use?)")
    started = time.time()
    log = RESULTS / "perf-build.log"
    print(f"Building WebGL with Unity {EDITOR_VERSION} (several minutes)...")
    code = subprocess.run(
        [str(unity_exe()), "-batchmode", "-projectPath", str(REPO), "-buildTarget", "WebGL",
         "-executeMethod", "Orcapafea.Daedalus.Dev.PerfCheckBuild.Build", "-perfOutput", str(BUILD),
         "-logFile", str(log)],
        timeout=BUILD_TIMEOUT_SECONDS).returncode
    index = BUILD / "index.html"
    if code != 0:
        sys.exit(f"FAIL  WebGL build: Unity exited with code {code} (see {log})")
    if not index.exists() or index.stat().st_mtime < started:
        sys.exit(f"FAIL  WebGL build: no fresh {index} (see {log})")
    print(f"Build done in {time.time() - started:.0f}s")


class Received:
    def __init__(self):
        self.result = None
        self.screenshot = None
        self.done = threading.Event()


class Handler(http.server.SimpleHTTPRequestHandler):
    extensions_map = {
        **http.server.SimpleHTTPRequestHandler.extensions_map,
        ".js": "application/javascript",
        ".wasm": "application/wasm",
        ".data": "application/octet-stream",
        ".json": "application/json",
    }

    def __init__(self, *args, received, **kwargs):
        self.received = received
        super().__init__(*args, **kwargs)

    def do_POST(self):
        body = self.rfile.read(int(self.headers.get("Content-Length", "0")))
        if self.path.endswith("/result"):
            self.received.result = json.loads(body)
        elif self.path.endswith("/screenshot"):
            self.received.screenshot = body
        else:
            self.send_error(404)
            return
        self.send_response(204)
        self.end_headers()
        if self.received.result is not None and self.received.screenshot is not None:
            self.received.done.set()

    def log_message(self, *args):
        pass


def measure():
    if not (BUILD / "index.html").exists():
        sys.exit(f"No build at {BUILD}; run without --skip-build")
    if not CHROME.exists():
        sys.exit(f"Chrome not found at {CHROME} (set CHROME)")

    received = Received()
    handler = functools.partial(Handler, directory=str(BUILD), received=received)
    server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), handler)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    url = f"http://127.0.0.1:{server.server_address[1]}/index.html"

    profile = tempfile.mkdtemp(prefix="daedalus-perf-chrome-")
    chrome = subprocess.Popen([
        str(CHROME), f"--user-data-dir={profile}", "--new-window", "--window-size=1366,768",
        "--no-first-run", "--no-default-browser-check",
        "--disable-background-timer-throttling", "--disable-renderer-backgrounding",
        "--disable-backgrounding-occluded-windows", url])
    print(f"Measuring in Chrome at {url} (about 30 s)...")
    try:
        arrived = received.done.wait(PAGE_TIMEOUT_SECONDS)
    finally:
        subprocess.run(["taskkill", "/F", "/T", "/PID", str(chrome.pid)], capture_output=True)
        server.shutdown()
        shutil.rmtree(profile, ignore_errors=True)
    if not arrived:
        sys.exit(f"FAIL  no result from the page within {PAGE_TIMEOUT_SECONDS}s")
    return received


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--skip-build", action="store_true", help="reuse the existing build")
    args = parser.parse_args()

    RESULTS.mkdir(exist_ok=True)
    if not args.skip_build:
        build()
    received = measure()

    result = received.result
    checks = {
        "frameIntervalP95Ms <= 20": result["frameIntervalP95Ms"] <= GATE_P95_MS,
        "fpsMean >= 58": result["fpsMean"] >= GATE_FPS,
        "sweepTiles >= 500": result["sweepTiles"] >= GATE_SWEEP_TILES,
    }
    result["gate"] = checks
    result["passed"] = all(checks.values())
    (RESULTS / "perf-check.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    (RESULTS / "perf-check.png").write_bytes(received.screenshot)

    print(f"fpsMean {result['fpsMean']:.1f}, p95 {result['frameIntervalP95Ms']:.1f} ms, "
          f"refresh {result['refreshRate']:.0f} Hz, {result['frames']} frames, "
          f"{result['tiles']} tiles, {result['entities']} entities, sweep {result['sweepTiles']:.0f} tiles, "
          f"{result['screenWidth']}x{result['screenHeight']}")
    for name, ok in checks.items():
        print(f"  {'ok  ' if ok else 'FAIL'} {name}")
    print("PASS" if result["passed"] else "FAIL", "-> TestResults/perf-check.json, perf-check.png")
    sys.exit(0 if result["passed"] else 1)


if __name__ == "__main__":
    main()
