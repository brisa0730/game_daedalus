"""Install the package into a clean project, import the Starter and play it, the way a student would.

Usage:
    python Tools/install_check.py [--editor 6000.0|6000.6|all] [--source file|git|registry] [--ref REF]
    python Tools/install_check.py --make-fixture
    python Tools/install_check.py --reimport-test

Creates Tools/compat/install-<editor>/ from the Universal 2D template (com.unity.template.2d-cross-platform, the name
Hub shows on both editors), points its manifest at the package, imports the Starter through Package Manager's own
import, and runs the scripted playthrough. Fails if the playthrough was skipped: a skip means the sample wasn't there.
It then copies each Tools/fixtures/<version> (a student-shaped project saved on that release) to a scratch folder and
proves today's package opens and plays it; every release tag, and the version in package.json, must have one.
--make-fixture creates the current version's fixture on 6000.0, refused once that version is tagged. --reimport-test
installs a real 0.1.1 over a saved level and imports its sample both ways, printing what happened to the level (it
only knows 0.1.0 -> 0.1.1).
"""

import argparse
import hashlib
import json
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.dont_write_bytecode = True  # importing unity_tests must not leave Tools/__pycache__ behind
sys.path.insert(0, str(Path(__file__).resolve().parent))
from unity_tests import (EDITORS, INPUT_SYSTEM, PACKAGE_DIR, PACKAGE_NAME, REPO, RESULTS, editor_exe, run, run_unity)  # noqa: E402

GIT_URL = f"https://github.com/brisa0730/game_daedalus.git?path=/Packages/{PACKAGE_NAME}"
REGISTRY = {"name": "OpenUPM", "url": "https://package.openupm.com", "scopes": ["com.orcapafea"]}
STEP_TIMEOUT_SECONDS = 15 * 60
VERSION_TAG = re.compile(r"^\d+\.\d+\.\d+$")
CURRENT = json.loads((PACKAGE_DIR / "package.json").read_text(encoding="utf-8"))["version"]
PLAYTHROUGHS = ["Orcapafea.Daedalus.Tests.StarterSamplePlaythroughTests", "Orcapafea.Daedalus.Tests.StarterStagesPlaythroughTests",
                "Orcapafea.Daedalus.Tests.StarterTrapDemoTests"]
FIXTURE_TESTS = "Orcapafea.Daedalus.Tests.FixtureProjectTests"
FIXTURES = REPO / "Tools" / "fixtures"
SAMPLE_PATH = f"Assets/Samples/Daedalus Platformer Maker/{CURRENT}/Starter"
SECOND_LEVEL = "Assets/My Level 2.unity"
FIXTURE_METHOD = "InstallCheckFixture.Run"
FIXTURE_SCRIPT = f"""using Orcapafea.Daedalus;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// What a student does after importing: Save As, place a trap, finish the Your Turn spot, tune the Player; then, as
// install guide section 9 says, save, duplicate the level and link the first level's Exit to the copy (that order:
// linking first would make the copy link to itself).
public static class InstallCheckFixture
{{
    public static void Run()
    {{
        Scene scene = EditorSceneManager.OpenScene("{SAMPLE_PATH}/Starter.unity", OpenSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/My Level.unity");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("{SAMPLE_PATH}/Prefabs/Pop Spike.prefab");
        var placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        placed.transform.position = new Vector3(49f, 1.5f, 0f);
        var yourTurn = GameObject.Find("Your Turn (add a Trap here)");
        var trap = new SerializedObject(yourTurn.AddComponent<Trap>());
        trap.FindProperty("tile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TileBase>("{SAMPLE_PATH}/Tiles/Ground Tile.asset");
        trap.ApplyModifiedPropertiesWithoutUndo();
        var player = new SerializedObject(Object.FindFirstObjectByType<Player>());
        player.FindProperty("movement.runSpeed").floatValue = 7.5f;
        player.FindProperty("movement.jumpHeight").floatValue = 4f;
        player.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);
        if (!AssetDatabase.CopyAsset("Assets/My Level.unity", "{SECOND_LEVEL}"))
        {{
            Debug.LogError("InstallCheckFixture: couldn't copy My Level.");
            EditorApplication.Exit(1);
            return;
        }}
        var link = new SerializedObject(Object.FindFirstObjectByType<Exit>()).FindProperty("nextStage");
        string guid = AssetDatabase.AssetPathToGUID("{SECOND_LEVEL}");
        if (link == null || string.IsNullOrEmpty(guid))
        {{
            Debug.LogError("InstallCheckFixture: no Next stage field, or no GUID for the copy.");
            EditorApplication.Exit(1);
            return;
        }}
        link.stringValue = guid;
        link.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(0);
    }}
}}
"""
REIMPORT_METHOD = "InstallCheckReimport.Run"
REIMPORT_SCRIPT = f"""using System.Linq;
using Orcapafea.Daedalus;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;

// Imports the newer sample with the option named on the command line and reports what became of the saved level.
public static class InstallCheckReimport
{{
    public static void Run()
    {{
        var options = System.Environment.GetCommandLineArgs().Contains("-override")
            ? Sample.ImportOptions.OverridePreviousImports | Sample.ImportOptions.HideImportWindow
            : Sample.ImportOptions.HideImportWindow;
        Sample sample = Sample.FindByPackage("{PACKAGE_NAME}", null).Single();
        bool imported = sample.Import(options);
        AssetDatabase.Refresh();
        EditorSceneManager.OpenScene("Assets/My Level.unity", OpenSceneMode.Single);
        int missing = 0, drops = 0;
        string report = "";
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {{
            if (PrefabUtility.IsPrefabAssetMissing(root)) missing++;
            if (!root.name.StartsWith("Drop Floor")) continue;
            drops++;
            report += " delay=" + new SerializedObject(root.GetComponent<Trap>()).FindProperty("delay").floatValue
                + " source=" + AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(root));
        }}
        Debug.Log("REIMPORT options=" + options + " imported=" + imported + " importPath=" + sample.importPath
            + " missing=" + missing + " dropFloors=" + drops + report);
        EditorApplication.Exit(0);
    }}
}}
"""
IMPORT_METHOD = "InstallCheckImport.Run"
IMPORT_SCRIPT = f"""using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;

public static class InstallCheckImport
{{
    public static void Run()
    {{
        var samples = Sample.FindByPackage("{PACKAGE_NAME}", null).ToList();
        if (samples.Count != 1)
        {{
            Debug.LogError($"Expected one sample, found {{samples.Count}}.");
            EditorApplication.Exit(1);
            return;
        }}
        bool imported = samples[0].Import(Sample.ImportOptions.HideImportWindow);
        AssetDatabase.Refresh();
        Debug.Log($"InstallCheckImport: {{samples[0].importPath}} imported = {{imported}}");
        EditorApplication.Exit(imported ? 0 : 1);
    }}
}}
"""


def unity(command, log):
    try:
        return run_unity(command, None, STEP_TIMEOUT_SECONDS)
    except subprocess.TimeoutExpired:
        sys.exit(f"Unity timed out after {STEP_TIMEOUT_SECONDS} s; see {log}")


fixture_before = {}


def git_output(*arguments):
    return subprocess.run(["git", *arguments], cwd=REPO, capture_output=True, text=True, check=True, encoding="utf-8").stdout


def version_key(version):
    return tuple(int(part) for part in version.split("."))


def releases():
    """Every release tag, here and on origin, oldest first. A clone behind on tags must never act as if a release is free."""
    local = {tag for tag in git_output("tag", "--list").split() if VERSION_TAG.match(tag)}
    try:
        listing = git_output("ls-remote", "--tags", "origin")
    except subprocess.CalledProcessError:
        sys.exit("Can't reach origin to learn which releases exist; refusing to guess.")
    remote = set()
    for line in listing.splitlines():
        name = line.split("\t")[-1]
        if name.startswith("refs/tags/") and not name.endswith("^{}") and VERSION_TAG.match(name[len("refs/tags/"):]):
            remote.add(name[len("refs/tags/"):])
    if remote - local:
        sys.exit(f"Release tags {sorted(remote - local, key=version_key)} are on origin but not here. Run: git fetch --tags")
    return sorted(local | remote, key=version_key)


def check_tag_parity(project):
    """The tag is what OpenUPM built: its package.json, and the version the registry installed, must both match it."""
    if CURRENT not in releases():
        sys.exit(f"No tag {CURRENT}: the registry can't have built it.")
    tagged = json.loads(git_output("show", f"{CURRENT}:Packages/{PACKAGE_NAME}/package.json"))["version"]
    lock = json.loads((project / "Packages" / "packages-lock.json").read_text(encoding="utf-8"))["dependencies"][PACKAGE_NAME]
    if tagged != CURRENT or lock["version"] != CURRENT or lock["source"] != "registry":
        sys.exit(f"Tag parity failed: tag {CURRENT}, package.json at the tag {tagged}, installed {lock['version']} from {lock['source']}.")


def package_version():
    return json.loads((PACKAGE_DIR / "package.json").read_text(encoding="utf-8"))["version"]


def create_clean_project(editor_key):
    version = EDITORS[editor_key]
    exe = editor_exe(version)
    resources = exe.parents[1] / "Resources" if sys.platform == "darwin" else exe.parent / "Data" / "Resources"
    templates = sorted((resources / "PackageManager" / "ProjectTemplates").glob("com.unity.template.2d-cross-platform-*.tgz"))
    if not templates:
        sys.exit(f"No Universal 2D template in {version}")
    project = REPO / "Tools" / "compat" / f"install-{editor_key}"
    if project.exists():
        shutil.rmtree(project)
    project.parent.mkdir(parents=True, exist_ok=True)
    log = RESULTS / f"install-{editor_key}-create.log"
    print(f"Creating a clean project on {version}...")
    code = unity([str(exe), "-batchmode", "-quit", "-createProject", str(project),
                  "-cloneFromTemplate", str(templates[-1]), "-logFile", str(log)], log)
    if code != 0:
        sys.exit(f"Creating the project failed (exit {code}); see {log}")
    return project


def point_at_package(project, source, ref):
    manifest_path = project / "Packages" / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if source == "file":
        manifest["dependencies"][PACKAGE_NAME] = "file:" + PACKAGE_DIR.as_posix()
    elif source == "git":
        manifest["dependencies"][PACKAGE_NAME] = f"{GIT_URL}#{ref}"
    else:
        manifest["dependencies"][PACKAGE_NAME] = package_version()
        manifest["scopedRegistries"] = [REGISTRY]
    # Without these our test assemblies don't compile and the run reports zero tests.
    manifest["testables"] = [PACKAGE_NAME, INPUT_SYSTEM]
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")


def import_starter(editor_key, project):
    editor_folder = project / "Assets" / "Editor"
    editor_folder.mkdir(parents=True, exist_ok=True)
    script = editor_folder / "InstallCheckImport.cs"
    script.write_text(IMPORT_SCRIPT, encoding="utf-8", newline="\n")
    log = RESULTS / f"install-{editor_key}-import.log"
    print("Installing the package and importing the Starter...")
    code = unity([str(editor_exe(EDITORS[editor_key])), "-batchmode", "-projectPath", str(project),
                  "-executeMethod", IMPORT_METHOD, "-logFile", str(log)], log)
    if code != 0:
        sys.exit(f"Import failed (exit {code}); see {log}")
    scene = project / "Assets" / "Samples" / "Daedalus Platformer Maker" / package_version() / "Starter" / "Starter.unity"
    if not scene.exists():
        sys.exit(f"The import reported success, but {scene} isn't there; see {log}")
    script.unlink()
    script.with_name(script.name + ".meta").unlink(missing_ok=True)


def run_method(editor_key, project, script_name, source, method, log_name, *extra):
    editor_folder = project / "Assets" / "Editor"
    editor_folder.mkdir(parents=True, exist_ok=True)
    script = editor_folder / script_name
    script.write_text(source, encoding="utf-8", newline="\n")
    log = RESULTS / log_name
    code = unity([str(editor_exe(EDITORS[editor_key])), "-batchmode", "-projectPath", str(project), "-executeMethod", method,
                  "-logFile", str(log), *extra], log)
    if code != 0:
        sys.exit(f"{method} failed (exit {code}); see {log}")
    script.unlink()
    script.with_name(script.name + ".meta").unlink(missing_ok=True)
    return log


def copy_fixture(source, name):
    """A scratch copy of a fixture, pointed at today's package. Opening or building never touches the committed one."""
    if source not in fixture_before:
        fixture_before[source] = fixture_state(source)
    project = REPO / "Tools" / "compat" / name
    if project.exists():
        shutil.rmtree(project)
    shutil.copytree(source, project)
    point_at_package(project, "file", None)
    return project


def fixture_state(fixture):
    return {str(path.relative_to(fixture)): hashlib.sha256(path.read_bytes()).hexdigest()
            for path in fixture.rglob("*") if path.is_file()}


def assert_fixture_untouched(fixture):
    if fixture_state(fixture) != fixture_before[fixture]:
        sys.exit(f"The committed fixture {fixture.name} changed during the run.")


def make_fixture():
    if CURRENT in releases():
        sys.exit(f"Tag {CURRENT} exists: this fixture is what {CURRENT} students have, and is never regenerated.")
    fixture = FIXTURES / CURRENT
    project = create_clean_project("6000.0")
    point_at_package(project, "file", None)
    import_starter("6000.0", project)
    run_method("6000.0", project, "InstallCheckFixture.cs", FIXTURE_SCRIPT, FIXTURE_METHOD, "install-fixture.log")
    if fixture.exists():
        shutil.rmtree(fixture)
    fixture.mkdir(parents=True)
    for folder in ("Assets", "Packages", "ProjectSettings"):
        shutil.copytree(project / folder, fixture / folder)
    (fixture / "Packages" / "packages-lock.json").unlink(missing_ok=True)
    # The helper scripts are gone; an empty folder would survive in git only as an orphan .meta.
    empty = fixture / "Assets" / "Editor"
    if empty.exists() and not any(empty.iterdir()):
        empty.rmdir()
        (fixture / "Assets" / "Editor.meta").unlink(missing_ok=True)
    # The committed manifest is a student's: the release, from the registry, no test setup.
    manifest_path = fixture / "Packages" / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest["dependencies"][PACKAGE_NAME] = package_version()
    manifest["scopedRegistries"] = [REGISTRY]
    manifest.pop("testables", None)
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"Fixture written to {fixture.relative_to(REPO)}")


def reimport_case(option_name, extra, second_package):
    project = create_clean_project("6000.0")
    point_at_package(project, "file", None)
    import_starter("6000.0", project)
    run_method("6000.0", project, "InstallCheckFixture.cs", FIXTURE_SCRIPT, FIXTURE_METHOD, "install-reimport-save.log")
    manifest_path = project / "Packages" / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest["dependencies"][PACKAGE_NAME] = "file:" + second_package.as_posix()
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")
    log = run_method("6000.0", project, "InstallCheckReimport.cs", REIMPORT_SCRIPT, REIMPORT_METHOD, f"install-reimport-{option_name}.log", *extra)
    lines = [line for line in log.read_text(encoding="utf-8", errors="replace").splitlines() if line.startswith("REIMPORT ")]
    if not lines:
        sys.exit(f"{option_name}: no REIMPORT line; see {log}")
    print(f"{option_name}: {lines[-1]}")
    samples = project / "Assets" / "Samples" / "Daedalus Platformer Maker"
    print(f"  version folders: {sorted(p.name for p in samples.iterdir() if p.is_dir())}")


def reimport_test():
    second = REPO / "Tools" / "compat" / "package-0.1.1"
    if second.exists():
        shutil.rmtree(second)
    shutil.copytree(PACKAGE_DIR, second)
    metadata = second / "package.json"
    old_metadata = metadata.read_text(encoding="utf-8")
    if '"version": "0.1.0"' not in old_metadata:
        sys.exit("package.json isn't at 0.1.0; the test can't make a 0.1.1 from it.")
    metadata.write_text(old_metadata.replace('"version": "0.1.0"', '"version": "0.1.1"'), encoding="utf-8", newline="\n")
    prefab = second / "Samples~" / "Starter" / "Prefabs" / "Drop Floor.prefab"
    text = prefab.read_text(encoding="utf-8").replace("\r\n", "\n")
    if "  delay: 0\n" not in text:
        sys.exit("The Drop Floor prefab has no 'delay: 0' to change.")
    prefab.write_text(text.replace("  delay: 0\n", "  delay: 5\n"), encoding="utf-8", newline="\n")
    reimport_case("none", [], second)
    reimport_case("override", ["-override"], second)


def latest_results(project):
    runs = sorted((RESULTS / "runs").glob(f"*-{project.name}-*-playmode"))
    return runs[-1] / "results.xml"


def ran(project, what):
    root = ET.parse(latest_results(project)).getroot()
    played = [case for case in root.iter("test-case") if case.get("result") == "Passed"]
    if not played or int(root.get("skipped", "0")) != 0:
        print(f"FAIL  {what} did not run: {len(played)} passed, {root.get('skipped')} skipped")
        return False
    return True


def check_editor(editor_key, source, ref):
    project = create_clean_project(editor_key)
    point_at_package(project, source, ref)
    import_starter(editor_key, project)
    if source == "registry":
        check_tag_parity(project)
    # One run each: a skip in either is a failure, so neither is required in name only.
    for playthrough in PLAYTHROUGHS:
        if not run(editor_key, "playmode", playthrough, str(project)) or not ran(project, playthrough):
            return False
    present = sorted((p.name for p in FIXTURES.iterdir() if p.is_dir() and VERSION_TAG.match(p.name)), key=version_key)
    missing = sorted((set(releases()) | {CURRENT}) - set(present), key=version_key)
    if missing:
        print(f"FAIL  no fixture for {missing}: run --make-fixture on that version")
        return False
    for version in present:
        source = FIXTURES / version
        fixture = copy_fixture(source, f"fixture-{version}-{editor_key}")
        passed = run(editor_key, "playmode", FIXTURE_TESTS, str(fixture)) and ran(fixture, f"the {version} fixture test")
        assert_fixture_untouched(source)
        if not passed:
            return False
    return True


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--editor", choices=[*EDITORS, "all"], default="all")
    parser.add_argument("--source", choices=["file", "git", "registry"], default="file")
    parser.add_argument("--ref", default="main", help="branch, commit or tag for --source git")
    parser.add_argument("--make-fixture", action="store_true", help="create Tools/fixtures/<package.json version> on 6000.0")
    parser.add_argument("--reimport-test", action="store_true", help="install 0.1.1 over a saved level, import both ways")
    args = parser.parse_args()
    if args.make_fixture:
        make_fixture()
        return
    if args.reimport_test:
        reimport_test()
        return
    editors = list(EDITORS) if args.editor == "all" else [args.editor]
    results = [check_editor(editor, args.source, args.ref) for editor in editors]
    print(f"\n{sum(results)}/{len(results)} installs passed ({args.source})")
    sys.exit(0 if all(results) else 1)


if __name__ == "__main__":
    main()
