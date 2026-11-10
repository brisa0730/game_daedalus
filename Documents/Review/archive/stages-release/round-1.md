---
slug: stages-release
round: 1
verdict: changes-requested
reviewer: claude-cowork
updated: 2026-11-10T14:40:00+09:00
blocking: 3
recommendations: 0
---

# Round 1: stages-release (full review)

## What I could run, and what I verified

`device_bash` over the office PC repo (`C:\Users\brisa\Documents\Projects\games\orcarin\daedalus`, HEAD `59a2e79`, tag `0.1.0`, the plan untracked). No Unity, no test suites, no build, no Chrome, no network to OpenUPM. I read the plan whole, as it stood at 14:19 (it changed once before the round opened: Step 0 was added; I reviewed the opened version).

Verified against the files:
- `install_check.py` (all 377 lines): `TAG`, `FIXTURE`, `SAMPLE_PATH` (:32–36), `release_tag_exists` (:145–152), `check_tag_parity` (:155–160), `make_fixture` (:258–283), `reimport_test` refusing anything but 0.1.0 (:311–312), `copy_fixture`/`fixture_state` bound to the one `FIXTURE` (:235–255), `ran()` failing on any skip (:328–334). The cited help/docstring lines :11 and :361 are right.
- `build_starter.py` :11 and :123 name 0.1.0 as cited; `read_frozen` reads `TAG`'s list (:87–95). `StarterFrozen.txt`: 30 rows at `0.1.0`, 32 at HEAD, the difference exactly the two `Stage Player` rows, as the plan says.
- `share_check.py` :22 as cited; `prepare_stages`, `check_stages`, `play_stages` and `STAGE_PAGE` (:143–380); `main` never runs `check_stages` with `--project` (:457).
- `Exit.cs`, `StageLoad.cs`, `ExitLink.cs`, `AssemblyInfo.cs` (the PlayMode tests see internals), `ShareBuild.cs` :97–110 and :158, `StageWalk.cs` :33–61, guide § 9 (install-ko.md :75–82).
- Step 0 against `TrapTestSceneBuilder.cs` and `StarterBuilder.cs`: I re-parsed the bench layout. Its 13 `TrapDef`s minus the three adversarial ones leave exactly the ten the plan keeps, and the fourth adversarial piece is `MakeCheckpointTrap`. The Starter prefabs' serialized zones match the bench: Drop Floor (−1.5, −0.5, 3 × 2) and Pop Spike (−1, −0.5, 2 × 2) are the bench's default formula, and Falling Ceiling (−1.5, −9.5, 3 × 9) about (63.5, 10.5) is the bench's (62, 1)–(65, 10). The checkpoint cells 40, 57 and 100 all stand on ground (row 0 runs are 0–4, 8–11, 15–27, 31–44, 48–69, 79–93, 99–119). None is inside a kept zone once A and C take default zones; 100 touches Fast Spikes' zone (x 101–102) only at an edge. The start (x 2.5) is outside Drop Floor A's default zone (x 5–8). The trap boxes all sit over empty cells. `TrapAction` has the four actions and `TrapTrigger` the two triggers, and Hidden Block is the only Touch. `Starter.unity` is unchanged since `0.1.0` (only `Stage Example/` was added), so the fixture test's 6 traps and one Exit still hold. `StarterSampleTests` :84 and :105 are the two places a new scene folder must be added, as the plan says.
- `.claude/CLAUDE.md` :126 and :134 name 0.1.0 as cited (see F7 for :135).

Not verified, taken on trust: that OpenUPM builds a new tag on its own; the operator's statement that no student has installed 0.1.0.

## Findings

### F1 — blocking · accuracy · Step 1 (`share_check.py`), Step 4, Completion Criteria
**The stage checks can't run on the 0.2.0 fixture, so "with `--project`, run `check_stages` when the chosen scene has a linked Exit" fails every time.** `check_stages` doesn't take a project. It calls `prepare_stages` (:143–151), which always shares the dev stage bench, not the `--project` copy. Its Chrome page (`STAGE_PAGE`, :262–310) recognises stages only by the bench's colours. Stage A has to fill over 90% of the samples with blue, then the frame goes black, then Stage B fills over 50% with orange. It also depends on Stage A reaching its Exit with no input (`StageTestSceneBuilder.cs` :12). `My Level` and `My Level 2` are copies of the Starter: navy background, green ground, and a Player that stands still at x 1.5 until someone presses a key. The page would report `stage state stuck at A … saw blue on 0` after 90 s. If the call is wired literally, it re-tests the bench and proves nothing about the fixture.
**Remedy:** for `--project`, don't play it in Chrome. Assert the build's own chain instead. `ShareBuild.Run` logs `Share my game: {walk.Chain} → {zip}…` (ShareBuild.cs :158), and `walk.Chain` is `My Level → My Level 2 → win` (StageWalk.cs :31). So `share_check` can require that line, with two stages and `→ win`, when the scene's Exit is linked. Optionally add an `--expect-chain` argument. That makes Step 4's "(both stages built)" an observed fact. Today nothing checks it: `check_zip` checks the six entries, not the stage count.

### F2 — blocking · design · Step 1 (`install_check.py` `RELEASES`), feeds `build_starter.py`'s union
**"Git tags … (local, else `ls-remote`)" fails open on a clone with stale tags.** This is the case `release_tag_exists` was written to refuse (:145–152: "a clone that hasn't fetched it must not act as if it's free"). With several tags, "local, else remote" uses the local list whenever it is non-empty. On a clone that has `0.1.0` but not `0.2.0`, `RELEASES` is `[0.1.0]`, and three things follow:
- `build_starter.py`'s baseline drops 0.2.0's rows, so the generator may rewrite `Stage Player.prefab` (frozen at 0.2.0).
- The "fixture required for every tag" check passes without one.
- Registry parity compares against the wrong set.

June works on two machines (the office PC and the Mac mini), so a clone that is behind is a real state. `git fetch` auto-follows tags only for commits it fetches, so a clone that hasn't pulled since the tag stays stale.
**Remedy:** `RELEASES` = local tags ∪ `ls-remote --tags origin` (both filtered to `^\d+\.\d+\.\d+$`). If origin can't be reached, exit as `release_tag_exists` does today. For a tag that is listed remotely but missing locally, exit with "Run: git fetch --tags", because `read_frozen`'s `git show <tag>:…` needs it (:95 already has that message).

### F3 — blocking · design · Step 3 (`FixtureProjectTests`)
**"When the level's Exit is linked, assert …" is a guard that fails open (Architecture rule 7).** The 0.2.0 fixture is the only one with a link. If `--make-fixture` doesn't write the link, the test takes the unlinked branch and passes, and it passes for both fixtures. The link can go missing several ways:
- an empty GUID from `AssetPathToGUID`;
- a wrong property path;
- a prefab-instance override that isn't saved.

Nothing else would notice. The Completion Criterion "its `My Level` Exit links to `My Level 2`" has no command behind it, and the fixture can never be regenerated after the tag. This also catches F4's wrong-order case. Separately, if the stage check is written as its own `[UnityTest]` that ignores itself on 0.1.0, `ran()` (:331) fails the 0.1.0 fixture run on the skip.
**Remedy:** key the branch on the second level's file, not on the link. If `Assets/My Level 2.unity` exists, `Exit.Link` must be `Scene` with that path, and `My Level 2`'s own Exit must be unlinked (the game ends there). If the file is absent, the Exit must be unlinked (0.1.0). Keep it inside the one test, with no `Assert.Ignore`. Then put the criterion's check in a test, not in prose.

### F4 — non-blocking · accuracy · Step 3 (the fixture script)
**The order of the copy and the link isn't stated, and only one order is right.** Guide § 9 (:79–82) is: save, Ctrl+D, then link the *first* scene, and its step 4 warns that "the copy keeps the original's Next stage". If the script links `My Level` first and copies after, `My Level 2`'s Exit links to `My Level 2`. That is a self-loop, `StageWalk` stops on it (`NoWayToWin`, :40–43), and it would be frozen into the fixture forever. Also state how the link is written. The script compiles in the student project's `Assembly-CSharp-Editor`, which can't see `internal NextStageGuid` (`InternalsVisibleTo` names only our assemblies). So it must set the serialized `nextStage` string to `AssetDatabase.AssetPathToGUID("Assets/My Level 2.unity")` through `SerializedObject`, as `StarterBuilder.SaveStage` does. That makes the Exit, a prefab instance of `Exit.prefab`, carry an override.
**Remedy:** spell out the order: `SaveScene(My Level)`, then `CopyAsset(My Level → My Level 2)`, then set `nextStage` on `My Level`'s Exit, then `SaveScene`.

### F5 — non-blocking · accuracy · Step 0 (`MakeTrapDemo`, "same pattern … `Produced.Add` for its path")
**In the actual pattern, the path is added *before* the cleanup, not inside the maker.** `Generate` adds the stage paths at :138–141 and only then runs `DeleteWhatWasNotProduced()` at :143. `MakeStageExample` runs at :145, after the deletion. A `Produced.Add` inside `MakeTrapDemo` (called after :145) would be too late. On every rebuild, the seeded `Trap Demo/Trap Demo.unity` would be deleted at :143 (`AssetDatabase.DeleteAsset` takes its `.meta`) and re-made with a new GUID. Step 0's "no diff on a second run" would then fail. Separately, `build_starter.py` cleans up `starter-picture.png` in the compat project (:142, :186), and `trap-demo-picture.png` needs the same.
**Remedy:** add `TrapDemoPath` beside :138–141, and say so in Step 0. Unlink the new picture where the old one is unlinked.

### F6 — non-blocking · design · Step 7
**The tag check comes after the tag is public.** "Check `git show 0.2.0:Tools/fixtures/0.2.0/Packages/manifest.json` reads 0.2.0" sits after `git push origin 0.2.0`. By then a wrong tag can't be taken back: Package Manager and OpenUPM already have it. A local tag can be deleted for free.
**Remedy:** `git tag 0.2.0`, then run the check, extended to `git show 0.2.0:Packages/com.orcapafea.daedalus/package.json` reading 0.2.0 and `git ls-tree 0.2.0 Tools/fixtures/0.2.0` being non-empty, and only then `git push origin 0.2.0`.

### F7 — non-blocking · accuracy · File Manifest, Step 2, Step 7
Bookkeeping the plan leaves out:
- The `v0.27` commit is version-bumping, so CLAUDE.md § Session-end writes need a `Changelog.md` entry and the `GDD.md` version line. The line now reads "package 0.1.0 published on OpenUPM with its Starter sample" and should name 0.2.0 and the demo. `v0.28` needs the same. Neither file is in the manifest.
- `Glossary.md` :11 (`Starter`, Draft) lists the sample's contents. The Trap Demo, a new student-facing name, belongs there.
- Deferred.md :35's trigger reads "before students are first told to install **0.1.0**", not only "ships in 0.1.1". Reword both.
- `.claude/CLAUDE.md` :135 also needs the new wording ("refused once the tag exists" → that version's tag; the fixture check now covers every fixture). The plan cites only :126 and :134.
- The File Manifest's `install_check.py` row doesn't list adding `StarterTrapDemoTests` to `PLAYTHROUGHS`, which Step 0 relies on.
- Step 2's grep will also hit `install_check.py` :305–313, the `--reimport-test` hard-wire that stays on purpose. That is not "history". Name it as the known exception, so the check isn't read as failing.

## Reviewer's view

I would build this once F1–F3 are fixed. F1 is a check that can't pass as written. F2 and F3 are guards that fail open, both on things that freeze at the tag. Step 0 checks out against the bench and the Starter prefabs, apart from F5's ordering. The release sequencing is sound, with F6 moving one check in front of the irreversible push.
