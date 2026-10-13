# Architecture
**Last updated:** v0.01

Mirrors [CLAUDE.md](../../.claude/CLAUDE.md) § Architecture. Draft until the scaffold and the runtime spike land.

## System Map

| Part | Owns | Must not |
|---|---|---|
| **Project format** (`packages/format/`) | Project JSON types, validation, `formatVersion` migrations, the default tunable values | Import the runtime or the editor |
| **Runtime sim** (`packages/runtime/src/sim/`) | `step(state, input) → state`: player, collision, combat, enemies, level elements, in-game progress state | Touch the DOM, canvas, Web APIs, the clock or `Math.random()` directly |
| **Runtime render** (`packages/runtime/src/render/`) | Drawing sim state: chunked tiles, sprites and animation states, camera, interpolation between ticks | Mutate sim state |
| **Runtime input** (`packages/runtime/src/input/`) | Keyboard + Gamepad API → one `Input` value per tick | Make gameplay decisions |
| **Editor** (`apps/editor/`) | Editing tools, inspector, presets, command stack (undo/redo), autosave, export/import, validation warnings, asset import, publish | Contain gameplay logic (Architecture rule 3) |
| **Player page** (`apps/player/`) | Load a shared project and run it in play mode only | Include editor code |

**Dependency direction:** editor → runtime → format; player page → runtime → format.

## Time Model

- The sim advances in **fixed 60 Hz ticks**. Rendering runs at the display rate and interpolates between the last two sim states.
- Hit-stop (~50 ms) pauses the sim's gameplay advance, not the render loop. Exact mechanism: decided in `combat-and-enemies`.
- Times in the project format are seconds; the runtime converts them to ticks.

## Data Flow

1. The editor holds the project JSON and edits it only through commands (undo/redo).
2. **Play from start:** the runtime receives the project; the player starts at the player start with `startingAbilities`.
3. **Play-from-here:** the runtime receives the project, a start position and an ability set (the student's checkboxes). Esc returns to the editor at the same view.
4. The player page receives a published project and runs it as in step 2.
5. The runtime converts tile units to pixels internally; nothing outside the runtime sees pixels.

## Active Radius

Enemies, projectiles and moving objects update only within ~1.5 screens of the camera; outside it they freeze in place. Tiles render from cached chunks (e.g. 32 × 32 tiles).

## Open Questions

- **Runtime:** custom Canvas/WebGL vs Phaser 3 (recommendation: custom tile collision for the player; decide after a one-day spike). If Phaser, where its scene loop ends and the pure `step()` begins.
- **Editor UI framework:** React (recommended) vs plain TypeScript.
- **Asset storage:** embedded in the project file vs stored alongside it for hosting.
