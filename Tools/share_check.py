"""Check Daedalus -> Share my game end to end: a batch Share build of the trap bench, judged by the gallery's own check.

Usage:
    python Tools/share_check.py [--editor 6000.0|6000.6|all] [--no-gallery-check] [--probe] [--project PATH --scene PATH]

Per editor it runs the menu's own build core (Orcapafea.Daedalus.Editor.ShareMyGame.BuildFromCommandLine) on
Assets/Dev/TrapTest with a hostile title, then checks:
  - Unity exited 0 and the zip is fresh and at most 50,000,000 bytes;
  - the zip holds exactly the six Share zip entries, Deflated (what Mono writes), daedalus.json without a BOM;
  - the gallery's check accepts it (cargo run --example check_share_zip), with the editor's raw title equal to the
    gallery's clean_label of the same input; skipped with --no-gallery-check (a machine without cargo);
  - the WebGL Player Settings and the Build Settings scene list are back exactly as they were (the run starts from the
    opposite of every pinned value, so a broken restore shows; the settings file is put back afterwards);
  - the Korean gallery labels in ShareTexts.cs appear verbatim in the gallery's strings.rs, and the editor's copies of
    the gallery limits match config.rs.
--probe instead proves the fail-closed wire: a build callback that logs an exception (the build still succeeds) is
dropped into the project for one run; Share must refuse with its exception text and make no zip. Then it is removed.
6000.6 uses the dev project; 6000.0 the compat project (Tools/compat/6000.0), with the trap bench copied in.
--project PATH --scene PATH shares a student-shaped project instead (a scratch copy in Tools/compat/, pointed at today's
package): the committed project is never opened, and the run fails if Tools/fixtures/0.1.0 changed.
Writes TestResults/share-check/<editor>.zip and .log, and TestResults/<unity version>-share-check-screenshot.png.
"""

import argparse
import json
import os
import re
import shutil
import signal
import subprocess
import sys
import time
import zipfile
from pathlib import Path

sys.dont_write_bytecode = True  # importing unity_tests must not leave Tools/__pycache__ behind
sys.path.insert(0, str(Path(__file__).resolve().parent))
from unity_tests import EDITORS, REPO, RESULTS, editor_exe, ensure_compat_project, looks_locked  # noqa: E402
from install_check import assert_fixture_untouched, copy_fixture  # noqa: E402

OUT = RESULTS / "share-check"
GALLERY = REPO / "Deploy" / "gallery"
SHARE_TEXTS = REPO / "Packages" / "com.orcapafea.daedalus" / "Editor" / "ShareTexts.cs"
STRINGS_RS = GALLERY / "src" / "strings.rs"
BENCH = "Assets/Dev/TrapTest"
COMPAT_BENCH = "Assets/ShareCheck/TrapTest"
MAX_ZIP_BYTES = 50_000_000
BUILD_TIMEOUT_SECONDS = 60 * 60
ENTRIES = ["daedalus.json", "screenshot.png", "Build/game.loader.js", "Build/game.data.br",
           "Build/game.framework.js.br", "Build/game.wasm.br"]
# Each pinned setting with a value opposite to the pin (Gzip, fallback on, caching on, hashes on, threads on).
SEEDS = {"webGLCompressionFormat": "1", "webGLDecompressionFallback": "1", "webGLDataCaching": "1",
         "webGLNameFilesAsHashes": "1", "webGLThreadsSupport": "1"}
SETTING_KEYS = list(SEEDS)
CONFIG_RS = GALLERY / "src" / "config.rs"
EDITOR = REPO / "Packages" / "com.orcapafea.daedalus" / "Editor"
PROBE = """using System;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Written and removed by Tools/share_check.py --probe: logs an exception during a build that still succeeds (ISS-001's shape).
public class ShareCheckProbe : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) { Debug.LogException(new Exception("share-check probe")); }
}
"""
# Korean, emoji, a tab, NBSP, an ideographic space, a bidi control, leading spaces and more than 40 characters.
HOSTILE_TITLE = "  함정\t지옥 😀　‮ 트롤   레벨 " + "가" * 40


class Fail(Exception):
    pass


def snapshot(project):
    settings = (project / "ProjectSettings" / "ProjectSettings.asset").read_text(encoding="utf-8")
    values = {}
    for key in SETTING_KEYS:
        match = re.search(rf"^\s*{key}: (.*)$", settings, re.MULTILINE)
        values[key] = match.group(1).strip() if match else None
    build = (project / "ProjectSettings" / "EditorBuildSettings.asset").read_text(encoding="utf-8")
    # The scene list up to the next key at the same indent (entries are indented further).
    scenes = re.search(r"^  m_Scenes:.*?(?=^  m_|\Z)", build, re.MULTILINE | re.DOTALL)
    values["m_Scenes"] = scenes.group(0).strip() if scenes else None
    return values


def seed(project):
    """Sets every pinned setting to the opposite of the pin; returns the file's original text to put back."""
    path = project / "ProjectSettings" / "ProjectSettings.asset"
    original = path.read_text(encoding="utf-8")
    seeded = original
    for key, value in SEEDS.items():
        seeded, count = re.subn(rf"^(\s*{key}): .*$", rf"\g<1>: {value}", seeded, flags=re.MULTILINE)
        if count != 1:
            raise Fail(f"{key} not found once in {path}")
    path.write_text(seeded, encoding="utf-8", newline="")
    return path, original


def run_unity(command):
    """Like unity_tests.run_unity, with a build-sized timeout (a cold WebGL build can pass 30 minutes)."""
    process = subprocess.Popen(command, start_new_session=sys.platform != "win32")
    try:
        return process.wait(timeout=BUILD_TIMEOUT_SECONDS)
    except subprocess.TimeoutExpired:
        if sys.platform == "win32":
            subprocess.run(["taskkill", "/F", "/T", "/PID", str(process.pid)], capture_output=True)
        else:
            os.killpg(process.pid, signal.SIGKILL)
        raise Fail(f"Unity didn't finish within {BUILD_TIMEOUT_SECONDS // 60} minutes")


def prepare(editor_key, other_project=None, other_scene=None):
    if other_project:
        return copy_fixture(Path(other_project).resolve(), f"share-{editor_key}"), other_scene
    if editor_key == "6000.6":
        return REPO, f"{BENCH}/TrapTest.unity"
    project = ensure_compat_project(editor_key)
    target = project / COMPAT_BENCH
    if target.exists():
        shutil.rmtree(target)
    shutil.copytree(REPO / BENCH, target)  # .meta files too, so the scene's GUIDs resolve
    return project, f"{COMPAT_BENCH}/TrapTest.unity"


def check_zip(zip_path):
    with zipfile.ZipFile(zip_path) as archive:
        infos = archive.infolist()
        names = [info.filename for info in infos]
        if names != ENTRIES:
            raise Fail(f"zip entries are {names}, expected {ENTRIES}")
        for info in infos:
            if info.compress_type != zipfile.ZIP_DEFLATED:
                raise Fail(f"{info.filename} uses compression method {info.compress_type}, expected Deflated (8)")
        metadata = archive.read("daedalus.json")
        if metadata.startswith(b"\xef\xbb\xbf"):
            raise Fail("daedalus.json starts with a BOM")
        parsed = json.loads(metadata.decode("utf-8"))
        if parsed.get("format") != 1 or not isinstance(parsed.get("title"), str):
            raise Fail(f"daedalus.json is {parsed}")
        return archive.read("screenshot.png"), parsed["title"]


def gallery_check(zip_path, title_file):
    result = subprocess.run(["cargo", "run", "--quiet", "--example", "check_share_zip", "--", str(zip_path),
                             "--expect-title-file", str(title_file)], cwd=GALLERY, capture_output=True, text=True,
                            encoding="utf-8")
    line = result.stdout.strip().splitlines()[-1] if result.stdout.strip() else ""
    try:
        report = json.loads(line)
    except json.JSONDecodeError:
        raise Fail(f"check_share_zip printed no report (exit {result.returncode}): {result.stderr.strip()[-500:]}")
    if result.returncode != 0 or not report.get("ok"):
        raise Fail(f"the gallery rejected the zip: {line}")
    if report.get("build_name") != "game":
        raise Fail(f"build name is {report.get('build_name')}, expected game")
    return report


def const(path, name):
    match = re.search(rf"const \w+ {name} = (\d+)", path.read_text(encoding="utf-8"))
    if not match:
        raise Fail(f"{name} not found in {path.name}")
    return int(match.group(1))


def check_limits():
    config = CONFIG_RS.read_text(encoding="utf-8")

    def gallery(name):
        match = re.search(rf"\b{name}: ([\d_]+),", config)
        if not match:
            raise Fail(f"{name} not found in config.rs")
        return int(match.group(1).replace("_", ""))

    pairs = [
        ("max_upload_bytes", gallery("max_upload_bytes"), const(EDITOR / "ShareBuild.cs", "MaxZipBytes")),
        ("max_screenshot_bytes", gallery("max_screenshot_bytes"), const(EDITOR / "ShareScreenshot.cs", "MaxBytes")),
        ("title_max_chars", gallery("title_max_chars"), const(EDITOR / "ShareTitle.cs", "MaxChars")),
    ]
    for name, rust, csharp in pairs:
        if rust != csharp:
            raise Fail(f"config.rs {name} is {rust}, the editor's copy is {csharp}")
    first = re.search(r"Sizes = \{ new Vector2Int\((\d+), (\d+)\)", (EDITOR / "ShareScreenshot.cs").read_text(encoding="utf-8"))
    if not first or (int(first.group(1)), int(first.group(2))) != (gallery("max_screenshot_width"), gallery("max_screenshot_height")):
        raise Fail("the screenshot's first size doesn't match config.rs max_screenshot_width/height")
    if f"up to {gallery('max_upload_bytes') // 1_000_000} MB" not in SHARE_TEXTS.read_text(encoding="utf-8"):
        raise Fail("ShareTexts.TooBig doesn't name the gallery's limit in MB")


def check_labels():
    texts = SHARE_TEXTS.read_text(encoding="utf-8")
    strings = STRINGS_RS.read_text(encoding="utf-8")
    labels = re.findall(r'const string Gallery(?:Upload|UpdateMyGame|NewGame) = "([^"]+)"', texts)
    if len(labels) != 3:
        raise Fail(f"expected 3 Korean gallery labels in ShareTexts.cs, found {labels}")
    for label in labels:
        if f'"{label}"' not in strings:
            raise Fail(f"ShareTexts label {label} is not in strings.rs")
    if 'NAV_UPLOAD: &str = "업로드"' not in strings:
        raise Fail("strings.rs NAV_UPLOAD is no longer 업로드")


def check_editor(editor_key, gallery, probe=False, other_project=None, other_scene=None):
    version = EDITORS[editor_key]
    exe = editor_exe(version)
    project, scene = prepare(editor_key, other_project, other_scene)
    probe_dir = project / "Assets" / "ShareCheckProbe"
    if probe:
        (probe_dir / "Editor").mkdir(parents=True, exist_ok=True)
        (probe_dir / "Editor" / "ShareCheckProbe.cs").write_text(PROBE, encoding="utf-8")
    settings_path, settings_text = seed(project)
    try:
        return run_share(editor_key, version, exe, project, scene, gallery, probe)
    finally:
        settings_path.write_text(settings_text, encoding="utf-8", newline="")
        if probe:
            shutil.rmtree(probe_dir, ignore_errors=True)
            Path(str(probe_dir) + ".meta").unlink(missing_ok=True)


def run_share(editor_key, version, exe, project, scene, gallery, probe):
    OUT.mkdir(parents=True, exist_ok=True)
    zip_path = OUT / f"{editor_key}.zip"
    log_path = OUT / f"{editor_key}.log"
    title_file = OUT / "title.txt"
    title_file.write_text(HOSTILE_TITLE, encoding="utf-8")
    for stale in (zip_path, log_path):
        if stale.exists():
            stale.unlink()

    before = snapshot(project)
    started = time.time()
    code = run_unity([str(exe), "-batchmode", "-projectPath", str(project),
                      "-executeMethod", "Orcapafea.Daedalus.Editor.ShareMyGame.BuildFromCommandLine",
                      "-shareScene", scene, "-shareTitleFile", str(title_file), "-shareOutput", str(zip_path),
                      "-logFile", str(log_path)])
    seconds = time.time() - started
    log = log_path.read_text(encoding="utf-8", errors="replace") if log_path.exists() else ""
    if looks_locked(log, project):
        raise Fail("the project is open in another Unity; close it and run again")
    share_line = next((line for line in log.splitlines() if line.startswith("ShareMyGame: ")), "no ShareMyGame line")

    after = snapshot(project)
    changed = [key for key in before if before[key] != after[key]]
    if changed:
        raise Fail(f"not restored after the share: {changed} ({share_line})")
    if probe:
        exception_text = re.search(r'BuildException = "([^"]+)"', SHARE_TEXTS.read_text(encoding="utf-8")).group(1)
        if "share-check probe" not in log:
            raise Fail("the probe didn't run")
        if code == 0 or exception_text not in share_line:
            raise Fail(f"Share didn't refuse with its exception text: {share_line}")
        if zip_path.exists():
            raise Fail("a zip was made despite the exception")
        return f"refused as expected in {seconds:.0f}s: {share_line}"
    if code != 0:
        raise Fail(f"Unity exited {code}: {share_line}")

    if not zip_path.exists() or zip_path.stat().st_mtime < started:
        raise Fail("no fresh zip")
    zip_bytes = zip_path.stat().st_size
    if zip_bytes > MAX_ZIP_BYTES:
        raise Fail(f"zip is {zip_bytes} bytes")
    screenshot, title = check_zip(zip_path)
    (RESULTS / f"{version}-share-check-screenshot.png").write_bytes(screenshot)
    if gallery:
        report = gallery_check(zip_path, title_file)
        title = report["title"]
    return f"build {seconds:.0f}s, zip {zip_bytes} bytes, title {title!r}"


def main():
    # The hostile title holds an emoji; a Korean Windows console (cp949) can't print it.
    sys.stdout.reconfigure(errors="backslashreplace")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--editor", choices=["6000.0", "6000.6", "all"], default="6000.6")
    parser.add_argument("--no-gallery-check", action="store_true", help="skip the cargo check (a machine without cargo)")
    parser.add_argument("--probe", action="store_true", help="prove an exception during the build makes no zip")
    parser.add_argument("--project", help="share a scratch copy of this project instead of the trap bench")
    parser.add_argument("--scene", help="the scene to share in --project, relative to it")
    args = parser.parse_args()
    if bool(args.project) != bool(args.scene):
        parser.error("--project and --scene go together")
    editors = ["6000.6", "6000.0"] if args.editor == "all" else [args.editor]

    failed = False
    try:
        check_labels()
        check_limits()
    except Fail as error:
        print(f"FAIL  contract: {error}")
        failed = True
    for editor_key in editors:
        try:
            print(f"PASS  {EDITORS[editor_key]}: {check_editor(editor_key, not args.no_gallery_check, args.probe, args.project, args.scene)}")
        except Fail as error:
            print(f"FAIL  {EDITORS[editor_key]}: {error}")
            failed = True
    if args.project:
        assert_fixture_untouched()
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
