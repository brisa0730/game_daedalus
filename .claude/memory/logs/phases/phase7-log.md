# Phase 7 log: `first-release` (v0.24, 2026-11-09)

Moved ahead of combat so students can try what exists. Prompt: [phase7-first-release](../../../../Documents/Phases/archive/phase7-first-release.md).

## What shipped
MIT on the package folder; `0.1.0` metadata; README as the listing; `Documentation~/install-ko.md`; the Starter (`Samples~/Starter`, 16 files) from `Assets/Dev/Editor/StarterBuilder.cs` via `Tools/build_starter.py`; `Tools/install_check.py`; `Tools/fixtures/0.1.0`; `share_check.py --project/--scene`.

## Changes from the prompt
- **No art pack.** The operator kept the plain generated art (rectangles for ground and player); Step 6 became tiles only. Three CC0 packs were shortlisted and not used.
- **One Exit.** Touching any Exit wins, so section 3 is the "your turn" stretch followed by the Runaway Exit, not a Runaway Exit followed by a second Exit.
- **Dev editor 6000.6.8f1.** The operator installed it with WebGL and 6.7 was gone from this machine.
- **Hand check not run** (Step 11): the operator chose to move on. Deferred.md. The install guide's screenshot waits for it.

## Evidence
- **LFS proof (Step 4).** `git check-attr filter` on a Starter PNG: unset; on `Assets/Dev/TrapTest/Ground.png`: lfs. On `Starter.unity` and a `.meta`: `diff: unspecified`, `text: auto`. `git lfs ls-files | grep -c Packages/` = 0.
- **Generator.** Normal, repeat and `--as-tagged` runs byte-identical across the sample. 30 frozen rows (15 pieces with their `.meta`, the palette included).
- **Suites.** 488 EditMode and 87 PlayMode (85 passed, 2 skipped: the Starter and fixture tests, which need their own projects) on 6000.0.85f1 and 6000.6.8f1.
- **Install.** `install_check.py --editor all --source file`: playthrough and fixture PASS on both. The package `.tgz` too, on 6000.0. Git URL and registry not run yet (they need the push).
- **Share from the student's level.** 6000.0.85f1: build 462 s, zip 11,740,673 bytes. 6000.6.8f1: zip 11,467,014 bytes, screenshot shows the level. Run with `--no-gallery-check` (no cargo on this PC), so the gallery's `check_share_zip` hasn't seen a Starter-based zip.

## Re-import test (Step 9, Open question 6)
A real 0.1.1 from `file:`, with Drop Floor's delay changed from 0 to 5, over a saved level, on 6000.0:
- `ImportOptions.None` (HideImportWindow only): `imported=False`; folder stays `0.1.0`; level untouched (`delay=0`, source 0.1.0).
- `OverridePreviousImports`: `imported=True`; folder replaced by `0.1.1`; nothing missing; **the level's Drop Floor now has `delay=5`**, source 0.1.1.

So re-importing never breaks a level saved outside `Assets/Samples`, and it would pass a prefab change through: the freeze prevents that. No extra rule for students beyond the guide's line not to edit inside `Assets/Samples`, which an override replaces.

## Review
Handshake: 2 rounds, approved (F1 ceiling zone and F2 content freeze blocking, both fixed). Code review: PASS WITH FLAGS (1 HIGH, 5 MED, 9 LOW). Fixed: freeze baseline from the tag and tag detection through origin; a completeness test; palette frozen; tag parity for registry installs; `--make-fixture` refused after the tag; per-trap fixture asserts; the LOW fixes in scripts and guide. Not done: the guide's screenshot (Deferred.md); a guard that the fixture's sample matches `Samples~` before the tag (a recommendation; regenerate the fixture if the Starter changes before tagging).

## Scene for the next session
A Python heredoc in a Bash call can fail on quoting; a patch script in the scratchpad is safer for multi-file edits.
