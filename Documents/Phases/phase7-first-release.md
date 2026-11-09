# Phase 7: First Release

## Overview

Students can't try the tool yet. There is no install route and no example level. The package says "no gameplay yet" and has no license.

**Where things stand:** The tool works for the operator. A student has no way to get it, and nothing to open once they have it.

**What this phase changes:**
- The tool gets an open-source license and its first version number.
- A starter level students can open and play right away, with the traps already working and one spot left for them to try.
- Short step-by-step install instructions in Korean.
- The tool is listed on a public registry so students install it from inside Unity.
- Proof that a clean computer can install it and play the starter on both supported editor versions.
- The automatic scene saving idea is dropped. Students save their own work.

**How you'll know it worked:** A fresh project on a clean machine installs the tool from the registry, imports the starter, and plays it.

**Not in this phase:** new animation, tile templates, the play-from-here button and the checking list. They stay on the roadmap.

---

## Source
- **GDD sections:** `Documents/GDD/GDD.md`; `architecture.md` (editor tools row); `systems.md` (Editor Tools, Combat & Damage line 57); `platforms.md` (Student work); `Glossary.md` (Editor tools); `conventions.md` (Authored Data & Upgrades); `.claude/CLAUDE.md` (Student projects, Layout, Distribution, Art); `Documents/Operations/Roadmap.md` (Milestones 6 and 7, §2 gates); `Documents/Operations/Deferred.md`.
- **GDD version:** the current one at start (v0.23 at planning). This phase bumps it.
- **Depends on:** Phases 1 to 6. The Trap, Checkpoint, Exit, Player and DaedalusTile components, the Add Component menu entries, TrapAuthoring, Share my game and the gallery are all in place.
- **Decisions this phase makes real** (home until implementation; synced via Notes to GDD):
  1. MIT license, on the package folder only.
  2. First tag `0.1.0`.
  3. OpenUPM listing.
  4. A starter sample with a CC0 art pack.
  5. Scene autosave is cut entirely.
  6. Share my game's operator editor check moves up to before students try the release.

## File Manifest

| Action | File | Purpose |
|--------|------|---------|
| MODIFY | `.claude/CLAUDE.md`, `Documents/GDD/architecture.md`, `systems.md`, `platforms.md`, `Glossary.md`, `.claude/skills/audit/SKILL.md` | Remove scene autosave (Step 1) |
| NEW | `Packages/com.orcapafea.daedalus/LICENSE.md` (+ `.meta`) | MIT license for the package folder only |
| MODIFY | `Packages/com.orcapafea.daedalus/package.json` | license, repository, documentationUrl, keywords, samples |
| MODIFY | `Packages/com.orcapafea.daedalus/README.md` | Becomes the OpenUPM listing; says what works and what doesn't |
| NEW | `Packages/com.orcapafea.daedalus/Documentation~/install-ko.md` | Korean student install steps (English menu names) |
| MODIFY | `.gitattributes` | Path exception so starter art stays out of Git LFS |
| NEW | `Assets/Dev/Editor/StarterBuilder.cs` (+ `.meta`) | The one generator for the starter (Build / SelfCheck / SavePicture) |
| NEW | `Tools/build_starter.py` | Runs the generator on 6000.0 inside the compat project and copies the result to `Samples~` |
| NEW | `Packages/com.orcapafea.daedalus/Samples~/Starter/**` (+ committed `.meta`) | Scene, prefabs, tiles, sprites, Tile Palette, art-pack license |
| NEW | `Packages/com.orcapafea.daedalus/Tests/EditMode/ReleaseMetadataTests.cs`, `StarterSampleTests.cs`, `StarterFrozen.txt` (+ `.meta`) | Metadata and sample guards; the frozen list of the starter's pieces |
| NEW | `Packages/com.orcapafea.daedalus/Tests/PlayMode/StarterSamplePlaythroughTests.cs` (+ `.meta`) | Scripted playthrough of an imported starter |
| NEW | `Tools/install_check.py` | Clean-project install and playthrough on both editors; source = file, git or registry |
| NEW | `Tools/fixtures/0.1.0/**` | Student-shaped fixture project for this version |
| NEW | `Packages/com.orcapafea.daedalus/Tests/PlayMode/FixtureProjectTests.cs` (+ `.meta`) | Opens and plays the fixture scene (ignored when no fixture is present) |
| MODIFY | `Tools/share_check.py` | Add `--project` and `--scene` so a Share of the student's copy is tested |
| MODIFY | `.gitignore` | Ignore `Library/`, `Temp/`, `Logs/`, `UserSettings/` and `Packages/packages-lock.json` under `Tools/fixtures/` (the scratch projects live in the already-ignored `Tools/compat/`) |
| MODIFY | `Documents/Operations/Deferred.md`, `Roadmap.md` (Milestone 6 and 7 lines, §2 gate), `Changelog.md`, `GDD.md` (bump), `.claude/memory/decisions.md`, `.claude/MEMORY.md` | Cleanup, record and version bump |

---

## Steps

Markers: **[OPERATOR GATE]** blocks until the operator decides. **[NEEDS APPROVAL AT RUN TIME]** is outward-facing: ask the operator at the moment it runs, even if they approved the plan.

### Step 1: Cut scene autosave from the docs
- **MODIFY** these, each a small edit. Keep every other word; update-safety rules stay.
    - `.claude/CLAUDE.md` line 51 (Student projects): drop "Scene autosave from the editor tools (Unity does not autosave scenes)". Keep update safety, the fixture project and "Validation warns".
    - `Documents/GDD/architecture.md` line 12: drop "scene autosave" from the editor tools row.
    - `Documents/GDD/systems.md` line 152: delete the Scene autosave bullet. Say instead that students work in the normal Unity Editor and save themselves (Ctrl+S).
    - `Documents/GDD/platforms.md` line 32: "saved by Unity (Ctrl+S)", without "plus the editor tools' scene autosave".
    - `Documents/GDD/Glossary.md` line 17: drop ", scene autosave".
    - `.claude/skills/audit/SKILL.md` line 81: drop "scene autosave works;".
- Do not touch Changelog history. The Roadmap's Milestone 6 line and §4 were already edited when this prompt was saved.
- **Tests:** none.
- **Verify:** `[grep]` `grep -rni autosave .claude/CLAUDE.md Documents/GDD .claude/skills Documents/Operations/Roadmap.md` returns nothing. Changelog and archived phases may still match.
- **Depends on:** none.

### Step 2: Choose the starter art pack **[OPERATOR GATE]**
- Shortlist 2 to 3 CC0 packs (candidates to check first, from memory and unverified: Kenney's Tiny Town, Tiny Dungeon and 1-bit packs). Measure each pack's tile size; never shortlist one that isn't 16 px and plan to rescale it (Kenney's Pixel Platformer may be 18 px).
- For each, open the license at its source and quote the URL and the date checked in the message. Do not trust a mirror. Confirm CC0 and not "free for personal use".
- Criteria:
    - 16 px tiles.
    - One ground tile that is seamless on all four sides (tile it 4 × 4 and look at the picture).
    - A spike tile, a player-like character sprite, and something readable as a checkpoint and an exit.
    - Full Rect-compatible sprites (no mesh-tight need).
    - A license file we can ship beside the pack.
- Show the operator a picture per candidate (ground tiled, spike, player, checkpoint, exit). Recommend one.
- If no pack gives a seamless ground tile, say so and offer a fallback: the generator draws plain placeholder art per pixel, as the dev bench does. This is the operator's call.
- Nothing from the chosen pack enters the repo until the operator approves.
- **Tests:** none.
- **Verify:** `[manual]` the operator approves a named pack in writing. Record the choice and license URL in Notes to GDD.
- **Depends on:** none (Steps 3 to 5 can run in parallel).

### Step 3: Package metadata and license
- **NEW** `Packages/com.orcapafea.daedalus/LICENSE.md`: the MIT text, "Copyright (c) 2026 orcapafea" (use the author name already in package.json). It covers the package folder only. Add one line saying the gallery server, docs and the development project are not covered. Add the `.meta` file Unity generates.
- **MODIFY** `package.json`, keeping version `0.1.0`:
    - `"license": "MIT"`.
    - `"repository"`: `{"type": "git", "url": "https://github.com/brisa0730/game_daedalus.git"}`.
    - `"documentationUrl"`: the GitHub URL of `Documentation~/install-ko.md` on `main`.
    - `"keywords"`: a handful (platformer, 2d, tilemap, education).
    - `"samples": [{"displayName": "Starter", "description": "<one Korean-or-English sentence, plain>", "path": "Samples~/Starter"}]`.
- **MODIFY** `Tests/EditMode/PackageManifestTests.cs` only if needed. Add the new tests in `ReleaseMetadataTests.cs`:
    - `version` is plain `x.y.z` and `0.1.0` at this phase.
    - `license` is `MIT` and `LICENSE.md` exists and begins with "MIT License".
    - `samples[0].path` exists on disk.
    - `repository.url` is the public repo.
    - `documentationUrl` ends with a file that exists in `Documentation~`.
    - A tag-parity check in `Tools/install_check.py` (Step 9) covers the tag, since an EditMode test can't see Git.
- **Verify:** `[automated]` EditMode on both editors.
- **Depends on:** none.

### Step 4: Keep starter art out of Git LFS
- **MODIFY** `.gitattributes`. `*.png` is LFS-tracked and the package has none today. Add, after the Images block (the last match wins), a path rule scoped to image types: `Packages/com.orcapafea.daedalus/Samples~/**/*.png -filter -diff -merge binary` (one line per image extension the pack ships). Do not match `Samples~/**` as a whole: `binary` would also turn the scene, prefabs, tiles and `.meta` files into opaque files, and Step 5's "no diff" check and any GUID change could no longer be read.
- Why: OpenUPM and Package Manager fetch by Git. Files stored as LFS pointers, which an LFS-less fetch downloads as text, would arrive broken.
- Prove it in two ways, with a throwaway PNG placed under `Samples~` (do not commit it):
    - `git check-attr filter -- <that png>` prints `unset`, and an ordinary PNG elsewhere prints `lfs`.
    - `git check-attr diff text -- Packages/com.orcapafea.daedalus/Samples~/Starter/Starter.unity` prints `diff: unspecified` and `text: auto`: the scene stays text.
    - After the real starter is added (Step 7), `git lfs ls-files` shows no file under `Packages/`.
- **Tests:** none (the check is the proof).
- **Verify:** `[manual]` both outputs pasted into the phase log.
- **Depends on:** none (final proof after Step 7).

### Step 5: One generator for the starter, run on 6000.0
- **NEW** `Assets/Dev/Editor/StarterBuilder.cs`, following the shape of `TrapTestSceneBuilder`:
    - `Build()` for the batch entry. A menu item under Daedalus Dev for the dev project is fine.
    - `SelfCheck()` that returns exit 0/1.
    - `SavePicture()`.
- Sprites are imported at 16 PPU, Point filter, Full Rect (the bench's import code at lines 427 to 460 is the pattern). Tiles and prefabs are reused by path when they exist, so GUIDs stay stable.
- Output goes to a scratch folder in the running Unity project, `Assets/StarterBuild/`.
- **Seed first.** If `Packages/com.orcapafea.daedalus/Samples~/Starter/` exists, copy it, `.meta` files included, into `Assets/StarterBuild/` before building, so every asset keeps its committed GUID. Then copy the folder back, `.meta` files included. Unity doesn't import `Samples~`, so this is the only way GUIDs persist.
- **Once tag `0.1.0` exists, the generator never rewrites a frozen piece** (Step 7). It leaves each file listed in `StarterFrozen.txt`, with its `.meta`, exactly as seeded and builds only new pieces and the scene. Otherwise a re-save on another 6000.0 patch could change the bytes without changing a value, and the freeze test would fail with no way out. SelfCheck asserts every frozen file is byte-identical to its seed after the build.
- **NEW** `Tools/build_starter.py`:
    - Reuses `unity_tests.py`'s `ensure_compat_project("6000.0")`.
    - Copies `StarterBuilder.cs` into the compat project's `Assets/Dev/Editor/`.
    - Runs Unity 6000.0.85f1 batch with `-executeMethod`.
    - Mirrors the result and `.meta` files into `Samples~/Starter/`: a file the build no longer produces is deleted there, or the seed would carry it forward forever. The generator holds the list of files it makes, deletes anything else in `Assets/StarterBuild/` before saving, and SelfCheck asserts the list.
    - Fails on a non-zero exit or SelfCheck failure.
    - Writes `TestResults/starter-picture.png`.
- Author on 6000.0 only. The dev scenes generate on 6000.6, whose serialized format might not open on the minimum, so never commit a 6000.6-generated starter.
- **Tests:** SelfCheck, described in Step 7.
- **Verify:** `[manual]` running `Tools/build_starter.py` twice produces no `git diff` on the second run (idempotent, GUIDs stable).
- **Depends on:** Step 2 for the final art. Start with generated placeholders and swap in the pack when approved.

### Step 6: Import the approved art pack
- Place only the art the scene uses, plus the pack's license file beside it (`Samples~/Starter/Art/LICENSE.txt`, verbatim). The source URL goes in a one-line `SOURCE.txt` beside it.
- The generator sets the import settings (16 PPU, Point, Full Rect, no compression). Packs that ship sprite sheets are sliced in the generator, not by hand.
- About three tiles in all: Ground, Spike (a `DaedalusTile` set to Hazard) and Fake Floor (a `DaedalusTile` set to None, drawn like ground). Plain tiles only; the open auto-tile decision (Roadmap §4) is not touched.
- **Tests:** in Step 7's `StarterSampleTests`: the pack's license file exists beside the art; every PNG under `Samples~` sits in a folder with a license file.
- **Verify:** `[automated]` as above, and `[manual]` look at the picture.
- **Depends on:** Steps 2 and 5.

### Step 7: The starter scene and prefabs
- **Scene** (`Samples~/Starter/Starter.unity`), built by the generator. Everything a play needs is already in it, so students need to add nothing before the first play:
    - One Grid with **one Tilemap named `Collision`** and nothing else (no Background or Foreground).
    - A camera tagged `MainCamera`.
    - A Global Light 2D.
    - A **Player already placed**, with On hazard = Respawn at checkpoint (the default, Return to safe ground, makes Checkpoints do nothing). The bench sets this through the property path `health.onHazard`.
- **Level:** three short sections, clearable end to end in about 30 seconds. Each section opens at a Checkpoint and teaches one thing:
    1. Fake Floor (a None-kind tile painted on Collision) plus a Drop Floor.
    2. Pop Spike plus a Falling Ceiling (placed high, with the explicit zone below reaching the floor).
    3. A Runaway Exit near the end.
    - Then an unfinished "your turn" stretch containing one object with only a SpriteRenderer and no Trap, so **Add Component → Daedalus → Trap** happens on purpose.
    - The stretch must be jumpable without touching that object, so the level stays clearable. The exact form is for the implementer to propose and show in the picture.
    - A final Exit follows the stretch.
- **Prefabs** (`Samples~/Starter/Prefabs/`): Drop Floor, Pop Spike, Falling Ceiling, Runaway Exit (an Exit carrying a Move trap, offset 6,0), Checkpoint, Exit. **No Player and no Rope prefab.**
    - Trap zones are stored relative to the object (`TrapLook.WorldZone`), so they survive being dragged to another spot. Do not copy the bench's world rects. Each prefab uses default values except what its recipe needs. The generator computes the zone after setting the trap's size and, for the Runaway Exit, after it is on the Exit (`TrapLook.DefaultZoneLocal(trap)`).
    - **The Falling Ceiling is the exception and needs its own zone.** A trap's default zone is its box grown one tile *upward* (`LevelRules.DefaultZone`), so a ceiling placed high would have a zone above itself that no player enters. The prefab ships an explicit local zone as wide as the trap, from its box down 9 tiles (the height it hangs above the floor in section 2, as on the bench, line 66). The zone moves with the object: placed at another height, it may stop short of the floor or poke through it. Short means the trap never fires. The student steps (Step 8) say so and show the fix.
- **Fake Floor ships as a tile, not a prefab.** A tile isn't a GameObject. It is a `Fake Floor` `DaedalusTile` (kind None, ground sprite) in the sample's Tile Palette, painted once in section 1. The bench paints it on Background; here it sits on Collision, so there is no wrong-layer failure and only one Tilemap.
    - A short test proves, on a hand-built map in EditMode: the Player falls through a None tile, and a None tile is not recorded as safe ground. Add this to `StarterSampleTests` or an existing sim suite.
- **Tile Palette:** a palette prefab with the three tiles, built as the bench's palette is (lines 483 to 507).
- **SelfCheck** asserts: exactly one Tilemap, named Collision; a MainCamera exists; one Global Light 2D; one enabled Player with RespawnAtCheckpoint; every prefab has the expected component; every `.meta` exists; no `Library` or `Temp` path leaked in.
- **Tests:**
    - `StarterSampleTests` (EditMode): `Samples~/Starter` exists with its `.meta` files; no duplicate GUIDs inside it; every prefab referenced by the scene exists.
    - **Freeze GUIDs and content.** A student's saved level holds prefab instances of the sample's prefabs, and every setting they didn't override comes from the prefab asset, so keeping the references alive isn't enough. `Tests/EditMode/StarterFrozen.txt` lists each prefab, tile and sprite (with its `.meta`) by path, GUID and SHA-256 of its content (text files hashed with line endings normalized to LF). A test fails, naming the file, if a listed entry changes or disappears. **The rule: a frozen sample piece never changes; a new behaviour is a new prefab under a new name.** `Tools/build_starter.py --refreeze` rewrites the list until tag `0.1.0` exists; after that it only adds new entries.
    - `StarterSamplePlaythroughTests` (PlayMode): see Step 9. It plays a scripted run and asserts each Checkpoint is reached and every trap type fires.
- **Verify:** `[automated]` as above; `[manual]` the implementer looks at `TestResults/starter-picture.png` and at the PlayMode screenshots of each section. Re-run Step 4's `git lfs ls-files` proof.
- **Depends on:** Steps 5 and 6.

### Step 8: README, student steps and install text
- **MODIFY** `Packages/com.orcapafea.daedalus/README.md`. It is the OpenUPM listing and also the Package Manager page.
    - Replace "Work in progress: no gameplay yet" (line 5) with what works: the Player and its movement (run, jump, wall and rope abilities as built), traps, Checkpoints, Exits, Share my game to the class gallery, and the Starter sample.
    - And what doesn't: **no enemies, no attack, no play-from-here button, no custom inspectors beyond the Trap, Checkpoint and Exit** (confirm the current list in the code before writing it).
    - State the license, MIT, and link the install steps. Add one line saying the Starter needs a Universal 2D (URP) project: its Global Light 2D is a URP type, and a Built-in project opens it with a missing script.
    - Verify before release (unverified): whether OpenUPM shows the package-folder README or a repo-root one. If it shows the repo root, add a root `README.md` pointing here. Do not guess.
- **NEW** `Packages/com.orcapafea.daedalus/Documentation~/install-ko.md`. This is the home of the student steps, since it ships with the package and `documentationUrl` can point at it. No new top-level directory. Korean prose, English menu names (no Korean language pack is installed in the editor). Order:
    1. Install exactly one editor version, **6000.0.85f1**, with **WebGL Build Support** ticked. Include the archive link in case Hub doesn't list it.
    2. Create the project from the **Universal 2D** template (the package pulls in neither the Tile Palette nor URP).
    3. Open **Edit → Project Settings → Package Manager → Scoped Registries** and paste the three values: Name `OpenUPM`, URL `https://package.openupm.com`, Scope `com.orcapafea`. (Confirm the scope against the OpenUPM listing at submission time.)
    4. **Window → Package Manager → My Registries → Daedalus Platformer Maker → Install**, then the **Samples** tab → **Import** Starter. Include a screenshot of the imported folder (Import opens nothing; there is no menu item for it).
    5. The Console: where it is (**Window → General → Console**) and that warnings appear only there.
    6. Open the starter, play it, then **File → Save As** a copy outside `Assets/Samples` (re-importing the sample overwrites it, and this starts the save habit).
    7. The core gesture: erase three tiles, drag a Trap prefab in, **Snap to grid**; then **Add Component → Daedalus → Trap** on the "your turn" object. One line on the Falling Ceiling: its trigger box is the tall box below it; after moving it, check that box reaches the floor, and drag the box's handle in the Scene view if it doesn't.
    8. "Put the Player back at the start before sharing."
    9. **Daedalus → Share my game**. Link to the gallery.
- Keep the page short; one screen per step. Do not name code files.
- **Tests:** a small EditMode test that the page exists and the README links it.
- **Verify:** `[manual]` the operator follows it (Step 11).
- **Depends on:** Step 3 (URL and values).

### Step 9: Fixture project and the clean-project install test
- **NEW** `Tools/fixtures/0.1.0/`: a Unity project shaped like a student's.
    - Made by a script that uses the real import path (`UnityEditor.PackageManager.UI.Sample.FindByPackage(...).Import()`), then saves the scene as a copy outside `Assets/Samples`, places a few traps from the prefabs, and tunes the Player (a few non-default values).
    - Created on **6000.0.85f1**, so it opens on both editors.
    - The creation script installs the package from `file:` (the registry doesn't exist until Step 15), then rewrites the committed manifest to the student's shape: the package at `0.1.0` plus the OpenUPM scoped registry. `Packages/packages-lock.json` is not committed: one written during the `file:` install names a path that exists only on the dev machine. Unity regenerates it in each scratch copy.
    - Add `.gitignore` entries for its `Library/`, `Temp/`, `Logs/`, `UserSettings/` (the root patterns are anchored to the repo root and won't cover it).
    - The project's `Assets/` PNGs may stay in LFS; only `Samples~` is exempt.
- **NEW** `Tests/PlayMode/FixtureProjectTests.cs`: loads the fixture scene, plays it, and asserts the placed traps and the tuned Player values survived. It is ignored (not failed) when the fixture scene isn't in the project.
- **NEW** `Tools/install_check.py`, `--editor 6000.0|6000.6|all`, `--source file|git|registry`:
    1. Creates a clean project in `Tools/compat/` (already gitignored) from the same template lookup as `ensure_compat_project`. That template, `com.unity.template.2d-cross-platform`, is the one Hub shows as **Universal 2D** on both editors (its `displayName`, read from each editor's template file), so the test follows the students' path.
    2. Writes a manifest pointing at the package from the chosen source (file path; the Git URL with `?path=/Packages/com.orcapafea.daedalus#<tag-or-branch>`; or the scoped registry), with `testables` listing the package and `com.unity.inputsystem`, as `unity_tests.py` does. Without that our test assemblies don't compile and the run reports zero tests.
    3. Imports the Starter through `Sample.Import()` in batch.
    4. Runs `StarterSamplePlaythroughTests` and fails if the tests were ignored.
    5. Copies `Tools/fixtures/0.1.0` to a scratch folder in `Tools/compat/` and runs `FixtureProjectTests` there, never in place: opening it in 6000.6 rewrites its version and lock files. The committed manifest stays student-shaped (the package at `0.1.0` from the scoped registry). The scratch copy's manifest is rewritten to the current package by `file:` absolute path, plus the same `testables`, so the run proves today's package opens and plays the 0.1.0 project.
    6. For `--source registry`, also checks the installed version equals `package.json` and equals the tag.
    7. Asserts `git status --porcelain Tools/fixtures` is empty afterwards.
- **Re-import hazard test, in the clean project.** Package Manager copies a sample to a different folder per version, so a newer version's import puts the same GUIDs in a second folder while the student's saved copy still references them, and which copy Unity keeps isn't predictable. Use a real second version: a scratch copy of the package bumped to `0.1.1`, with one value changed in a frozen prefab, installed from `file:`. Import the 0.1.0 sample, save the scene copy elsewhere, install 0.1.1, then import its sample through `Sample.Import` **both ways**: `ImportOptions.None` and `ImportOptions.OverridePreviousImports` (both exist on 6000.0). In each case confirm the student's copy opens with nothing missing, and whether the changed value reached it. Record both results and what students should click in the phase log. If either can break or change a copy, the operator sets the rule students are told (Open question 6) before the tag.
- Run `--source file` on both editors now, `--source git` before the tag, and `--source registry` after the OpenUPM build (Step 15).
- **Tests:** the scripts themselves, plus the two PlayMode suites.
- **Verify:** `[automated]` pass on both editors per source; `[manual]` look at the screenshots.
- **Depends on:** Steps 7 and 8.

### Step 10: Share from the student's copy
- **MODIFY** `Tools/share_check.py`: add `--project PATH` and `--scene PATH` so it can share a scene other than the bench. Keep the old defaults and every existing check. With `--project`, it copies that project to a scratch folder in `Tools/compat/` and shares from there (it deliberately flips every pinned WebGL setting first, which must never touch the committed fixture), and asserts `git status --porcelain Tools/fixtures` is empty afterwards.
- Reason: a Share of a scene under `Assets/Samples` or in the student's copy is untested today (the script hard-codes the bench paths).
- **Tests:** run on both editors against the fixture project (Step 9).
- **Verify:** `[automated]` `python Tools/share_check.py --editor all --project Tools/fixtures/0.1.0 --scene <student copy path>`; look at the `*-share-check-screenshot.png`.
- **Depends on:** Step 9.

### Step 11: Pre-tag checks by hand **[OPERATOR GATE]**
- The operator runs, on a Windows laptop and not the dev machine, following only `install-ko.md`:
    - A fresh project, the steps through Save As, the core gesture, and Share my game. The first sit-through of the steps is the test of the steps.
    - The **Share my game operator editor check** (checklist in `.claude/memory/logs/phases/phase6-log.md` § Operator editor check): the title window, save dialog, done dialog, stop dialogs, a Korean title typed with the IME, one timed first share, and the Gzip Build Profile case. This check moves up to before students try the release.
- Fix anything that reads badly or fails. Then **delete the Deferred.md entry** "Share my game's operator editor check isn't done".
- **Verify:** `[manual]` the operator reports pass or the list of what to fix.
- **Depends on:** Steps 8 to 10 (the steps use the `file` or `git` source until the registry is live).

### Step 12: Deferred.md, Roadmap and ledger edits
- **MODIFY** `Documents/Operations/Deferred.md`:
    - Delete the Share operator check entry (after Step 11 passes).
    - Reword the trigger of "A Rope with no sprite is invisible in Play". It names the default art pack, but the starter ships no Rope. New trigger: when a Rope prefab or a rope look is added to a sample, or the `editor-tools` missing-piece hints land.
    - Gallery backups: apply the operator's answer to Open question 4 (keep the entry, reword its trigger, or fire it).
    - Add entries for any leftovers (each with a concrete trigger).
- **MODIFY** `Documents/Operations/Roadmap.md`: delete the `first-release` milestone section (its work moves to §0's table). In Milestone 6, cut to what's left of "the starter sample" wording (the starter exists; `editor-tools` extends it, for example with new prefabs). In Milestone 7, remove "and the first OpenUPM release" and the default-art-pack wording that this phase delivers; leave the rest. In §2, delete the "Default art pack licensed" gate if the Step 6 test proves it.
- **Verify:** `[grep]` nothing in the Roadmap or Deferred still describes work this phase did.
- **Depends on:** Step 11.

### Step 13: Run Tests
- `python Tools/unity_tests.py` (both editors, both modes). Inspect `TestResults/*-render-smoke.png`, `*-movement-*.png`, `*-traps-*.png` and the new starter pictures.
- `python Tools/share_check.py --editor all` and with `--project` (Step 10).
- `python Tools/install_check.py --editor all --source file`.
- `git lfs ls-files` shows no file under `Packages/`.
- Fix failures. Do not proceed to review until green.

### Step 14: Code Review
- Run the code-review agent (`.claude/agents/workflow/code-review.md`) on all files changed in this phase. Fix HIGH and MED issues and re-run the affected tests. Report the result. This comes before anything is published.

### Step 15: Tag and OpenUPM listing **[NEEDS APPROVAL AT RUN TIME]**
Order: commit and push `main` → Git URL test → tag → submit → registry test. Ask the operator before each outward-facing action, naming exactly what it will do.
1. **Push `main` to `origin`** (public repo: makes the license, starter and docs public). Approval needed.
2. Run `Tools/install_check.py --editor all --source git` against the pushed commit. OpenUPM asks for this to work first.
3. **Push the `0.1.0` tag.** Approval needed. The tag must equal `package.json` version `0.1.0`; check with `git describe --tags` and the `--source registry` parity check. Only class-ready versions get a tag, because Package Manager offers every stable tag as an Update.
4. **OpenUPM submission** (form at `openupm.com/packages/add`). Claude prepares the values:
    - Repository URL.
    - Package name `com.orcapafea.daedalus`.
    - Subfolder `Packages/com.orcapafea.daedalus`.
    - OpenUPM's docs say subfolder packages are auto-detected; confirm in the form.
    - The operator submits it. The submission opens a PR from the operator's GitHub account. OpenUPM's docs do not say listing is free; check that at the form before submitting and tell the operator.
5. First-time approval is aimed at within 24 hours; the build takes 15 to 30 minutes.
6. Run `python Tools/install_check.py --editor all --source registry`. This is the final proof. It does not use a `file:` path.
- **Verify:** `[automated]` the registry install plays the starter on both editors; `[manual]` the operator installs by hand once from the registry.
- **Depends on:** Steps 11 to 14.

---

## Operator check list

**School visit around 2026-11-11 (ISS-002).** These do not depend on the rest of this phase. Run them there with the checklist that already exists:
- Games start within 15 s from the school network (`Tools/gallery_load.py`, or by hand with a stopwatch).
- Share my game with a **Korean IME title** and a **timed first share** on a school laptop with 6000.0.
- Whether `package.openupm.com` is reachable from the school network. Also check `packages.unity.com`: Package Manager needs it for Input System and uGUI, not just OpenUPM. And whether Unity Hub and the editor download there.
- If the school blocks OpenUPM, the plan B is an open question (see Open question 5).

**Before tagging:** Step 11's hand check of the install steps and the Share editor check.

**After the registry build:** install once by hand from the registry on a clean machine.

---

## Completion Criteria

- [ ] Scene autosave appears nowhere outside Changelog history and archived phases.
- [ ] `package.json` has `license`, `repository`, `documentationUrl` and `samples`; `LICENSE.md` is MIT; the metadata tests pass on both editors.
- [ ] The starter was built by `Tools/build_starter.py` on 6000.0; running it twice makes no diff; every prefab, tile and sprite matches the frozen list in GUID and content.
- [ ] The Falling Ceiling prefab's zone reaches the floor below it, and the PlayMode run proves it fires.
- [ ] The starter opens and plays on both editors; each of three sections is reachable from a Checkpoint; the "your turn" object has no Trap; the level is clearable in about 30 seconds.
- [ ] One Tilemap named Collision; MainCamera; Global Light 2D; Player with Respawn at checkpoint; no Player or Rope prefab.
- [ ] Fake Floor ships as a None-kind tile on Collision; a test proves it draws, doesn't block and isn't safe ground.
- [ ] The art pack's CC0 license was checked at the source, the operator approved the pack, and the license file sits beside the art.
- [ ] `git lfs ls-files` shows nothing under `Packages/`.
- [ ] README says what works and what doesn't; `install-ko.md` exists and the operator has followed it on a clean laptop.
- [ ] The fixture project (`Tools/fixtures/0.1.0`) opens and plays on both editors; Share works from the student's copy on both editors.
- [ ] The re-import test ran with a real 0.1.1 and a changed value, both import options; the results are recorded and, if either can break or change a copy, the operator has set the rule.
- [ ] `git status --porcelain Tools/fixtures` is empty after `install_check.py` and `share_check.py --project`.
- [ ] The Share operator editor check passed and its Deferred entry is deleted.
- [ ] Tag `0.1.0` matches `package.json`; the OpenUPM listing is live; `install_check.py --source registry` passes on both editors.
- [ ] GDD bumped and Changelog entry written in the same commit as the work; Deferred.md and Roadmap updated per Step 12.

---

## Notes to GDD

**Decisions (settled by the operator; ADR rows are in `decisions.md`; put the facts in the GDD):**
1. MIT license, on the package folder only (not the gallery server, docs or dev project).
2. The first tagged version is `0.1.0`.
3. OpenUPM listing, so students install from the registry.
4. A starter sample imported from Package Manager, with a CC0 art pack and its license file.
5. **Scene autosave is cut, not deferred.** Students work in the normal Unity Editor and save themselves. Update-safety rules stay.
6. Share my game's operator editor check moves up to before students try the release; its Deferred entry is deleted when done.
7. Not in this phase and stay in their milestones: animation sets, tileset templates, moving Share my game into the daedalus window, play-from-here, custom inspectors, the validation list.
8. Design-panel recommendations built in: the starter design (open-and-play, three sections, one Tilemap Collision, Player placed with Respawn at checkpoint, the unfinished "your turn" stretch); the prefab list; Fake Floor as a None-kind tile on Collision, shipping as a tile and not a prefab; about three tiles; the Save As habit; frozen sample pieces (GUIDs and content; a new behaviour is a new prefab under a new name); no `Daedalus → Open Starter` menu item; Korean install steps with English menu names in `Documentation~/install-ko.md`; `.gitattributes` exception for `Samples~`; generated on 6000.0; the student-shaped fixture; tag only class-ready versions.

**Open questions for the operator.** Questions 1 to 5 were **settled by the operator on 2026-11-09 as recommended here** (for question 3, with the reviewer's added sentence; ADR rows in `decisions.md`). Questions 6 and 7 wait for the phase.
1. **Combat and saved levels.** Combat makes hazards cost HP, and HP 0 always respawns at the checkpoint (systems.md:57). A saved 0.1.0 troll level could change on its first update. Recommend: a Player setting "Hazard damage" defaulting to all HP, and Players saved at data version 1 upgrade to that value. Rule the combat phase would inherit: a level saved before combat plays exactly as before. Settle before the tag.
   Reviewer recommends: the same, settled before the tag because Open question 3's promise depends on it (round 1, F10).
2. **Which one editor version students install.** Recommend 6000.0.85f1. It matches the Share check and the compat suite. Unverified whether Hub lists it directly; the steps carry the archive link.
   Reviewer recommends: 6000.0.85f1, the version the compat suite, `share_check` and the fixture are made on; the archive link covers Hub not listing it (round 1, F11).
3. **What 0.1.0 promises in writing.** Recommend: "Levels you save in 0.1.x keep opening and playing the same in later 0.x updates. Settings you tuned keep their values. New features arrive switched off or at values that don't change an existing level. Anything we can't keep, we tell you in the update notes before you update." Feel numbers are not promised.
   Reviewer recommends: the same wording plus "Starter pieces you placed keep their settings", true once the content freeze in Step 7 holds (round 1, F12).
4. **Gallery backups on the same disk** (Deferred.md, trigger "before the classroom launch"). Does an early student try-out fire it? Recommend no, if students are told in writing that try-out games may be deleted. Otherwise fire it (copy the nightly backup to a second place).
   Reviewer recommends: no for the try-out, with that written notice; fire it before the classroom launch as the trigger says (round 1, F13).
5. **If the school blocks `package.openupm.com`.** Plan B is open: tarball install from a GitHub release (no Git needed), or the Git URL (needs Git on every machine). Not decided.
   Reviewer recommends: a tarball from the GitHub release, with the `.tgz` copied into the project's own `Packages/` folder before installing, so the manifest holds a project-relative path that survives copying the project between machines (round 1, F14).
6. **Re-import hazard.** If the Step 9 test shows a second sample import can break or change a student's saved copy, the operator sets the rule students are told.
   Reviewer recommends: wait for the Step 9 result; if it can break or change a copy, one rule: don't edit anything inside `Assets/Samples`, and when updating, import the sample with the option the test proved safe (round 1, F15).
7. **Package-version vs GDD-version.** The package is `0.1.0`; the GDD is v0.23 and moving. Write one sentence in the GDD that the two number lines are independent.
   Reviewer recommends: the same (round 1, F16).

**Verified facts worth keeping:**
- Fake Floor as a None-kind `DaedalusTile` is supported by the code (`CollisionMapReader.KindOf` maps None to `TileKind.None`).
- `Samples~` is invisible to Unity: the generator seeds from the committed copy, `.meta` files included, to keep GUIDs stable.
- Sample import lands in `Assets/Samples/<display name>/<version>/<sample>`.

---

## Post-Phase Cleanup
After code review passes:
1. Move this prompt to `Documents/Phases/archive/`.
2. Update the phase status in CLAUDE.md. Specifically: the Layout line ("`Samples~/` ... arrives with `editor-tools`" now exists); add `Tools/build_starter.py`, `Tools/install_check.py` and `Tools/fixtures/` to Layout and Build / run; add a row to the Testing table for the starter and install check; update the Distribution line with the OpenUPM facts.
3. Write `memory/logs/phases/phase7-log.md` (include the LFS proof, the re-import test result, the licence URL and date, and the registry install output).
4. Update `MEMORY.md` current state.
5. Update `architecture.md`, `platforms.md`, `Glossary.md` (the Starter entry), `memory/decisions.md` (any ADR rows from open questions settled during the phase), `memory/lessons-learned.md` (`Samples~` is invisible to Unity; LFS vs Git-URL installs) and `memory/framework.md` (Sample import path and GUID behaviour).
6. **Deferred items.** The Share check entry is deleted (Step 11); the Rope trigger is reworded; add entries for any leftover, each with a trigger.
7. Commit all phase work with the `vX.YY:` prefix. The tag and OpenUPM submission follow only with the operator's approval (Step 15).

## Workflow
Execute steps sequentially. Proceed to the next step without waiting for user approval, except at the **[OPERATOR GATE]** and **[NEEDS APPROVAL AT RUN TIME]** markers, which always stop and ask. If blocked, stop and report: what step, what was attempted, what failed.

---

## Review round 1

---
slug: phase7-first-release
round: 1
verdict: changes-requested
reviewer: claude-cowork
updated: 2026-11-09T14:45:00+09:00
blocking: 2
recommendations: 7
---

# Review round 1: Phase 7 First Release

## What I could run

Shell over the connected folder (`C:\Users\brisa\Documents\Projects\games\orcarin\daedalus`): `grep`, `sed`, `git log/status/tag/remote`, reading files. I did not run Unity, the test suites, a build, `git check-attr`, or anything on OpenUPM. One HTTP call from the cloud container to the GitHub API confirmed the repo is public.

## Verified

- Step 1 citations: CLAUDE.md:51, architecture.md:12, systems.md:152, platforms.md:32, Glossary.md:17, audit SKILL.md:81 each hold the autosave text the step says. The Verify grep today matches exactly those six lines (outside Changelog/decisions/the phase/review files). systems.md:57 holds the combat line Open question 1 quotes.
- `package.json` is already `0.1.0`, author `orcapafea`, no license/repository/samples. README line 5 says "Work in progress: no gameplay yet." No git tags exist yet (`git tag` is empty), so the first tag OpenUPM sees is `0.1.0`. `origin` is `brisa0730/game_daedalus`, already public.
- `.gitattributes`: `*.png lfs` is in the Images block; the package has no PNGs today.
- `OnHazard` default is `ReturnToSafeGround` (PlayerHealthSection.cs:12); `LevelStepper` uses the checkpoint only when `Rule == RespawnRule.Checkpoint` (LevelStepper.cs:344-350), so "the default makes Checkpoints do nothing" holds. The bench sets `health.onHazard` (TrapTestSceneBuilder.cs:293).
- `CollisionMapReader.KindOf` maps a DaedalusTile to its own kind (CollisionMapReader.cs:100-103); `TileCollisionKind.None = 0` exists. Safe ground needs a Solid/Climbable/OneWay cell underneath (TileCollision.cs:322), so a None tile is not safe ground. No runtime code hides the Collision Tilemap's renderer, so a None tile on Collision draws.
- `AddComponentMenu("Daedalus/Trap")` (Trap.cs:13). Runaway Exit offset (6,0) (TrapTestSceneBuilder.cs:77). Zones are stored relative to the object (TrapLook.WorldZone, TrapLook.cs:137-145).
- Bench import code sits inside `SaveSprite` (427-460; the importer lines are 443-459) and the palette in `SavePalette` (480-506). Close enough to the cited ranges.
- Dev builders have no asmdef and each carries its own `Require`/`Property` helpers, so copying one `StarterBuilder.cs` alone into the compat project compiles if it follows that shape.
- `ensure_compat_project` uses `com.unity.template.2d-cross-platform-*` (unity_tests.py:78) and sets `testables` to the package and the Input System (unity_tests.py:94-97). Package files are read through `PackageFiles` (resolved path), so `Samples~` tests can reuse it.

## Not verified (taken on trust or left to the Author)

- OpenUPM's handling of LFS, auto-detection of subfolder packages, and whether listing is free. Package Manager's behaviour when a newer version's sample is imported (see F2, F6). Whether "Universal 2D" in Hub is the `2d-cross-platform` template (F7). The Kenney pack sizes (F9).

## Findings

### F1: The Falling Ceiling prefab, as specified, never fires
- **Type:** blocking · **Category:** accuracy · **Location:** Step 7, Prefabs bullets
- A trap's default zone is its box **grown upward** by 1 tile (`LevelRules.DefaultZone`, LevelRules.cs:71-74; `DefaultZoneGrowUp = 1`, :18). Step 7 says zones come from `TrapLook.DefaultZoneLocal` and each prefab ships "with their defaults", and also that the Falling Ceiling is "placed high; its zone reaches down". Both can't be true. Built with the defaults, a ceiling placed high has a zone above itself that the player never enters, and section 2 can't fire its trap. The bench gets its downward zone from a custom world rect (`Rect.MinMaxRect(62, 1, 65, 10)`, TrapTestSceneBuilder.cs:66), which the step says not to copy.
- **Fix:** give the Falling Ceiling prefab an explicit local zone that reaches down a stated number of tiles (e.g. 9 to the floor of the starter's section), say what happens when a student places it at another height (the zone no longer meets the floor), and put that in the student steps in place of "place it high". Rewrite "A Falling Ceiling dropped at floor height is already down", which I can't map to any behaviour.

### F2: Freezing GUIDs does not freeze what a student's level gets from the prefabs
- **Type:** blocking · **Category:** design · **Location:** Step 7 (GUID-freeze), Step 9 (re-import), Open question 3
- A student's saved level holds prefab *instances* of `Assets/Samples/Daedalus Platformer Maker/0.1.0/Starter/Prefabs/*`. Every setting the student didn't override comes from the prefab asset. The GUID-freeze test keeps the references alive but lets a later version change a Drop Floor's delay, a Pop Spike's size or a zone. Once the student imports the newer sample in a way that replaces the old copy (or Unity resolves the shared GUID to the new copy, which Step 9 says isn't predictable), their level changes with no warning. That breaks Architecture rule 4 ("never silently change a saved project") and the promise Open question 3 proposes.
- **Fix:** freeze the content as well as the GUIDs. Either a test that fails when any serialized value in a frozen prefab or tile changes after the tag, or the rule "a frozen sample prefab never changes; a new behaviour is a new prefab under a new name". Run Step 9's re-import test with a value changed in the second version so it detects this case, not only missing references.

### F3: The `.gitattributes` rule makes every sample file binary, not just the art
- **Type:** non-blocking · **Category:** design · **Location:** Step 4
- `Packages/com.orcapafea.daedalus/Samples~/** -filter -diff -merge binary` also matches `Starter.unity`, the prefabs, tiles, every `.meta`, `LICENSE.txt` and `SOURCE.txt`. `binary` is `-diff -merge -text`, so those YAML files lose `text=auto` normalisation and show as "Binary files differ". That defeats the reviews this phase relies on: Step 5's "no `git diff` on the second run" can say only *that* something changed, and a GUID change in a `.meta` can't be read in a diff.
- **Fix:** scope the rule to image types, e.g. `Packages/com.orcapafea.daedalus/Samples~/**/*.png -filter -diff -merge binary` (one line per image extension the pack ships). Add `git check-attr diff text -- Samples~/Starter/Starter.unity` to the Step 4 proof to show YAML stays text.

### F4: The clean project won't compile our test assemblies unless it marks them testable
- **Type:** non-blocking · **Category:** accuracy · **Location:** Step 9, `install_check.py` items 2 to 5
- Item 2 writes a manifest with only the package source. Tests in a package compile only when that package is in `testables`, and our PlayMode suites need `com.unity.inputsystem` testable too, for `InputTestFixture` (unity_tests.py:94-97 does both). Without that, item 4 fails with "ignored/zero tests" and doesn't explain why. The same applies to the fixture project, and the plan doesn't say how the fixture's manifest points at the package (a relative `file:` path to the repo's package folder, so it tests the current package?).
- **Fix:** state both: `testables` in the clean and fixture manifests, and the fixture's package reference.

### F5: Running the checks against `Tools/fixtures/0.1.0` will modify the committed fixture
- **Type:** non-blocking · **Category:** design · **Location:** Step 9 item 5, Step 10
- Opening the 6000.0-made fixture in 6000.6 rewrites `ProjectVersion.txt` and `packages-lock.json` and may re-serialize assets. `share_check.py` deliberately flips every pinned WebGL setting before building. Either way the fixture stops being the 0.1.0 student project it is meant to stand for, and leaves a diff in the repo.
- **Fix:** both scripts copy the fixture to a gitignored scratch folder and run there. Assert that `git status --porcelain Tools/fixtures` is empty afterwards.

### F6: Simulating the re-import with a folder copy skips Package Manager's own path
- **Type:** non-blocking · **Category:** design · **Location:** Step 9, Re-import hazard test
- Copying the folder models only "both versions present". From memory (please verify on 6000.0), Package Manager's Import for a newer version offers to replace earlier imports (`Sample.Import` with `ImportOptions.OverridePreviousImports`), and that case is the one F2 bites.
- **Fix:** test both cases with a real second version (a local `0.1.1` bump installed from `file:`), import through `Sample.Import` with and without the override option, and record what students should click.

### F7: The clean install test may not use the template the students are told to pick
- **Type:** non-blocking · **Category:** accuracy · **Location:** Step 8 item 2, Step 9 item 1
- `install_check.py` reuses `com.unity.template.2d-cross-platform-*` (unity_tests.py:78). `install-ko.md` tells students "Universal 2D". If those are different Hub templates on either editor, the clean test isn't testing the students' path. The starter's Global Light 2D is a URP type, so a project from a Built-in 2D template opens the sample with a missing script.
- **Fix:** confirm which Hub name maps to that template on both editors, and name it in the steps. Add one README line saying the Starter needs a URP 2D (Universal 2D) project.

### F8: A seeded-then-copied-back sample never loses a file
- **Type:** non-blocking · **Category:** design · **Location:** Step 5, Seed first
- The generator seeds `Assets/StarterBuild/` from `Samples~/Starter/` and then copies the result back. A sprite or prefab the generator stops making stays in the seed and is copied back on every run.
- **Fix:** the copy-back mirrors: it deletes files in `Samples~/Starter/` that the build didn't produce (or the generator deletes unlisted assets before saving). SelfCheck asserts the file list.

### F9: One unverified pack candidate may not be 16 px
- **Type:** non-blocking · **Category:** accuracy · **Location:** Step 2
- From memory (unverified), Kenney's *Pixel Platformer* is 18 px, while *Tiny Town*, *Tiny Dungeon* and the *1-Bit* pack are 16 px. The step already says to check. Note it so an 18 px pack isn't shortlisted and later rescaled.

### F10: Open question 1, combat and saved levels
- **Type:** recommendation · **Category:** design
- Agree with the recommendation: a "Hazard damage" setting that defaults to all HP, and an upgrade that gives Players saved at data version 1 that value. Settle it before the tag, because OQ3's promise depends on it.

### F11: Open question 2, which editor students install
- **Type:** recommendation · **Category:** design
- 6000.0.85f1. It is the version the compat suite, `share_check` and the fixture are made on. The archive link covers Hub not listing it.

### F12: Open question 3, what 0.1.0 promises
- **Type:** recommendation · **Category:** design
- Agree with the wording, and add "Starter pieces you placed keep their settings". That sentence is only true once F2 is fixed.

### F13: Open question 4, gallery backups
- **Type:** recommendation · **Category:** design
- No for the try-out, provided students are told in writing that try-out games may be deleted. Fire it before the classroom launch, as the trigger already says.

### F14: Open question 5, if the school blocks OpenUPM
- **Type:** recommendation · **Category:** design
- A tarball from the GitHub release, with the `.tgz` copied into the project's own `Packages/` folder before installing, so the manifest holds a project-relative path that survives copying the project folder between machines. Git URLs need Git on every school laptop.

### F15: Open question 6, re-import rule
- **Type:** recommendation · **Category:** design
- Wait for the Step 9 result (with F6's two cases). If it can break or change a copy, tell students one rule: don't edit anything inside `Assets/Samples`, and when updating, import the sample with the option the test proved safe.

### F16: Open question 7, two version lines
- **Type:** recommendation · **Category:** design
- Agree: one GDD sentence saying the package's semver and the GDD's vX.YY are independent.

## Reviewer's view

I would build this phase once F1 and F2 are fixed. F1 is a spec contradiction that would leave section 2 dead. F2 is the one gap that matters for student projects over time: the GUID freeze protects the references but not the values students' levels inherit. F3 to F8 are cheap tightenings to scripts and rules that otherwise make the phase's own checks unreliable (F3, F5) or fail without a clear cause (F4, F7).

---

## Review round 2

---
slug: phase7-first-release
round: 2
verdict: approved
reviewer: claude-cowork
updated: 2026-11-09T14:40:00+09:00
blocking: 0
recommendations: 0
---

# Review round 2: Phase 7 First Release (delta)

## What I could run

Shell over the connected folder: `sed`/`grep` over the doc and `log.md`. I didn't run Unity, tests, or `git check-attr`. The Author reports reproducing F3 in a scratch repo, and reading `Sample.ImportOptions` and the template `displayName`s from the editors (F6, F7). I accepted those on trust.

## Round 1 findings

- **F1: closed.** Step 7 gives the Falling Ceiling an explicit local zone from its box down 9 tiles, which matches the bench (trap at y 10, zone y 1 to 10, TrapTestSceneBuilder.cs:66). It says what happens at another height. Step 8 item 7 tells students to check the box and drag its handle. There is a completion criterion. "Already down" and "with their defaults" are gone.
- **F2: closed.** `StarterFrozen.txt` (path, GUID and SHA-256 per piece), the rule "a frozen piece never changes, a new behaviour is a new prefab", and a re-import test that changes a value. Leaving the scene out is right: a saved copy doesn't reference it. One seam remains, filed as F17.
- **F3 to F9: closed** as folded. Step 4's rule now covers images only, with the scene check added. Testables and the fixture reference are stated. The fixture and Share runs use scratch copies under `Tools/compat/`, with a `git status` check afterwards. The re-import test runs both ways on a real 0.1.1. Template parity is recorded, and so is the README line. The sample copy-back now mirrors. Step 2 now says to measure tile size.
- **F10 to F16:** recorded beside Open questions 1 to 7. Nothing further.

## New findings

### F17: The content freeze and the generator collide after the tag
- **Type:** non-blocking · **Category:** design · **Location:** Step 5 (`build_starter.py`), Step 7 (Freeze GUIDs and content)
- The freeze hashes each piece's bytes, and `build_starter.py` re-saves every tile, prefab and sprite on each run (they are "reused by path", but saved again). After the tag, `--refreeze` only adds entries. A later run on a different 6000.0 patch, or one where Unity re-serializes a prefab (a `serializedVersion` bump, a reordered field), changes the bytes without changing any value. The freeze test then fails and can't be refrozen, and the only way out is to rename a piece students already use.
- **Fix:** once `0.1.0` is tagged, the generator never rewrites a frozen piece. It copies the frozen file from the seed untouched and builds only new pieces and the scene. One sentence in Step 5, plus the same as a SelfCheck assertion.

### F18: The fixture can't be made from the registry before the registry exists
- **Type:** non-blocking · **Category:** accuracy · **Location:** Step 9, fixture bullets and item 5
- The committed fixture manifest is "the package at 0.1.0 from the scoped registry". The fixture is made in Step 9, before the OpenUPM build in Step 15, so its creation script has to install from `file:` and then rewrite the manifest. Say so, and say whether its `packages-lock.json` is committed. A lock written during the `file:` install would name a path that exists only on the dev machine.

## Late-pushback test

Asked "what do you think?", I would add only F17 and F18. Both are filed above.

## Reviewer's view

Approved. Both blocking findings are fixed in the text, with tests behind them (F1's completion criterion, F2's frozen list and value-changing re-import test). Fold F17 before Step 5 is built, because it decides whether the freeze is livable after the tag. F18 is a wording fix.
