# Handshake log: stages-release

## Opened 2026-11-10T14:19:09+09:00 (Author)

**Document:** `Documents/Plans/stages-release.md` (plan, draft; nothing is built).

**Proposes:** publishing package 0.2.0, the first version students will install (none has 0.1.0). It adds a Trap Demo scene to the Starter sample: the dev trap bench's layout without its four adversarial fixtures, built by the Starter generator from the Starter's own pieces. It makes the release tools handle several releases: tags as a list, a fixture per release, and the frozen-piece baseline as the union of every tag's list. It saves a two-stage 0.2.0 fixture, writes a release checklist into CLAUDE.md, then commits, pushes and tags with the operator's OK at each, and checks the install from the registry.

**Already reviewed:** a critic sub-agent pass at draft time (3 HIGH, 4 MED, 5 LOW), folded before this round: the update-flow note corrected, the fixture test scoped to link-and-load, the frozen-list union, a fixture required for the current version, Share run on the new fixture, `--source git --ref 0.2.0`. The Trap Demo step (Step 0) was added after that pass and has not been reviewed.

## Round 1 dispositions (2026-11-10T14:26:40+09:00, Author)

Every cited line was re-read before accepting: share_check.py :143–151 and :457 (bench only, no stage check with `--project`), ShareBuild.cs :158 and StageWalk.cs :31 (the chain line; `HelpTexts.StageName` is the file name without extension, so the chain reads `My Level → My Level 2 → win`), share_check.py :384–396 (each run has its own `-logFile`, so the line can be read), install_check.py :145–152, build_starter.py :135/:142/:186, StarterBuilder.cs :136–145, AssemblyInfo.cs (InternalsVisibleTo names only our three assemblies), Glossary.md :11, CLAUDE.md :135.

- **F1 (blocking): accepted.** `check_stages` stays bench-only. New `--expect-chain TEXT` with `--project` requires the build log's `Share my game: TEXT → <zip>` line. It is an explicit flag rather than detection from the scene, so it can't fail open. Step 4 passes `--expect-chain "My Level → My Level 2 → win"`. Manifest row, Step 1, Step 4 and a new Completion Criterion changed.
- **F2 (blocking): accepted.** `RELEASES` = local ∪ `ls-remote --tags origin`. An unreachable origin exits; a remote-only tag exits with "Run: git fetch --tags". Step 1 and the manifest changed.
- **F3 (blocking): accepted.** The branch is keyed on `Assets/My Level 2.unity` existing, inside the one test with no `Assert.Ignore`. That level's own Exit must be unlinked; without the file, the Exit must be unlinked. The Completion Criterion now points at this test. Step 3 changed.
- **F4 (non-blocking): accepted.** The order is spelled out: save, `CopyAsset`, set `nextStage` through `SerializedObject` to the GUID (the student assembly can't see internals; it is a prefab-instance override), save. Step 3 changed.
- **F5 (non-blocking): accepted.** `TrapDemoPath` is added to `Produced` in `Generate` before `DeleteWhatWasNotProduced`, and `trap-demo-picture.png` is unlinked at the same two places as `starter-picture.png`. Step 0 changed.
- **F6 (non-blocking): accepted.** Tag locally, then check package.json, the fixture manifest and `ls-tree` at the tag, then push the tag. Step 7 changed.
- **F7 (non-blocking): accepted, all six points.** Bookkeeping line under the manifest (Changelog v0.27/v0.28, GDD.md version line, Glossary `Starter` row, Deferred.md); the Deferred trigger wording in Post-Plan Cleanup; CLAUDE.md :135 in the manifest; `PLAYTHROUGHS` in the install_check row; `--reimport-test` named as the grep's known exception in Step 2.

Fold re-verified: every new line reference re-read. The chain text matches `StageName` plus " → win". The order in Step 3 matches guide § 9 :79–82.

## Round 2 and close (2026-11-10T14:29:28+09:00, Author)

Verdict: approved, 0 blocking, 0 recommendations, no new findings. F1–F7 closed by the Reviewer. Its note on F2 (every `install_check` and `build_starter` run now needs origin) is accepted as stated: `release_tag_exists` already refuses to guess, so nothing new folds.

Close fold: none needed (no open non-blocking findings or recommendations). The document is an approved proposal: **nothing is built**. Operator gates: the go-ahead to start, the Trap Demo play-check, the OK before each commit, the push and the tag, and the hand check before students are told.
