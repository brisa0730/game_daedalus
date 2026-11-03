# Handshake log: phase6-share-my-game

## Opened 2026-11-03T14:27:40+09:00 (Author: Claude Code)
**Doc:** Documents/Phases/phase6-share-my-game.md (Phase 6 `share-my-game`, GDD v0.16; design-panel reviewed; not yet operator-approved; nothing built).
**Proposes:** the Unity editor command **Daedalus → Share my game**, which turns the active scene into exactly the Share zip the live class gallery accepts (platforms.md § Share zip). That means a pinned-settings Brotli WebGL build inside the scene-list guard, a first-frame screenshot, the title cleaned to match the gallery's `clean_label`, a fail-closed exception watch, plain-English stops and a done dialog. A new `Tools/share_check.py` feeds the editor's zip to the gallery's own Rust checker (new `check_share_zip` example). It also removes the spike build files (Deferred trigger) and moves `BuildScenes` into the package.
**Open questions are marked PROPOSED** in § Open operator questions (audience age conflict, gallery address constant, exception-only check, refuse vs warn, time-warning wording, Data Caching).

## Round 1 dispositions (2026-11-03T14:39:27+09:00, Author)
Verified first: `share_zip.rs:210,227` (`unpacked.title` = `clean_label(raw_title)`), `strings.rs:7` NAV_UPLOAD 업로드 linked from `pages.rs:29` and 게임 올리기 linked only from `gallery.rs:42`, `Tests/PlayMode/TestImages.cs:12` internal, `unity_tests.py:43` 30-min timeout, `CameraFollowTests.cs:88` NoMainCameraMeansNoFollowAndNoWarning, `decisions.md` last row = the prompt-stage Share row.
- **F1 (blocking): accepted.** Reproduced: the old assert compared Rust(C#(raw)) with Rust(raw). Step 8 now compares the zip's raw `daedalus.json` title with `clean_label(raw_file)` byte for byte, plus a second assert that `unpacked.title` equals it. Completion criterion reworded.
- **F2: accepted.** CheckBeforeBuild order fixed (Play/compiling, WebGL module, no Player, no Main Camera); no Untitled check there. The menu saves an Untitled scene only after it passes. `Run` keeps a no-path guard for the batch entry. The tests and the completion criterion match.
- **F3: accepted.** The done text says 업로드. NAV_UPLOAD replaces UPLOAD_TITLE among the copied labels; Step 9.9 checks 업로드 = NAV_UPLOAD; Step 11 says 업로드.
- **F4: accepted.** Remedy chosen: `ShareZip.Write` takes `maxBytes` and checks the partial before replacing (one call, no write/commit split). An oversize share leaves the previous zip untouched. New test case.
- **F5: accepted.** Step 10 adds the throwing `IPreprocessBuildWithReport` check in the compat project's scratch folder, expecting FAIL and no zip. The completion criterion names it.
- **F6: accepted.** `BuildFromCommandLine` calls `AssetDatabase.SaveAssets()` after `Run`.
- **F7: accepted.** `temp.ResetAspect()` after `targetTexture`; `SubmitRenderRequest`/`StandardRequest` named as the fallback if case 1 renders black.
- **F8: accepted.** share_check.py uses a 60-minute build timeout (perf_check's) with run_unity's process-group handling; `--no-gallery-check` for Windows, with check_share_zip run on the Mac.
- **F9: accepted.** The EditMode test carries its own copy of the folder rule.
- **F10: accepted.** `ShareBuild.SharedPlayer()`: first after a deterministic sort; `Take`'s caller passes it.
- **F11: accepted.** One decimal, rounded up (50,000,001 bytes → 50.1).
- **F12: accepted.** Post-Phase step 9 updates the existing row instead of adding one.
- **F13: accepted.** Step 10 also tests a Player Settings override on the active profile, and runs on 6000.0.85f1.
- **R1–R6: recorded** beside each open question in § Open operator questions. R4's corrected camera text is also folded into Step 6, since it fixes an inaccuracy in a message (the runtime treats no Main Camera as normal).
Re-verified my own fold: re-read Steps 3, 5, 6, 7, 9, 10 and 11 and the completion criteria. Only the Run step-1 wording needed a follow-up (no-path guard named), and it is fixed. Nothing built.

## Round 2: approved; closing dispositions (2026-11-03T14:43:22+09:00, Author)
- **F5 (re-opened): accepted.** Verified the premise: an exception thrown from a preprocess callback fails the build, so the round-1 probe would refuse via Outcome's not-succeeded branch and never test the exception count. Step 10's probe now logs `Debug.LogException` without throwing, and asserts the build Succeeded, that the fail line carries the exception-during-build text, and that no zip exists. The completion criterion is reworded to match.
- **F14: accepted.** Reproduced: Run calls CheckBeforeBuild first, so an empty NewScene stops at no-Player. The test scene now has a Player and a MainCamera-tagged camera.
- **F15: accepted.** The Step 12.2 Untitled check now uses a scene with a Player and a Main Camera (prompt, Cancel stops quietly, Save continues). The must-be-absent line reads "a **new** zip after a failed share (an earlier zip at that path stays)".
Re-verified the close fold: re-read the Step 10 probe paragraph, the Step 6 test line, Step 12.2, the must-be-absent line and the completion criterion; the review appendix is untouched. No non-blocking finding or recommendation remains unfolded or unrecorded. The document is approved as a proposal; **nothing was built**. The operator still decides the six open questions (R1–R6 recorded) at phase approval.
