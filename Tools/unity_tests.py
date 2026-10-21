"""Run the daedalus Unity test suites in batch mode and fail closed.

Usage:
    python Tools/unity_tests.py [--editor 6000.0|6000.6|all] [--platform editmode|playmode|all]
                                [--filter NAME] [--project PATH]

6000.6 runs against the dev project (the repo root). 6000.0, the minimum, runs against a
throwaway project in Tools/compat/6000.0/ that references the package by file path, so the
dev project is never opened (and downgraded) by the older editor.

A run passes only when Unity exits 0, this run's own results.xml exists and is complete,
at least one test ran and none failed, the mode's own test assembly ran (unless --filter
is given), our assemblies were compiled in this run (the wrapper deletes them first) and
the log has no compiler warning from our package. Every run writes to a
fresh folder under TestResults/runs/, so an earlier result can never stand in for it.
"""

import argparse
import datetime
import json
import os
import re
import shutil
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
PACKAGE_NAME = "com.orcapafea.daedalus"
PACKAGE_DIR = REPO / "Packages" / PACKAGE_NAME
RESULTS = REPO / "TestResults"
EDITORS = {"6000.0": "6000.0.85f1", "6000.6": "6000.6.7f1"}
PLATFORMS = {"editmode": "EditMode", "playmode": "PlayMode"}
ASSEMBLIES = {
    "editmode": "Orcapafea.Daedalus.Tests.EditMode",
    "playmode": "Orcapafea.Daedalus.Tests.PlayMode",
}
TIMEOUT_SECONDS = 30 * 60
# Unity prints our compiler warnings only when it builds our assemblies (a cache hit counts).
OUR_COMPILE = re.compile(r"\bCsc \S*Orcapafea\.Daedalus")
OUR_WARNING = re.compile(r"com\.orcapafea\.daedalus[/\\][^\r\n]*warning CS\d+")
OUR_ERROR = re.compile(r"[^\r\n]*error CS\d+[^\r\n]*")
LOCKED = "another Unity instance is running"
# Unity 6.6 in batch mode logs nothing about a lock: it exits 1 right after this line.
LAST_LINE_WHEN_LOCKED = "Successfully changed project path to:"


def editor_exe(version):
    root = Path(os.environ.get("UNITY_EDITORS", r"C:\Program Files\Unity\Hub\Editor"))
    exe = root / version / "Editor" / "Unity.exe"
    if not exe.exists():
        sys.exit(f"Unity {version} not found at {exe} (set UNITY_EDITORS to the Hub editor folder)")
    return exe


def ensure_compat_project(editor_key):
    """Create (once) the throwaway project for an editor other than the dev one."""
    version = EDITORS[editor_key]
    project = REPO / "Tools" / "compat" / editor_key
    if not (project / "ProjectSettings" / "ProjectVersion.txt").exists():
        exe = editor_exe(version)
        templates = exe.parent / "Data" / "Resources" / "PackageManager" / "ProjectTemplates"
        candidates = sorted(templates.glob("com.unity.template.2d-cross-platform-*.tgz"))
        if not candidates:
            sys.exit(f"No 2D template found in {templates}")
        if project.exists():
            shutil.rmtree(project)
        project.parent.mkdir(parents=True, exist_ok=True)
        log = project.parent / f"{editor_key}-create.log"
        print(f"Creating compat project for {version} (first run only)...")
        code = subprocess.run([str(exe), "-batchmode", "-quit", "-createProject", str(project),
                               "-cloneFromTemplate", str(candidates[-1]), "-logFile", str(log)]).returncode
        if code != 0:
            sys.exit(f"Creating the compat project failed (exit {code}); see {log}")

    manifest_path = project / "Packages" / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest["dependencies"][PACKAGE_NAME] = "file:" + PACKAGE_DIR.as_posix()
    manifest["testables"] = [PACKAGE_NAME]
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")
    return project


def project_for(editor_key, override):
    if override:
        return Path(override).resolve()
    if editor_key == "6000.6":
        return REPO
    return ensure_compat_project(editor_key)


def failed_test_names(root):
    return [case.get("fullname") for case in root.iter("test-case") if case.get("result") == "Failed"]


def looks_locked(log_text, project):
    if LOCKED in log_text:
        return True
    opened_nothing = LAST_LINE_WHEN_LOCKED in log_text and "Loading" not in log_text.split(LAST_LINE_WHEN_LOCKED, 1)[1]
    return opened_nothing and (project / "Temp" / "UnityLockfile").exists()


def check(run_dir, exit_code, started, platform, filtered, project):
    """Return (ok, message). Checks run in order and stop at the first failure."""
    log_text = ""
    log_path = run_dir / "unity.log"
    if log_path.exists():
        log_text = log_path.read_text(encoding="utf-8", errors="replace")

    xml_path = run_dir / "results.xml"
    root = None
    if xml_path.exists() and xml_path.stat().st_mtime >= started:
        try:
            root = ET.parse(xml_path).getroot()
        except ET.ParseError as error:
            return False, f"results.xml does not parse: {error}"

    if exit_code != 0:
        if looks_locked(log_text, project):
            return False, "the project is open in another Unity instance; close it and rerun"
        if exit_code == 2 and root is not None:
            return False, "test failures: " + ", ".join(failed_test_names(root))
        errors = sorted(set(OUR_ERROR.findall(log_text)))[:10]
        detail = ("\n    " + "\n    ".join(errors)) if errors else f" (see {log_path})"
        return False, f"Unity exited with code {exit_code}{detail}"

    if root is None:
        return False, f"no results.xml written by this run (see {log_path})"
    if root.tag != "test-run" or root.get("total") is None or root.get("end-time") is None:
        return False, "results.xml is incomplete (no finished test-run)"

    total = int(root.get("total"))
    failed = int(root.get("failed", "0"))
    if total == 0:
        return False, "0 tests ran"
    if failed:
        return False, "test failures: " + ", ".join(failed_test_names(root))

    if not filtered:
        assembly = ASSEMBLIES[platform]
        names = {suite.get("name") for suite in root.iter("test-suite") if suite.get("type") == "Assembly"}
        if f"{assembly}.dll" not in names:
            return False, f"{assembly} did not run (assemblies: {', '.join(sorted(n for n in names if n)) or 'none'})"

    if not OUR_COMPILE.search(log_text):
        return False, "our assemblies were not compiled in this run, so warnings went unchecked"
    warnings = sorted(set(OUR_WARNING.findall(log_text)))
    if warnings:
        return False, "compiler warnings in our package:\n    " + "\n    ".join(warnings)

    return True, f"{total} tests passed"


def force_our_compile(project):
    """Delete our compiled assemblies so this run builds them and prints any warnings."""
    library = project / "Library"
    for path in [*library.glob("ScriptAssemblies/Orcapafea.*"), *library.glob("Bee/artifacts/*/Orcapafea.*")]:
        if path.is_file():
            path.unlink()


def run_unity(command, env):
    """Run Unity; on timeout kill it and its child processes (they would hold the project lock)."""
    process = subprocess.Popen(command, env=env)
    try:
        return process.wait(timeout=TIMEOUT_SECONDS)
    except subprocess.TimeoutExpired:
        subprocess.run(["taskkill", "/F", "/T", "/PID", str(process.pid)], capture_output=True)
        raise


def run(editor_key, platform, test_filter, project_override):
    version = EDITORS[editor_key]
    exe = editor_exe(version)
    project = project_for(editor_key, project_override)
    stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M%S-%f")
    run_dir = RESULTS / "runs" / f"{stamp}-{project.name}-{editor_key}-{platform}"
    run_dir.mkdir(parents=True, exist_ok=False)

    command = [str(exe), "-batchmode", "-projectPath", str(project), "-runTests",
               "-testPlatform", PLATFORMS[platform],
               "-testResults", str(run_dir / "results.xml"),
               "-logFile", str(run_dir / "unity.log")]
    if test_filter:
        command += ["-testFilter", test_filter]
    env = dict(os.environ, DAEDALUS_TEST_RESULTS=str(RESULTS))

    force_our_compile(project)
    started = time.time()
    try:
        exit_code = run_unity(command, env)
    except subprocess.TimeoutExpired:
        ok, message = False, f"timed out after {TIMEOUT_SECONDS // 60} min"
    else:
        ok, message = check(run_dir, exit_code, started, platform, bool(test_filter), project)
    seconds = time.time() - started

    if ok and not project_override and not test_filter:
        shutil.copyfile(run_dir / "results.xml", RESULTS / f"{editor_key}-{platform}.xml")
    status = "PASS" if ok else "FAIL"
    print(f"{status}  {version} {PLATFORMS[platform]:8} {project.name}: {message}  ({seconds:.0f}s, {run_dir.name})")
    return ok


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--editor", choices=[*EDITORS, "all"], default="all")
    parser.add_argument("--platform", choices=[*PLATFORMS, "all"], default="all")
    parser.add_argument("--filter", help="Unity -testFilter value")
    parser.add_argument("--project", help="run against this project instead (needs a single --editor)")
    args = parser.parse_args()

    editors = list(EDITORS) if args.editor == "all" else [args.editor]
    platforms = list(PLATFORMS) if args.platform == "all" else [args.platform]
    if args.project and len(editors) != 1:
        parser.error("--project needs a single --editor")

    results = [run(e, p, args.filter, args.project) for e in editors for p in platforms]
    passed = sum(results)
    print(f"\n{passed}/{len(results)} runs passed")
    sys.exit(0 if passed == len(results) else 1)


if __name__ == "__main__":
    main()
