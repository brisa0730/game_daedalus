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
