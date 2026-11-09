# Handshake log: phase7-first-release

## Opened 2026-11-09T13:31:44+09:00
Doc: Documents/Phases/phase7-first-release.md. Proposes the first public release of the package ahead of combat: MIT license on the package folder, tag 0.1.0, OpenUPM listing, an open-and-play starter sample with CC0 art, Korean install steps, clean-project install tests on both editors, the first fixture project, and cutting scene autosave from the docs. Nothing is built; the document is the proposal.
Note: the phase's Open questions 1-7 are the operator's decisions, not settled here.

## Round 1 dispositions (2026-11-09T14:32:02+09:00)
Verdict received: changes-requested (2 blocking, 7 non-blocking, 7 recommendations).

- **F1 (blocking): accepted.** Verified: LevelRules.DefaultZone grows the box upward by DefaultZoneGrowUp = 1 (LevelRules.cs:18, 71-74); the bench's downward zone is a custom rect (TrapTestSceneBuilder.cs:66, trap at y 10, zone y 1-10). Step 7 now gives the Falling Ceiling an explicit local zone from its box down 9 tiles, says what happens at another height, and Step 8 item 7 tells students how to fix it with the zone handle (TrapEditor.cs ZoneHandle). Removed "already down". Also replaced "shows only its needed settings, with their defaults". Completion criterion added.
- **F2 (blocking): accepted.** Prefab instances inherit every non-overridden value from the asset, so the reported gap is real. Chose the rule plus a test: StarterFrozen.txt holds path, GUID and SHA-256 of each prefab, tile and sprite with its .meta; a frozen piece never changes, a new behaviour is a new prefab under a new name. The scene is not frozen: a student's saved copy doesn't reference it. Step 9's re-import test now changes a value in the second version.
- **F3 (non-blocking): accepted.** Reproduced in a scratch repo: a Samples/** binary rule sets diff, merge and text unset on a .unity file; the png-only rule leaves it text: auto and diff unspecified, keeps the png filter unset and an ordinary png on lfs. Step 4 rewritten, the scene's check-attr added to the proof.
- **F4 (non-blocking): accepted.** Verified unity_tests.py:96. Step 9 items 2 and 5 now state testables in both manifests and how the fixture references the package (committed: student-shaped, 0.1.0 from the registry; scratch copy: file: absolute path).
- **F5 (non-blocking): accepted.** share_check.py sets every pinned setting to its opposite (lines 13, 48, 88). Both scripts now copy the fixture to Tools/compat/ (verified gitignored) and assert git status --porcelain Tools/fixtures is empty.
- **F6 (non-blocking): accepted.** Verified on 6000.0: UnityEditor.CoreModule.xml documents Sample.ImportOptions None, OverridePreviousImports and HideImportWindow. Behaviour is still unknown, which is what the test is for. Step 9 uses a real 0.1.1 from file:, with both options.
- **F7 (non-blocking): accepted.** Verified: com.unity.template.2d-cross-platform has displayName "Universal 2D" in each editor's template (5.1.0 on 6000.0.85f1; 2d-7.0.0 on 6000.6.7f1). Recorded in Step 9 item 1; README line added in Step 8.
- **F8 (non-blocking): accepted.** The seed-then-copy flow keeps stale files as described. Step 5 now mirrors, deletes unlisted build files, and SelfCheck asserts the list.
- **F9 (non-blocking): accepted.** Step 2 says to measure each pack and never shortlist a non-16 px one. The 18 px figure stays marked as unverified.
- **F10-F16 (recommendations): recorded** beside Open questions 1-7 in Notes to GDD, for the operator.

Fold re-verified: grep of the changed text against LevelRules, TrapLook, TrapEditor, TrapTestSceneBuilder:66, share_check.py and git check-ignore; stale phrases ("GUID-freeze", "place it high", "with their defaults", the whole-folder Samples~ rule) are gone. Nothing was built.

## Round 2 dispositions and close (2026-11-09T14:34:05+09:00)
Verdict received: approved (0 blocking, 2 non-blocking).

- **F17 (non-blocking): accepted.** Consequence holds: the freeze hashes bytes and the generator re-saves what it builds, so a re-serialization alone would fail the test with no refreeze allowed after the tag. Step 5 now says that once the tag exists the generator leaves frozen files as seeded and builds only new pieces and the scene; SelfCheck asserts byte identity.
- **F18 (non-blocking): accepted.** Step 9 now says the creation script installs from file: and rewrites the committed manifest to the student's shape. packages-lock.json is not committed (added to the .gitignore row in the File Manifest).

Close fold re-verified by grep of the three new passages; no line numbers or figures were introduced. Round 2 review appended to the doc. Open questions 1-7 remain the operator's. Nothing was built: the document is an approved proposal, and Unity, tests and OpenUPM behaviour stay to be proven by the phase itself.
