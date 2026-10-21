# Architecture
**Last updated:** v0.03

Mirrors [CLAUDE.md](../../.claude/CLAUDE.md) § Architecture. Draft until the scaffold lands.

## System Map

| Part | Owns | Must not |
|---|---|---|
| **Sim** (`Runtime/Sim/`, assembly `Orcapafea.Daedalus.Sim`) | `Step(state, input) → state`: player, collision, combat, enemies, level elements, in-game progress state; the tunable defaults | Reference `UnityEngine` or `UnityEditor`; read the clock or a random source directly |
| **Runtime** (`Runtime/`, assembly `Orcapafea.Daedalus`) | The components and ScriptableObjects students place and edit; reading the scene (Collision Tilemap, placed elements, settings assets) into sim data at play start; the 60 Hz tick loop; sprites, animation, camera and interpolation; Input System → one `Input` per tick; checkpoint saves | Make gameplay decisions outside the sim; write to authored assets; reference the editor assembly |
| **Editor tools** (`Editor/`, assembly `Orcapafea.Daedalus.Editor`) | Custom inspectors and gizmos, the daedalus window (play-from-here abilities, validation warnings), Scene-view play-from-here, scene autosave, terrain setup helpers | Contain gameplay logic (Architecture rule 3) |
| **Unity** (not ours) | Scene view, Tile Palette, Tilemap, Inspector, prefabs, undo/redo, scene saving, Sprite Editor, Package Manager, WebGL build | — |

**Dependency direction:** editor tools → runtime → sim, enforced by assembly references. The sim references nothing.

## Time Model

- The sim advances in **fixed 60 Hz ticks**, driven by the runtime from Unity's frame loop with an accumulator; Unity's physics step is not used for gameplay. Rendering runs at the display rate and interpolates transforms between the last two sim states.
- Hit-stop (~50 ms) pauses the sim's gameplay advance, not rendering. Exact mechanism: decided in `combat-and-enemies`.
- Authored times are seconds; the runtime converts them to ticks.

## Data Flow

1. Students edit the scene and our assets with Unity's tools; Unity owns undo/redo and saving.
2. **Play from start** (Unity's Play button): the runtime reads the Collision Tilemap, the placed elements and the settings assets into the sim; the player starts at the Player start with the starting abilities.
3. **Play-from-here:** an editor tool records a start position and an ability set (the student's checkboxes) and enters Play mode; the runtime reads them at start instead of the Player start. Leaving Play mode returns to the same Scene view.
4. A WebGL build runs step 2.
5. **1 Unity unit = 1 tile.** Sprites import at 16 pixels per unit; nothing else sees pixels.

## Active Radius

Enemies, projectiles and moving objects update only within ~1.5 screens of the camera; outside it they freeze in place. Tilemaps render in Chunk mode.

## Technology (v0.03)

- **Unity 6** (minimum 6000.0), C#, distributed as the UPM package `com.orcapafea.daedalus` through OpenUPM.
- **Sim: custom, kinematic.** Our own tile collision on the Collision Tilemap's grid and a pure `Step()`; no Rigidbody2D / Collider2D for gameplay. Exact platformer feel (coyote time, one-way platforms, wall climb) and EditMode scenario tests both need rules we own.
- **Rendering:** Unity's Tilemap and SpriteRenderer with the **URP 2D Renderer**. **Input:** the Input System package. **Tiles:** 2D Tilemap Extras (rule, random and animated tiles).
