---
slug: phase2-movement-runtime
round: 2
verdict: changes-requested
reviewer: claude-cowork
updated: 2026-10-23T21:58:00+09:00
blocking: 1
recommendations: 0
---

# Review round 2 (delta): `Documents/Phases/phase2-movement-runtime.md`

## What I could run, what I verified, what I could not

**Could run:** the same shell over the connected `daedalus` folder as in round 1. No Unity, no tests.

**Verified:** `log.md` round 1 dispositions (all 14 findings accepted, F15/F16 recorded), and every folded passage in the doc: Step 2 (`[InspectorName("One-way")]`, tests-only setter), Step 3 (rule-8 interim sentence), Step 4 (rope tooltip), Step 5 (headroom wording), Step 6 (`TryGetWorldBounds`, the grep naming the shared pick), Step 9 items 2–5 and `Restart`, Step 11 (rope warning text, cell rule, listener counts, new test cases), Step 12 (35 × 22, 560 × 350, every setting through `SerializedObject`), Step 15 item 3, Step 18 gap line, Step 20 wire list, Post-Phase item 6, the two recorded recommendations.

**Rechecked the F1 cell rule against Phase 1:** top cell = column `floor(X)`, row `ceil(Top) − 1`. A rope whose top is exactly on a Solid block's top face (Top = 9.0, row 8 Solid) → row 8 → warns ✓. A rope touching a ceiling's bottom face (Top = 10.0, row 10 Solid) → row 9, empty → no warning, and Phase 1's head bump there is the normal "head touches the ceiling" case ✓. A one-way at the top (after the snap, `|round(Top) − Top| ≤ 0.0625`) → OneWay → no warning, consistent with Phase 1's stand-on-top rule ✓. F9: 560 × 350 is 16:10, and 35.2 columns at size 11 ✓.

**Not verified:** the same Unity-behaviour items as round 1.

## Findings

### F1–F16: closed
F1–F14 are folded as described in `log.md`; F15 and F16 are recorded beside the open questions. No corrections to my round-1 claims.

### F17. The student-path test (EditMode) uses a PlayMode-assembly helper
- **Type:** blocking · **Category:** accuracy · **Severity:** medium (compile error)
- **Location:** Step 15 item 3 (the F11 fold): "a `SpriteRenderer` **with a sprite** (`TestImages.SolidSprite`)"; Step 13 / manifest line 77 (`P/Tests/PlayMode/TestImages.cs`).
- **Defect:** `StudentPathTests.cs` is in `Tests/EditMode/` (assembly `Orcapafea.Daedalus.Tests.EditMode`); `TestImages` is in `Tests/PlayMode/` (`Orcapafea.Daedalus.Tests.PlayMode`). The EditMode asmdef references Sim, runtime, Editor and the two TestRunner assemblies, not the PlayMode test assembly, and a test assembly referencing another test assembly would be the wrong fix. As written, the EditMode suite doesn't compile. This is a seam defect: my F11 asked for a sprite, and the fold reached for the nearest helper across the assembly line.
- **Fix:** make the sprite in the test the way item 2 makes the tile sprite ("sprite from code": `Texture2D` + `Sprite.Create`, 12 × 24 px, centre pivot, 16 PPU), or name the EditMode-side helper that does it.

### F18. The play-start Tilemap pick changes what "empty field" means in three places
- **Type:** non-blocking · **Category:** accuracy · **Severity:** low
- **Location:** Step 9 `Start` items 2 and 5; Step 11 test "empty field → no exception, the player falls and resets"; Step 17 Do item 7 and Observe "each problem gives one plain-words Console warning".
- **Defect:** The F8 fold is right in substance, but:
  1. In `Start`, item 2 reads `collisionTilemap` and item 5 applies the pick afterwards; read literally, the pick runs after the map is already built from a null field.
  2. The Step 11 test "empty field → … falls and resets" now lands instead whenever its `TestLevel` has a Collision Tilemap; it only holds in a scene with no Tilemap (or two unnamed ones).
  3. Step 17 item 7 (clear the field, press Play) now lands silently in the operator's scene, so the Observe line "each problem gives one plain-words Console warning" fails on a behaviour that is now correct.
- **Fix:** (1) resolve the Tilemap (field, else the pick) as item 1 of `Start`, before the read; (2) the "falls and resets" test uses a scene with no Tilemap; (3) Step 17 item 7 becomes "clear the field and press Play: the player still lands, with no warning", and a separate item produces the warning (e.g. rename the Tilemap away from "Collision" and add a second Tilemap, then press Play).

## Convergence

Blocking ids: round 1 {F1, F2} → round 2 {F17}. Both round-1 blockers are closed; F17 is new and comes from a fold. Converging.

## Reviewer's view

I would build it once F17 is fixed, a one-line change to Step 15. F18 is wording and test setup that follows from the F8 fold. Nothing else stands between this and approval.
