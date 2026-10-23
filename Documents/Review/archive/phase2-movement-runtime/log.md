# Handshake log: phase2-movement-runtime

## Opening (2026-10-23T21:39:39+09:00)

**Document:** `Documents/Phases/phase2-movement-runtime.md` (Author: Claude Code), written 2026-10-23. First handshake for this document.

**What it proposes:** Phase 2 of Milestone 1 (`movement-prototype` → `movement-sim` + `movement-runtime`): the Unity glue that makes Phase 1's sim playable. The Input System 1.11.2 dependency proven first on both editors; a `DaedalusTile` asset with a collision kind (plain tiles count as Solid); the Player component (on the player object, Movement and Abilities sections, data version) and the Rope component; reading the Collision Tilemap at play start (bounds grown to the start and ropes plus 24 rows of headroom); one sorted rope scan at play start (a documented exception to Architecture rule 5); rope tops snapping onto one-way surfaces in the sim; a plain C# session with the 60 Hz driver, interpolation, `Restart(PlayStart)` (the play-from-here hook) and a one-tick `Teleported` reset snap; controls; feet alignment; plain-words warnings; a generated dev test scene; PlayMode screenshot, allocation and student-path tests; then an operator editor check and the operator play-check with default tuning (the milestone gate).

**Sources:** `Documents/GDD/`, `.claude/memory/decisions.md` (2026-10-23 rows), `.claude/CLAUDE.md`, the existing package and `Tools/unity_tests.py`, and the **approved** Phase 1 prompt `Documents/Phases/phase1-movement-sim.md` (handshake archived at `Documents/Review/archive/phase1-movement-sim/`), whose sim API this phase consumes.

**Author's pre-open fixes:** renamed the runtime `RopeTests` to `RopeComponentTests` (it clashed with Phase 1's sim `RopeTests` in the shared test namespace) and added Phase 1's rope test file to the manifest as MODIFY; added Claude's recommendations to the two open questions.

Nothing is built; this handshake reviews the document only.

## Round 1 dispositions (2026-10-23T21:48:58+09:00)

Re-read every cited line in the doc; checked `Runtime/AssemblyInfo.cs` for F2; recomputed F3 and F9. Unity-behaviour claims (F5 nicify, F6 LogAssert) match my knowledge and aren't run here; both fixes are safe either way.

- **F1 — accepted (first remedy).** Confirmed: the approved Phase 1 Step 11 says "the runtime warns about ropes overlapping solid tiles, Phase 2" and Phase 2 had no such warning. Added the "Rope top in a wall" text to `PlayerWarnings`, issued in `Start` after the sim snap for each rope whose top cell (column floor(X), row ceil(Top)−1) is Solid/Climbable, with the Rope as context; tests for the solid-top and one-way-top cases; added to the Step 20 wire list. Not deferred, so Phase 1's expectation stands.
- **F2 — accepted.** Confirmed IVT covers only the two test assemblies and `Assets/Dev/Editor` has no asmdef. The generator writes every setting through `SerializedObject` (collisionTilemap, the four ability toggles, each tile's `kind`, each Rope's `length`); the internal setter is "tests only". No IVT added.
- **F3 — accepted (wording; value kept).** Recomputed: 6 × (1+3) = 24 is the feet's reach; the head meets the map top 1.5 earlier at max settings. Reworded; 24 kept as the operator's 2026-10-23 value; the 26 alternative is reported to the operator.
- **F4 — accepted (second remedy).** "Either order works" narrowed to the state; the map is fixed at play start, so a late `Restart` is reliable only inside it. `editor-tools` inherits the note to deliver the start before `Start` (or rebuild the map) and verify the callback order.
- **F5 — accepted.** `[InspectorName("One-way")]` on `OneWay` (label only, saved value unchanged), pinned in `TileAssetTests`.
- **F6 — accepted.** "Exactly once" is counted with the test-local `Application.logMessageReceived` listener; the old-input-setting warning is code-review only and the phase log will say so.
- **F7 — accepted.** `TryGetWorldBounds` returns false for no ropes, so the union skips it; test: no ropes, tiles at x 200–220 → CellMin is the tiles' min.
- **F8 — accepted.** Confirmed `Reset` is the only fill and a prefab can't hold a scene reference. `Start` applies the same pick without writing the field when the field is empty, and warns only if that finds nothing; the Step 6 grep and Step 20 list name the shared pick; test: empty field + one "Collision" Tilemap → lands, no warning. Covered by the rule-5 play-start exception sentence already in Notes to GDD ("…Daedalus components or Tilemaps…").
- **F9 — accepted (first remedy).** Recomputed: size 11 → 22 rows; 16:10 → 35.2 columns; 16:9 → 39.1. Layout capped at 35 × 22 (features may share rows), picture rendered at 16:10 (560 × 350).
- **F10 — accepted.** Gap line now "with a single jump (no air jump)".
- **F11 — accepted.** The student-path object gets a sprite (`TestImages.SolidSprite`).
- **F12 — accepted.** "the Deferred.md entries listed in Notes to GDD" (F1 was kept in scope, so the list stays at three).
- **F13 — accepted.** Step 3 states rule 8's hiding is interim until the `editor-tools` custom inspectors.
- **F14 — accepted.** Rope tooltip ends "Changes take effect the next time you press Play."
- **F15 — recorded** beside open question 1.
- **F16 — recorded** beside open question 2.

Fold re-verified: the rope-warning cell rule agrees with Phase 1's one-way-top rule (row round(Top)−1 OneWay → no warning); every new string has one home; the grep and review lists name both scan sites. Nothing built.

## Round 2 dispositions (2026-10-23T21:53:33+09:00)

- **F17 — accepted.** Confirmed from the manifest: `StudentPathTests.cs` is under `Tests/EditMode/` and `TestImages.cs` under `Tests/PlayMode/`; the EditMode asmdef doesn't (and shouldn't) reference the PlayMode test assembly. Step 15 item 3 now makes the sprite in the test (`Texture2D` + `Sprite.Create`, 12 × 24 px, centre pivot, 16 PPU), as item 2 does for the tile sprite.
- **F18 — accepted (all three parts).** Re-read Step 9: the read came before the pick. (1) `Start` item 1 now resolves the Tilemap (field, else the pick) before the rope scan and the read; the pick is described below the list and items renumbered. (2) The "falls and resets" test uses a scene with no Tilemap, and separately two Tilemaps neither named "Collision", and asserts the warning once. (3) Step 17 item 7 expects a silent landing; new item 7b (rename away from "Collision" + a second Tilemap) produces the warning, so "each problem gives one warning" still has a producer.

Fold re-verified: the Start sequence reads in order (resolve → ropes → read → session → warnings) and item 1's cross-reference points at the pick (my first fold text said "item 5", which the renumbering made wrong; fixed before handing back). Nothing built.

## Round 3 — approved; close dispositions (2026-10-23T21:58:27+09:00)

- **F19 — accepted (first remedy).** Confirmed: Step 10 aligned only the feet and the Step 17 Square is 1 × 1 against a 0.75 × 1.5 hitbox. Added a Player `OnDrawGizmosSelected` hitbox outline (from `MovementInternals.HitboxWidth`/`HitboxHeight`, which Phase 1 defines; standing on the feet, edit mode via the sprite's bounds offset, Play via the session) and a Step 17 Observe line. Re-checked: it's a MonoBehaviour message (no editor code, rule 3 holds) and reads the sim's constants (one source of truth).

**Outcome:** approved as a proposal after 3 rounds (blocking {F1, F2} → {F17} → {}). Nothing is built and no test has run. Conditions carried to implementation: the Unity-behaviour items (Input System 1.11.2 on 6000.0, `InputTestFixture`, `ObjectFactory.AddComponent` → `Reset`, `EnterPlayMode` in batch, `FindObjectsByType` obsolescence, enum labels) are proven by the phase's own tests or fallbacks; the two operator stops (Step 17 editor check, Step 18 play-check — the milestone gate) are ahead; open questions 1–2 (toggles default off; type names) and the headroom note (24 vs 26) are the operator's.
