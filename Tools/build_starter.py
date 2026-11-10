"""Generate the Starter sample on 6000.0 and mirror it into the package's Samples~/Starter.

Usage:
    python Tools/build_starter.py [--refreeze] [--as-tagged]

Unity doesn't import Samples~, so the committed sample seeds Assets/StarterBuild in the compat project (keeping every
GUID), StarterBuilder runs there, and the result is mirrored back. A scene or prefab that differs from its seed only
in Unity's random object IDs is kept as seeded, so a rebuild with no real change leaves no diff, and references held by
a student's saved level stay valid.

--refreeze writes Tests/EditMode/StarterFrozen.txt. Until the first release tag exists it rewrites the list; after that it
only adds new entries. The generator leaves every piece listed at any release tag exactly as seeded (the union of every
tag's list). --as-tagged behaves as if the working copy's list were tagged, to test that path.
"""

import argparse
import hashlib
import re
import shutil
import subprocess
import sys
from pathlib import Path

sys.dont_write_bytecode = True  # importing unity_tests must not leave Tools/__pycache__ behind
sys.path.insert(0, str(Path(__file__).resolve().parent))
from unity_tests import EDITORS, PACKAGE_DIR, REPO, RESULTS, editor_exe, ensure_compat_project, run_unity  # noqa: E402
from install_check import git_output, releases  # noqa: E402

EDITOR_KEY = "6000.0"
BUILD_TIMEOUT_SECONDS = 10 * 60
SAMPLE = PACKAGE_DIR / "Samples~" / "Starter"
FROZEN_LIST = PACKAGE_DIR / "Tests" / "EditMode" / "StarterFrozen.txt"
BUILDER = REPO / "Assets" / "Dev" / "Editor" / "StarterBuilder.cs"
BUILD_FOLDER = "StarterBuild"
FROZEN_FOLDERS = ("Art", "Tiles", "Prefabs")
ROOT_PALETTE_META = SAMPLE / "Starter Palette.prefab.meta"
OBJECT_HEADER = re.compile(r"^--- !u!(\d+) &-?\d+", re.MULTILINE)
FILE_ID = re.compile(r"fileID: (-?[1-9]\d*)")
GUID = re.compile(r"^guid: ([0-9a-f]{32})", re.MULTILINE)


def normalized_objects(path):
    """The YAML objects of a scene or prefab with Unity's random IDs blanked, in a stable order."""
    text = path.read_text(encoding="utf-8").replace("\r\n", "\n")
    text = OBJECT_HEADER.sub(r"--- !u!\1 &X", text)
    text = FILE_ID.sub("fileID: X", text)
    return sorted(text.split("--- "))


def keep_unchanged_seeds(built, seed_copy):
    kept = 0
    for pattern in ("*.unity", "*.prefab"):
        for path in built.rglob(pattern):
            old = seed_copy / path.relative_to(built)
            if old.exists() and normalized_objects(path) == normalized_objects(old):
                shutil.copy2(old, path)
                kept += 1
    return kept


def sha256(path):
    data = path.read_bytes()
    if path.suffix != ".png":
        data = data.replace(b"\r\n", b"\n")
    return hashlib.sha256(data).hexdigest()


def frozen_rows():
    """One row per frozen piece and one per its .meta: relative path, GUID, content hash."""
    rows = {}
    for folder in FROZEN_FOLDERS:
        for meta in sorted((SAMPLE / folder).rglob("*.meta")) + ([ROOT_PALETTE_META] if folder == FROZEN_FOLDERS[0] else []):
            piece = meta.with_suffix("")
            if piece.is_dir():
                continue
            guid = GUID.search(meta.read_text(encoding="utf-8")).group(1)
            for path in (piece, meta):
                relative = path.relative_to(SAMPLE).as_posix()
                rows[relative] = f"{relative}\t{guid}\t{sha256(path)}"
    return rows


def parse_frozen(text):
    return {line.split("\t")[0]: line for line in text.splitlines() if line.strip()}


def read_frozen(as_tagged, tags):
    """What the releases froze: the union of every tag's own list, so an edited working copy can't loosen it."""
    if as_tagged:
        return parse_frozen(FROZEN_LIST.read_text(encoding="utf-8")) if FROZEN_LIST.exists() else {}
    relative = FROZEN_LIST.relative_to(REPO).as_posix()
    union = {}
    for tag in tags:
        try:
            rows = parse_frozen(git_output("show", f"{tag}:{relative}"))
        except subprocess.CalledProcessError:
            sys.exit(f"Tag {tag} exists but its frozen list can't be read here. Run: git fetch --tags")
        for key, row in rows.items():
            if union.get(key, row) != row:
                sys.exit(f"Release tags disagree on the frozen piece {key}: a frozen piece can never change.")
            union[key] = row
    return union


def write_frozen(frozen_mode, old):
    rows = frozen_rows()
    if frozen_mode:
        missing = sorted(set(old) - set(rows))
        if missing:
            sys.exit(f"Frozen pieces are gone from the sample: {missing}")
        changed = sorted(key for key in old if old[key] != rows[key])
        if changed:
            sys.exit(f"Frozen pieces changed: {changed}")
    FROZEN_LIST.write_text("\n".join(rows[key] for key in sorted(rows)) + "\n", encoding="utf-8", newline="\n")
    print(f"Frozen list: {len(rows)} rows in {FROZEN_LIST.relative_to(REPO)}")


def check_frozen_untouched(built, frozen_rows_before):
    for relative, row in frozen_rows_before.items():
        path = built / relative
        if not path.exists():
            sys.exit(f"A frozen piece is gone after the build: {relative}")
        if row.split("\t")[2] != sha256(path):
            sys.exit(f"The build rewrote a frozen piece: {relative}")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--refreeze", action="store_true", help="write StarterFrozen.txt from the result")
    parser.add_argument("--as-tagged", action="store_true", help="behave as if the working copy's frozen list were tagged")
    args = parser.parse_args()

    tags = [] if args.as_tagged else releases()
    frozen_mode = args.as_tagged or bool(tags)
    frozen_before = read_frozen(args.as_tagged, tags) if frozen_mode else {}
    if frozen_mode and not frozen_before:
        sys.exit(f"{FROZEN_LIST.name} is empty, but the tag exists: nothing to protect the frozen pieces with.")

    project = ensure_compat_project(EDITOR_KEY)
    built = project / "Assets" / BUILD_FOLDER
    built_meta = project / "Assets" / f"{BUILD_FOLDER}.meta"
    builder_folder = project / "Assets" / "Dev"
    pictures = [project / "starter-picture.png", project / "trap-demo-picture.png"]
    log = RESULTS / "starter-build.log"
    RESULTS.mkdir(exist_ok=True)

    for leftover in (built, builder_folder):
        if leftover.exists():
            shutil.rmtree(leftover)
    for picture in pictures:
        picture.unlink(missing_ok=True)
    try:
        seed_copy = RESULTS / "starter-seed"
        if seed_copy.exists():
            shutil.rmtree(seed_copy)
        if SAMPLE.exists():
            shutil.copytree(SAMPLE, built)
            shutil.copytree(SAMPLE, seed_copy)
        (builder_folder / "Editor").mkdir(parents=True)
        shutil.copy2(BUILDER, builder_folder / "Editor" / BUILDER.name)

        command = [str(editor_exe(EDITORS[EDITOR_KEY])), "-batchmode", "-quit", "-projectPath", str(project),
                   "-executeMethod", "Orcapafea.Daedalus.Dev.StarterBuilder.Build", "-logFile", str(log)]
        if frozen_mode:
            baseline = RESULTS / "starter-frozen-baseline.txt"
            baseline.write_text("\n".join(frozen_before.values()) + "\n", encoding="utf-8", newline="\n")
            command += ["-daedalusFrozenList", str(baseline)]
        print("Building the Starter on 6000.0...")
        try:
            code = run_unity(command, None, BUILD_TIMEOUT_SECONDS)
        except subprocess.TimeoutExpired:
            sys.exit(f"StarterBuilder timed out after {BUILD_TIMEOUT_SECONDS} s; see {log}")
        if code != 0:
            sys.exit(f"StarterBuilder failed (exit {code}); see {log}")

        if seed_copy.exists():
            print(f"Kept {keep_unchanged_seeds(built, seed_copy)} unchanged scene/prefab files as seeded.")
        check_frozen_untouched(built, frozen_before)

        if SAMPLE.exists():
            shutil.rmtree(SAMPLE)
        SAMPLE.parent.mkdir(exist_ok=True)
        shutil.copytree(built, SAMPLE)
        for picture in pictures:
            shutil.copy2(picture, RESULTS / picture.name)
        files = sum(1 for p in SAMPLE.rglob("*") if p.is_file() and p.suffix != ".meta")
        print(f"Starter: {files} files in {SAMPLE.relative_to(REPO)}; pictures TestResults/starter-picture.png, trap-demo-picture.png")
        if args.refreeze:
            write_frozen(frozen_mode, frozen_before)
    finally:
        for leftover in (built, builder_folder):
            if leftover.exists():
                shutil.rmtree(leftover)
        for meta in (built_meta, builder_folder.with_name("Dev.meta")):
            meta.unlink(missing_ok=True)
        for picture in pictures:
            picture.unlink(missing_ok=True)


if __name__ == "__main__":
    main()
