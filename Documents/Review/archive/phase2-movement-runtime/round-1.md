---
slug: phase2-movement-runtime
round: 1
verdict: changes-requested
reviewer: claude-cowork
updated: 2026-10-23T21:52:00+09:00
blocking: 2
recommendations: 2
---

# Review round 1: `Documents/Phases/phase2-movement-runtime.md`

## What I could run, what I verified, what I could not

**Could run:** a shell over the connected `daedalus` folder (read files, `grep`, `find`, short Python). Nothing outside it: no Unity, no test suite, no build, no Input System installed to probe.

**Verified against files:**
- The whole document (675 lines), the approved Phase 1 prompt (its API, Step 11 ropes, Step 13 allocation fallback, "For Phase 2" notes), `decisions.md` 2026-10-23 rows, `Roadmap.md`, `entities.md`.
- Code on disk: `FixedStepAccumulator.cs` (Advance returns ≤ 5, epsilon 1e-9, `InterpolationFraction`), so `AdvanceClock(1/60.0) → 1` and `AdvanceClock(0.5) → 5` hold; `package.json` (two deps today); the runtime and both test asmdefs; `Runtime/AssemblyInfo.cs` (IVT to the two test assemblies only); `AssemblyDirectionTests.cs`, `PackageManifestTests.cs`; `Tools/unity_tests.py` `ensure_compat_project` (testables overwritten each run, line 81).
- Compat project: `Tools/compat/6000.0/Packages/manifest.json` and `packages-lock.json` show `com.unity.inputsystem` 1.20.0 (the doc's "currently resolves 1.20.0" is right); `activeInputHandler` is 1 in the dev project and 2 in compat (as the doc says).
- Dev `packages-lock.json`: `com.unity.modules.tilemap` depends on `com.unity.modules.physics2d`, so `Rigidbody2D`/`Collider2D` in `Player.Awake` compile without a new package dependency.
- `Assets/Dev/Editor/` has no asmdef (Assembly-CSharp-Editor), as the doc says.
- Arithmetic: `ToSegment` example (3.5, 10), length 4, origin (−2, 1) → X 5.5, Bottom 5, Top 9 ✓. Headroom 6 × (1 + 3) = 24 ✓ (but see F3). Stick cases (0.6, −0.7) down / (0.8, −0.7) no down ✓ under both raw and dead-zone-processed values (the processor scales radially, so the |y| > |x| test is unchanged).

**Not verified (taken on trust or left to the Author):** that 1.11.2 resolves and `InputTestFixture` runs on 6000.0.85f1; that `ObjectFactory.AddComponent` calls `Reset()`; `EnterPlayMode` in batch mode; `FindObjectsByType` obsolescence on 6000.6; the Input System's same-frame press/release behaviour of `WasPressedThisFrame` (the doc tests it in Step 1, which is the right place). Unity's enum-popup display names and `LogAssert` semantics (F5, F6) are from my knowledge of Unity, not run here; please confirm.

## Findings

### F1. Phase 1's promised "rope overlapping solid" warning is dropped
- **Type:** blocking · **Category:** accuracy · **Severity:** medium
- **Location:** Step 11 (warning list), Step 6 (rope snap); Phase 1 Step 11 "Top".
- **Defect:** The approved Phase 1 prompt rests a rule on this phase: "a rope ending inside a Solid or Climbable cell blocks the head before the feet arrive, so it just stops at the head bump (**the runtime warns about ropes overlapping solid tiles, Phase 2**)". Phase 2's warning list has no such text, no test and no Deferred entry; `grep -i overlap` finds only the dev-scene "nothing overlaps". The Step 6 snap only rescues tops near a **one-way**; a rope whose top sits in a Solid cell (the common "hang it from the ceiling" placement, since students place without grid snap) still stops silently 1.5 tiles under the ceiling. That is the producer-without-consumer pattern of Architecture rule 7, across a phase seam.
- **Fix:** add a `PlayerWarnings` text (e.g. "This rope's top is inside a wall or ceiling tile, so the player stops below it. Move the rope down so its top is in an empty cell or on a one-way platform."), issued once per rope at play start with the Rope as context (check the cells under the rope's top after the snap), plus a `PlayerWarningTests` case; or record explicitly that it is deferred, with a Deferred.md trigger, and correct the expectation Phase 1 set.

### F2. The dev generator calls `internal` members it cannot see
- **Type:** blocking · **Category:** accuracy · **Severity:** medium (compile error)
- **Location:** Step 2 (`DaedalusTile` "an `internal` setter for tests and the dev generator"), Step 12 (scene with "a Player with every ability on", Rope lengths).
- **Defect:** `MovementTestSceneBuilder.cs` is in Assembly-CSharp-Editor (Step 12; confirmed: no asmdef in `Assets/Dev/Editor/`). `Runtime/AssemblyInfo.cs` grants `InternalsVisibleTo` only to `Orcapafea.Daedalus.Tests.EditMode` and `.PlayMode`. So the generator cannot use the internal kind setter, and has no way to turn the Player's abilities on or set rope lengths except through serialized fields, which the doc names only for `collisionTilemap`. As written, Step 12 does not compile.
- **Fix:** the generator writes everything through `SerializedObject` (`kind`, `abilities.doubleJump|wallJump|wallClimb|ropeClimb`, `length`), which also proves those field names, and the internal setter's purpose becomes "tests only". Do not add an IVT for Assembly-CSharp-Editor.

### F3. "24 rows is the highest reachable" leaves out the hitbox
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 5, `MapHeadroomRows`.
- **Defect:** 6 × (1 + 3) = 24 is how high the **feet** can rise above the highest surface. The head is 1.5 above the feet, and rows `y ≥ Height` are Solid, so with every setting at max the head reaches the invisible map top 1.5 tiles before the apex (feet 22.5 above the surface). The value is the operator's (decisions.md row 34) and I am not asking to change it; the justification sentence is wrong, and the pinned test would encode it.
- **Fix:** reword to "the feet's highest reach with every setting at max; the head meets the map top 1.5 tiles before that apex, which only matters at extreme settings". If the operator wants the stated property, the number is 26 (`ceil(24 + 1.5)`); that is their call.

### F4. A play-from-here start that arrives after `Start` is outside the grown map
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Step 9 `Restart` ("either order works"), Step 5 bounds growth.
- **Defect:** The bounds grow to include `pendingStart ?? DefaultStart`, computed once in `Start`. If the later editor tool's `EnteredPlayMode` call lands after `Start`, the map was grown around the placed Player, not the play-from-here point. A point beyond the painted extent or above the 24 rows is then outside the map, and `StartPlacement.Find` moves it up to 8 cells or fails. "Either order works" holds for the state, not the map. The same applies to any later `Restart` (checkpoints are inside the map, so they are fine).
- **Fix:** either state that the map is fixed at play start and play-from-here must call `Restart` before `Start` (and say which Unity callback guarantees that), or narrow "either order works" to "inside the map". No code needed now; the editor-tools milestone inherits the note.

### F5. The Kind dropdown will say "One Way", not "One-way"
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 17 Observe ("None, Solid, One-way, Climbable"); Step 2 tooltip.
- **Defect:** Unity's enum popup shows `ObjectNames.NicifyVariableName` of each member, so `OneWay` displays as "One Way". The operator check would fail on wording, or pass on a mismatch. (From Unity knowledge; not run here.)
- **Fix:** add `[InspectorName("One-way")]` on `OneWay` (runtime attribute, saved value unchanged) and pin it in `TileAssetTests`, or change the observation to "One Way".

### F6. `LogAssert.Expect` cannot prove "exactly once", and two warnings have no test path
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 11 tests; Completion "Every warning appears once".
- **Defect:** `LogAssert.Expect(LogType.Warning, text)` consumes one matching message; a second identical warning is not a failure (unexpected *warnings* do not fail a test the way unexpected errors do). So "exactly once over 120 frames" is not asserted by the tool named. Separately, the "Old input setting" warning (`#if !ENABLE_INPUT_SYSTEM`) cannot be produced in either test project (handlers 1 and 2), and no operator step covers it.
- **Fix:** count with the same test-local `Application.logMessageReceived` listener the clean case already uses (count == 1 per text). Mark the input-setting warning as code-review-only in the phase log, or add it to Step 17 if the operator is willing to flip the setting once.

### F7. An empty rope list must not put the world origin into the map bounds
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Step 6 `RopeCollector.WorldBounds(Rope[])`, Step 9 "include = start hitbox ∪ rope bounds".
- **Defect:** The doc does not say what `WorldBounds` returns for no ropes. The natural `Rect` default is a zero rect at (0, 0); unioned with the start, it grows the map to the world origin. A level painted at x = 200 then gets its left map wall at x = 0, so the edge behaviour (invisible wall vs. fall-out) changes with whether the scene has any rope. The reader test "a rope to the right of all tiles is included" would not catch it.
- **Fix:** `WorldBounds` returns a bool / nullable for "no ropes" and the union skips it; add a reader or `RopeComponentTests` case: no ropes, tiles far from the origin → `CellMin` is the tiles' min, not (0, 0).

### F8. A Player saved as a prefab loses its Collision Tilemap, and only `Reset` fills it
- **Type:** non-blocking · **Category:** design · **Severity:** medium
- **Location:** Step 3 `Reset()` auto-fill; Step 9 `Start` item 5; CLAUDE.md Architecture rule 4 ("often saved as the student's own prefabs").
- **Defect:** A prefab asset cannot reference a scene Tilemap, so a student's Player prefab dropped into a scene has an empty field; `Reset` does not run on instantiation. The same happens when a student adds the Player before creating or renaming the Tilemap (Step 17 does it in the safe order). Result: the player falls forever with a warning, on what the student experiences as a working object. The new rule-5 exception text already allows a play-start Tilemap scan.
- **Fix:** in `Start`, when the field is empty, apply the same pick as `Reset` (named "Collision", else the only Tilemap) without writing the field, and warn only when that also finds nothing. One more `FindObjectsByType` site (update the Step 6 grep and the Step 20 review list) and a `PlayerWarningTests` case: field empty, one "Collision" Tilemap → lands, no warning.

### F9. The dev scene does not fit a 16:10 laptop at camera size 11
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Step 12 "40 × 22 tiles, camera size 11"; Step 18 play-check.
- **Defect:** Size 11 shows 22 rows; 40 columns need an aspect ≥ 40/22 = 1.82. At 16:9 the view is 39.1 tiles wide (≈ 0.9 cut); at 16:10 (common on school laptops) 35.2 (≈ 4.8 cut); a docked Game view in Free Aspect is usually narrower still. The batch picture renders at whatever size the builder picks, so it can look complete while the operator's view is not.
- **Fix:** lay the scene out in ≤ 35 columns, or set the camera size from the aspect so the width fits (a dev-only script is fine), and render the picture at 16:10.

### F10. Play-check gates are not single-ability with every ability on
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Step 18 Do ("clear the 5-tile gap and fail the 6-tile gap"); Step 12 "every ability on".
- **Defect:** With double jump on, the 6-tile gap and the 4-tile ledge are cleared with the air jump, so "fail the 6-tile gap" is only meaningful with a single jump. The ledge line says "with a single jump"; the gap line does not.
- **Fix:** "with a single jump (no air jump)" on the gap line, or run the gap and ledge items with Double Jump unticked (the last Do line already does per-ability replays).

### F11. The student path test needs a sprite to be warning-free
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 15 items 3 and 6.
- **Defect:** Item 3 creates "a GameObject with a `SpriteRenderer`" but no sprite; item 6 asserts "no warnings" and "the sprite bottom on the floor top". Without a sprite the "No sprite" warning fires and the feet offset is 0.
- **Fix:** item 3: a `SpriteRenderer` with a sprite (the same code-made sprite as the tiles, or `TestImages.SolidSprite`).

### F12. "File the four Deferred.md entries above" lists three
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Post-Phase Cleanup item 6; Notes to GDD → Deferred.md.
- **Defect:** The Deferred list has three entries (warnings only in the Console; rope without a sprite; bounds never shrink). F1 or F13 may add a fourth; as written the count is wrong.
- **Fix:** say "the Deferred.md entries above" or fix the count after folding.

### F13. Mode-only settings stay visible; the doc does not say so
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Step 3 Abilities section; Step 17 Observe; CLAUDE.md Architecture rule 8 ("a setting that applies to one mode only … shows only in that mode").
- **Defect:** `airJumps`, `wallSlideSpeed` and `climbSpeed` apply only with their toggles on, and are always shown, with "Used only when …" tooltips. Roadmap §1 `editor-tools` already plans "mode-specific settings hidden", so this is a deliberate interim state, but the doc presents the layout as final and the operator check encodes it.
- **Fix:** one sentence in Step 3: "Rule 8's hiding arrives with the editor-tools custom inspectors (Roadmap §1); until then the tooltips say when a setting applies." No code.

### F14. Rope length changes in Play do nothing, and the tooltip doesn't say so
- **Type:** non-blocking · **Category:** design · **Severity:** low
- **Location:** Step 4 Rope tooltip; Step 6 (ropes read once at play start).
- **Defect:** The Player's numbers apply live in Play, the Rope's length does not, but its gizmo line follows the slider live, so a student sees the rope get longer while the player still stops at the old bottom.
- **Fix:** end the Rope tooltip with the same sentence the ability toggles use: "Changes take effect the next time you press Play."

### F15. Open question 1 (ability toggles default off)
- **Type:** recommendation
- **Location:** Open questions, 1.
- **Reviewer recommends:** off. It matches entities.md's starting abilities `["jump"]`, keeps a fresh Player's first Play to run and jump (no surprise wall-cling on the first wall a beginner paints), and leaves the pickups in world-systems something to unlock.

### F16. Open question 2 (type names)
- **Type:** recommendation
- **Location:** Open questions, 2.
- **Reviewer recommends:** keep them. They read as what they are, none collides with a Unity type in the namespaces used, and only the serialized field names (already fixed) are costly to change later.

## Reviewer's view

I would build this after F1 and F2. F2 is a compile error in Step 12 with a one-line fix; F1 is a promise the approved Phase 1 made on this phase's behalf and should either be kept or explicitly deferred. Of the rest, F8 (prefab Players lose their Tilemap) is the one with the most real student impact; F3–F7 and F9–F14 are wording, test-precision and layout fixes.
