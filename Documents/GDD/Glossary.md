# Glossary
**Last updated:** v0.06

Canonical terms. Each is **Draft** (provisional) or **Active** (committed).

| Term | Status | Meaning |
|---|---|---|
| Package | Draft | `com.orcapafea.daedalus`, installed from OpenUPM: the sim, the runtime, the editor tools and the starter sample. |
| Project | Draft | One student's Unity project with the package installed: a scene (Tilemaps + objects with Daedalus components) plus terrain and animation assets and the student's prefabs. |
| Authored data | Draft | What a student saves: the scene, their prefabs and our assets. A package update must never break it (data version + upgrade step). |
| Starter | Draft | The package sample a student imports first: a scene, example prefabs for every element and the default art pack. |
| Daedalus component | Draft | One of our components in the Add Component menu under Daedalus (Player, Enemy, Checkpoint…); adding it makes an object work, with at most ~8 settings. |
| Add-on | Draft | A small Daedalus component that extends one other (e.g. Shoots Projectiles on an Enemy). |
| Sim | Draft | The pure `Step(state, input)` at a fixed 60 Hz tick, in its own assembly with no engine references. |
| Tick | Draft | One fixed 1/60 s step of the sim. `FixedStepAccumulator` turns real time into ticks; after a hitch at most 5 run and the rest are dropped. |
| Runtime | Draft | The package's play-time part: reads the scene into the sim, runs it, drives sprites, camera, input and saves. |
| Editor tools | Draft | Our additions to the Unity Editor: custom inspectors and gizmos, the daedalus window, play-from-here, validation, scene autosave. No gameplay logic. |
| Daedalus window | Draft | The editor window holding the play-from-here ability checkboxes and the validation warnings. |
| Shared build | Draft | A WebGL build of a student's game, played in a browser. |
| Play-from-here | Draft | Entering Play mode at the Scene-view cursor with a chosen ability set; leaving Play mode returns to the same view. |
| Play from start | Draft | Unity's Play button: starting at the Player start with only the starting abilities, the real experience. |
| Live tuning | Draft | Changing component settings in the Inspector during Play mode to try them out. As everywhere in Unity, the changes are thrown away when Play mode ends; Copy / Paste Component Values keeps a result. |
| Ability | Draft | One of the player's built-in moves (run, jump, double jump, wall jump, wall climb, rope climb, attack); on from the start or unlocked by a pickup. |
| Ability pickup | Draft | A placed element that unlocks one ability. |
| Ability gating | Draft | Progression by making areas reachable only with a given ability. |
| Enemy type | Draft | A prefab of an object with an Enemy component (e.g. "Bat"). |
| Override | Draft | Unity's prefab override: a placed enemy's change to its prefab's values. |
| Safe ground | Draft | The last position the player stood on safely; hazards return the player there. |
| Checkpoint | Draft | A placed element that sets the respawn point and saves in-game progress. |
| Active radius | Draft | The area (~1.5 screens) around the camera inside which objects update. |
| Collision Tilemap | Draft | The one of the three Tilemaps (Background, Collision, Foreground) whose tiles affect gameplay. |
| Tile kind | Draft | A tile's or terrain's collision type: solid, one-way, climbable, hazard, none. Counts only on the Collision Tilemap. |
| Terrain | Draft | An auto-tiling brush (e.g. "Grass ground"): a tile asset with a layout, a collision kind and the sprites for each neighbor pattern. |
| Auto-tiling | Draft | Picking each painted cell's edge / corner / inner-corner tile from its neighbors. |
| Blob layout | Draft | The 47-tile auto-tile layout (8-neighbor bitmask; a diagonal counts only with both adjacent edges). |
| Dual-grid layout | Draft | The 16-tile auto-tile layout drawn half a tile off the logical grid; collision stays on the logical grid. |
| Tile Palette | Draft | Unity's window for painting tiles and terrains onto a Tilemap. |
| Operator | Draft | The person directing the project and making its design decisions. |
