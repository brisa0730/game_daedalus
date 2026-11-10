---
slug: stages-release
round: 2
verdict: approved
reviewer: claude-cowork
updated: 2026-11-10T14:35:00+09:00
blocking: 0
recommendations: 0
---

# Round 2: stages-release (delta review)

## What I could run, and what I verified

I had the same access as in round 1: `device_bash` over the office PC repo, and no Unity, tests, build or browser. This is a delta review. I read `log.md`'s round 1 dispositions and diffed the document against my saved round 1 copy. Every changed line was read against the code it cites.

## Round 1 findings

- **F1 (blocking): closed.** `--expect-chain TEXT` matches the real log line. `ShareBuild.cs` :158 writes `Share my game: {walk.Chain} → {zipPath}, …`, so the line for the fixture starts `Share my game: My Level → My Level 2 → win → `, and a prefix match on `TEXT → ` holds. `run_share` already reads each run's own `-logFile` as UTF-8 with `errors="replace"` (:394), and the arrow survives that. An explicit flag is better than my "detect a linked Exit" suggestion, because detection would itself have been a guard that can fail open. `check_stages` correctly stays bench-only, and the reason is written in Step 1.
- **F2 (blocking): closed.** The release list is local ∪ origin, an unreachable origin exits, and a tag that is only on origin exits with the fetch hint `read_frozen` needs. One consequence, which I accept and record here rather than raise: every `install_check` and `build_starter` run now needs origin, where today a run after the tag works offline. That matches the project's existing choice (`release_tag_exists` already refuses to guess), and both tools need the network anyway for the registry and template work.
- **F3 (blocking): closed.** The branch now depends on whether the `My Level 2` file exists, and it stays in the one test with no `Assert.Ignore`. With the file, `Exit.Link` is `Scene` to it and `My Level 2`'s own Exit is unlinked, which also catches the self-loop in F4. Without the file, the Exit must be unlinked, which holds for 0.1.0: its Exit was saved before `nextStage` existed and reads `""`, which is `ExitLink.None` (`StageLoad.cs`). The Completion Criterion now points at this test.
- **F4: closed.** The order in Step 3 matches guide § 9 :79–82 (save, duplicate, link the first, save), and the link is written through `SerializedObject` with the GUID.
- **F5: closed.** `TrapDemoPath` goes into `Produced` before `DeleteWhatWasNotProduced()` (StarterBuilder.cs :138–143). The new picture is unlinked at the same two places as the old one (build_starter.py :142, :186).
- **F6: closed.** The order is now push `main`, tag locally, run the three checks at the tag, then push the tag.
- **F7: closed.** All six points are in the manifest, Step 2 or Post-Plan Cleanup. The Deferred.md rewording covers both the trigger and "ships in 0.1.1".

## Seams checked

- Step 1's install_check row, Step 3's fixture script and FixtureProjectTests agree on the file name `Assets/My Level 2.unity`.
- Step 4's command and the new Completion Criterion pass the same chain text.
- The F6 reorder doesn't disturb Step 7's earlier rule that the tagged commit must already hold the bump, the fixture and the frozen list.
- No new findings.

## Reviewer's view

I would build this as written. F1–F3 are fixed in ways that close the holes rather than move them, and the folds introduced nothing new. What remains open belongs to the operator, and the plan already puts each at the right point: the Trap Demo play-check, the OK before each commit and push, and the hand check before students are told.
