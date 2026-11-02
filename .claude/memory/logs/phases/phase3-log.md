# Phase 3 log: `death-and-respawn` (v0.10, 2026-11-02)

Prompt: [phase3-death-and-respawn.md](../../../../Documents/Phases/archive/phase3-death-and-respawn.md) (design-reviewed, handshake-approved in 2 rounds). Result: EditMode 319 and PlayMode 72 green on **6000.0.85f1** and **6000.6.7f1**, no compiler warnings; operator editor check and play-check passed with every `LevelRules` value unchanged; code review PASS WITH FLAGS (no HIGH, 1 MED fixed, 9 LOW: 6 fixed, 1 deferred, 2 accepted).

## Per step
1. **Hazard tile kind 4.** `TileKind.Hazard = TileCollisionKind.Hazard = 4`; non-blocking; stale "reserved" comments fixed. `AsciiLevel` gains `^`.
2. **`LevelRules`.** The one source for the beat (0.4 s → 24 ticks), hazard inset 0.2, contact margin 0.0625, zone +1 up, trap Delay 0 (0–3), Size 3 × 1 (1–30), win lock 1 s → 60 ticks, `SecondsToTicks`, `DefaultZone`. Pinned by `LevelRulesTests`.
3. **Hazard queries and start placement.** `TileCollision.CoreTouchesHazard` and `StandsFullyOnPainted` (reads `GetPainted`); `StartPlacement.IsFree` also refuses a hazard under the core, and a moved candidate needs support (handshake F1). Max fall and max push speed can't skip a one-row strip (counted cases).
4. **World step.** New `LevelStepper` around `PlayerStepper` with the prompt's tick order; `PlayerStepper` lost its fall-out reset and start fields, `PlayerState` its `StartX/StartY`. Respawn sets `Teleported` on one tick; a held jump doesn't fire on respawn; `FixedStepAccumulator.Reset()`.
5. **Checkpoints, exits, win, play again (sim).** As specified; `CanPlayAgain` after 60 ticks.
6. **Static traps (sim).** `TrapSpec`, `Box`, the trap-cell layer in `TileGrid` (`Get` = painted else trap; `SetTrapCell` ignores cells outside the grid). `Fire` only marks; `ResolveAppeared` runs only for a batch that fired (handshake F14) and pushes once against the union box (ties up, left, right, down) or crushes. `ResetTraps` bumps `TrapChanges` on change (F6).
7. **Zero allocation.** `StepAllocationTests.LevelStepAllocatesNothing`; **planted failure seen:** a used `new int[1]` in `Fire` turned it red; reverted.
8. **Player Health section.** `health.onHazard` (Return to safe ground 0 / Respawn at checkpoint 1), live in Play through `SetRule`. `PlayerSession` drives `LevelStepper` (new constructor; keeps its name, open question 6).
9. **Checkpoint, Exit, play-start scan.** `PlayStartScan.FindActive<T>` (the 6000.6 `FindObjectsByType` split), `LevelCollector` (sorted by world box; `FindTraps(out ignored)` for traps on an Exit or Checkpoint); `RopeCollector` uses the shared scan.
10. **uGUI.** `com.unity.ugui` 2.0.0 declared; the compat lock file resolves **2.0.0 builtin**, the dev project 2.6.0 builtin. **Planted failure seen:** expected 2.0.1 in the manifest test → red; reverted.
11. **Trap component and Play look.** As specified, centred box (open question 1). **Not done:** the Tight-mesh check (Tiled mode with a Tight sprite) was never run; recorded as unchecked in framework.md. Dev sprites are Full Rect and the inspector warns on a non-Full-Rect sprite.
12. **Death look, camera, HUD, win screen.** `DeathBurst`, `CameraFollow` (`Camera.main` once at play start, clamped, cut on `Teleported`), `PlayHud` (code-built uGUI on a `ScreenSpaceCamera` canvas so screenshots see it; skull from a string pattern; `LegacyRuntime.ttf`; "0"–"999" cached). **Planted failure seen:** `deaths.ToString()` in `PlayHud.Show` turned `PlayerAllocationTests` red; reverted.
13. **Editor tools.** `TrapEditor` (zone only for the zone trigger, story, warnings, Fit zone, Snap to grid, `BoxBoundsHandle` snapped to whole tiles), `TrapAuthoring` (`ObjectFactory.componentWasAdded` hook), `CheckpointEditor`, `ExitEditor`, `TrapGhostGizmo`. **Deviation:** the ghost is drawn with GL immediate mode (one quad per cell from the sprite's `textureRect`) instead of `Graphics.DrawMeshNow`. **Deviation:** "over painted tiles" is one shared `TrapLook.CoversPaintedTiles` used by the inspector and play start, so they can't disagree.
14. **Play-start warnings.** Each new text once, with the object as context. **Bug found and fixed:** the off-grid warning could never fire, because `ApplyPlayLook` snapped the trap before `WarnLevel` ran; `WarnLevel` now runs first.
15. **Troll tour and allocation guard.** `Tests/PlayMode/Traps/` (`TrapPlayTests`, `HudAndWinTests`, `CameraFollowTests`, `LevelWarningTests`), `WarningLog` shared. **Deviation:** `TestLevel` adds traps, checkpoints and exits through helper methods after the build, not option lists. `PlayerRenderTests` now uses Respawn at checkpoint (the default rule returns to safe ground). The allocation tour covers a Pop Spike death, the beat, the exit, the win lock and play again.
16. **Trap bench.** `Assets/Dev/Editor/TrapTestSceneBuilder.cs` → `Assets/Dev/TrapTest/`: 60 × 16, Drop Floors A (start inside its zone), B and C (checkpoint inside its zone), Pop Spike, Hidden Block, a Fake Floor on Background, painted spikes, two adversarial traps (over painted tiles, off the grid), checkpoint (40, 1), exit (57, 1). Exit 0, self-check passed, picture inspected, GUIDs stable on a second run, PNGs in LFS.
17. **All suites.** 6000.0.85f1 and 6000.6.7f1: EditMode 315, PlayMode 72, green, no warnings. **Pictures inspected** on both editors: counter, win screen, burst, Drop Floor, Pop Spike, hidden block, checkpoint, render smoke. Notes: on the bench picture the Vanish traps can't be told from the ground (intended); the checkpoint's gold tint multiplies the sprite colour, so a coloured sprite turns muddy (the bench flag is white).

## Step 18 (operator editor check, 2026-11-02): passed
The operator's "Everything is doing good" (after Step 19) covered both checks; their screenshot shows a level of their own built with the tools (a Pop Spike strip, a Drop Floor over a gap, a block, a flag). No item-by-item report.

## Step 19 (operator play-check, 2026-11-02): passed
Questions answered on the way: a Drop Floor drawn over a gap drops the player where ground is visible (intended), and Return to safe ground brings the player back flush against the trap's edge (intended; flagged as a possible feel issue, not raised). The operator asked about moving floors: those are the Move and Fall actions of the next part of `traps`. **"Everything is doing good."**

## Step 20: tuning
No value changed: beat 0.4 s, inset 0.2, margin 0.0625, zone +1 up, Delay 0 (0–3), Size 3 × 1 (1–30), win lock 1 s, ghost alpha 0.38, push ties up / left / right / down. The prompt's **(proposal)** markers are confirmed.

## Step 21: code review (PASS WITH FLAGS)
- **MED, fixed:** an Inspector edit to a Trap in Play ran `SyncLook`, which re-enabled a vanished floor's renderer (or hid an appeared block) while the sim, which reads traps at play start, disagreed: a death with no visible cause. `SyncLook` now leaves the look alone in Play, and the inspector says "Trap changes take effect the next time you press Play" (`HelpTexts.TrapPlayModeNote`), as the Rope's length does. Test `InPlayAnEditLeavesTheRunningLookAlone`.
- **LOW, fixed:**
  - an off-grid trap's sim zone stayed at the unsnapped position (`ToSpec` now moves the zone with the snap; `AnOffGridTrapsZoneMovesWithItsSnappedBox`);
  - a huge Delay set by script overflowed to a negative tick count and threw at play start (clamped in `ToSpec`; `AnOutOfRangeDelayIsClampedNotThrown`);
  - the rope-top warning read the grid after trap cells were stamped, unlike the stepper's snap (moved before the session);
  - the story's numbers used the machine's culture ("0,5 s"; now invariant; `NumbersReadTheSameOnAMachineWithDecimalCommas`);
  - a stale "falls out and resets" comment in `CollisionMapReader`; a missing comma in the `DaedalusTile` tooltip.
- **Seen failing:** with the old behaviour planted back, all four new tests went red in one filtered run on 6000.6; restored.
- **LOW, deferred:** a Grid or Collision Tilemap moved by a fraction puts sim cells up to half a tile from the drawn boxes (Deferred.md).
- **LOW, accepted:** `TrapGhostGizmo`'s static material cache (editor-only, `HideAndDontSave`, a drawing resource, not game state); play again restores the play start's abilities while the Inspector toggles keep their Play-mode edits (Phase 2's `Restart` semantics).
- **Deviation noted:** the prompt's `Box.Contains` was not added; nothing needs it.

## Closing runs
After the review fixes: 4/4 green, no compiler warnings: 6000.0.85f1 EditMode 319, PlayMode 72; 6000.6.7f1 EditMode 319, PlayMode 72 (+4 EditMode from the review fixes). `traps-dropfloor` and `traps-win` re-inspected: unchanged.
