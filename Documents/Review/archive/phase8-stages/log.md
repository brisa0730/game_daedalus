# Handshake log: phase8-stages

## Opened 2026-11-09T22:19:32+09:00 (Author)

**Document:** `Documents/Phases/phase8-stages.md` (Phase 8 `stages`, unstarted; nothing is built).

**What it proposes:** troll games made of stages, one Unity scene per stage. An Exit gets one optional Next stage setting (empty = the win screen as today). A quick fade between stages, with the player and traps held until the new stage is showing. One death total across the game, owned by a run object that exists only in multi-stage games and uses no static state. Share my game follows the links, checks every stage, shows the chain and builds them all, with the zip contract unchanged. One-scene games play exactly as before.

**Already decided by the operator (2026-11-09):** one scene per stage, ahead of `combat-and-enemies`; one death total; a quick fade with no card. Recorded in `.claude/memory/decisions.md`; Roadmap §1 and §3 updated at planning.

**Still the operator's, marked pending in the document:** a chain loop stops Share; Exits with different links warn and all are built; Play again returns to where Play began; no differing-Players message. Also the first `#if UNITY_EDITOR` in Runtime (`StageLoad.cs`).

**Inputs:** design panel (UX Lens, Player Voice, Skeptic, Perf Guardian) and a codebase scan, both folded in by the phase planner. Nothing has been run; every Unity timing and behaviour in the document is a prediction its steps test.

## Round 1 dispositions, 2026-11-09 (Author)

Verdict received: changes-requested, 1 blocking, 9 non-blocking, 5 recommendations. Each was re-checked against the repo; nothing was built or run.

- **F1 (blocking): accepted, changed remedy.** Reproduced from the code: `SavedScenePlayTest` shows the by-path load, and Play again loads by path. Whether a by-path load really ignores unsaved edits is Unity behaviour not runnable in batch; the document now states it and Step 11 checks it. Took option (a) with two corrections. The reviewer's text says to press Ctrl+S during Play, but Unity doesn't save in Play mode, so the line reads "Stop, then press Ctrl+S". And it moves from play start to the win screen, where the confusion happens (`StageRun.FirstSceneUnsaved`, read while the scene is still loaded). `isDirty` in Play is unverified: the Step 11 editor check answers it, and Step 4 names the fallback (an editor prompt that only asks; the editor tools never save for students, decision of 2026-11-09). Added to Step 11's Must be absent and to the Completion Criteria.
- **F2: accepted, extended.** Soft lock confirmed: both the win screen and Play again are gated on `!leaving`. Failed load now covers an empty path, a null operation and an exception; a new `LoadFailed` clears `leaving`. Beyond the reviewer's fix, Play again checks `!run.LoadFailed` and restarts in place, otherwise it would repeat the failing load. The notice text is new because "next stage is missing" is wrong for a failed return to stage 1. Author's fold check found the total would double count on retry, so `GoNext` adds only once the load has started.
- **F3: accepted.** `ScanTilemaps` (`Player.cs:118`) and `PlayStartScan` search every loaded scene. Play-start warning in Step 2, a test, the Step 11 editor check, a Completion Criterion. It lives in the Console like other play-start warnings (Deferred.md, "Play-start warnings appear only in the Console").
- **F4: accepted, all three.** `SavedScenePlayTest : MovementPlayTest` confirmed (`:15`), and `LoadScene` is called from the test (`:25-27`). `captureFramerate = 60` confirmed (`MovementPlayTest.cs:23,33`). Long frame injected with `RunFrame(0.5f)` during the hold, the only place it means anything, since the hold skips the clock. Overlay note added. Noted that its first load is additive, then `Single` through the Exit, so the test re-finds the Player.
- **F5: accepted.** `perf_check.py:87,126` confirmed: no `Content-Encoding`, binds `127.0.0.1`. Step 8 says new code, confirm on first run, print the loader's error.
- **F6: accepted, second remedy.** Player starts several tiles above the Exit for at least 0.5 s of clean frames; the sample rule ignores frames before Unity's first non-empty one and uses a stated tolerance.
- **F7: accepted.** `SimPurityTests.cs:90-91` confirmed stricter than the plan. Grep confirms `PlayHud.Skull` and `Checkpoint.ReachedTint` are the Runtime's only `static readonly` fields. Rule written out, with a guard against checking zero fields.
- **F8: accepted.** Numbered 1–7 in Notes to GDD; Source points there, and Step 10's "decision 7" now resolves.
- **F9: accepted.** `StarterFrozen.txt:15-16` confirmed per-file rows. `PLAYTHROUGH` at `install_check.py:33`, used at `:343`: Step 9 now says list plus a per-test skip check.
- **F10: accepted, accepted the no-camera case.** `PlayHud.cs:63,69,71` confirmed. Written into Step 3 with the reason: Play mode only, one fade long.
- **R1: recorded** in Source with an Author's note that it is the one that changes the design. Operator's decision; the document stays on "a loop stops Share" until then.
- **R2, R3, R4, R5: recorded** in Source.

Fold re-verified: every line reference added above was re-read or re-grepped. Nothing was built.

## Round 2 close, 2026-11-09 (Author)

Verdict received: approved, 0 blocking, 0 recommendations, one new non-blocking finding. The reviewer also corrected its round 1 F1 text (Ctrl+S during Play).

- **F11: accepted, folded at close.** Reproduced: `PlayHud.cs:101` sets the title once at build and `:130` writes the deaths line only when the count changes, so a notice would stay. Step 2 adds `ClearNotice()`, called on every in-place restart, which also covers the Broken-link path the finding didn't name. Step 4 states that earlier stages' deaths stay on the counter after a failed Play again, taking the finding's "state that it carries on" option: almost only reached in the Editor, and no second reset path. One assertion added to the failed-load test.

Close fold re-verified: the three edits are present, and the two `PlayHud.cs` lines were re-read. Round 1's R1–R5 remain the operator's, recorded in Source; R1 is the one that changes the design.

**Nothing was built.** This approves a proposal. No Unity, test suite, build or browser ran in either round; every Unity timing and behaviour in the document is a prediction its steps test. Status: done.
