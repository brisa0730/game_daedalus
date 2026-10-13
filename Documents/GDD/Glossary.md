# Glossary
**Last updated:** v0.02

Canonical terms. Each is **Draft** (provisional) or **Active** (committed).

| Term | Status | Meaning |
|---|---|---|
| Project | Draft | One student's game: a versioned JSON document (map, entities, settings, asset references). |
| Project format | Draft | The shared schema for a project, with `formatVersion` and migrations; the only thing the editor and runtime exchange. |
| Runtime | Draft | The part that plays a project: the pure sim, the renderer and input. |
| Sim | Draft | The runtime's pure `step(state, input)` at a fixed 60 Hz tick. |
| Editor | Draft | The student-facing tool that builds a project. Contains no gameplay logic. |
| Player page | Draft | The play-only page a shared link opens. |
| Play-from-here | Draft | Starting the game at the editor cursor with a chosen ability set; Esc returns to the same view. |
| Play from start | Draft | Starting at the player start with only the starting abilities: the real experience. |
| Ability | Draft | One of the player's built-in moves (run, jump, double jump, wall jump, wall climb, rope climb, attack); on from the start or unlocked by a pickup. |
| Ability pickup | Draft | A placed element that unlocks one ability. |
| Ability gating | Draft | Progression by making areas reachable only with a given ability. |
| Enemy preset | Draft | A named, reusable combination of enemy settings (e.g. "Bat"). |
| Override | Draft | A per-instance change to a placed enemy's preset values. |
| Safe ground | Draft | The last position the player stood on safely; hazards return the player there. |
| Checkpoint | Draft | A placed element that sets the respawn point and saves in-game progress. |
| Active radius | Draft | The area (~1.5 screens) around the camera inside which objects update. |
| Chunk | Draft | A cached block of tiles (e.g. 32 × 32) rendered as one unit. |
| Tile kind | Draft | A tile's or terrain's collision type: solid, one-way, climbable, hazard, none. Counts only on the collision layer. |
| Terrain | Draft | An auto-tiling brush (e.g. "Grass ground"): a tileset, a layout, a collision kind and the tiles for each neighbor pattern. |
| Auto-tiling | Draft | Picking each painted cell's edge / corner / inner-corner tile from its neighbors. |
| Blob layout | Draft | The 47-tile auto-tile layout (8-neighbor bitmask; a diagonal counts only with both adjacent edges). |
| Dual-grid layout | Draft | The 16-tile auto-tile layout drawn half a tile off the logical grid; collision stays on the logical grid. |
| Tile palette | Draft | The editor panel showing a tileset; a click picks a tile, a drag-select a multi-tile brush. |
| Operator | Draft | The person directing the project and making its design decisions. |
