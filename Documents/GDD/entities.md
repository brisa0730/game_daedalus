# Entities & Data
**Last updated:** v0.01

Values here mirror the single source of truth in `packages/format/` once it exists (CLAUDE.md § Coding Conventions → Tunable defaults). Until then the numbers below are a starting sketch, not tuned defaults. Milestone 1 tunes them with the operator.

**Units:** positions and distances in tiles; speeds in tiles per second; times in seconds. The runtime converts to pixels internally.

## Player tunables

**Exposed to students as sliders:** run speed, acceleration, jump height, double-jump count, gravity, max fall speed, wall slide speed, climb speed, max HP, attack damage, attack range, attack cooldown.
**Internal (good defaults, not exposed):** coyote time, jump buffer, hit-stop, wall-jump input lock, i-frame duration.

| Setting | Format key | Sketch value |
|---|---|---|
| Run speed | `runSpeed` | 6 |
| Jump height | `jumpHeight` | 3.5 |
| Gravity | `gravity` | 1 |
| Max fall speed | `maxFallSpeed` | 12 |
| Max HP | `maxHp` | 5 |
| Air jumps (double-jump count) | `airJumps` | 1 |
| Wall slide speed | `wallSlideSpeed` | 2 |
| Climb speed | `climbSpeed` | 3 |
| Attack damage | `attack.damage` | 1 |
| Attack range | `attack.range` | 6 |
| Attack cooldown | `attack.cooldown` | 0.25 |
| Starting abilities | `startingAbilities` | `["jump"]` |
| Hit-stop | (internal) | ~0.05 s |
| I-frames | (internal) | ~1 s |

Acceleration (ground and air), coyote time, jump buffer and the wall-jump lock have no sketch value yet; Milestone 1 sets them.

## Abilities

`run`, `jump`, `doubleJump`, `wallJump` (wall slide + wall jump), `wallClimb`, `ropeClimb`, `attack`. Pickups can unlock: double jump, wall jump, wall climb, rope climb, attack. Exact identifiers are set by the format package.

## Enemy settings

| Setting | Values |
| --- | --- |
| Movement | Walk, Jump, Fly, Stationary |
| Damage | Contact only, or Contact + projectile |
| HP | Number of hits to kill |
| Speed | Slider |
| Aggro range | Distance at which it starts chasing |
| Idle behavior | Stand still, or patrol |
| Ledge behavior (walkers) | Turn at ledges, or walk off |
| Wall behavior (flyers) | Pass through walls, or blocked by walls |
| Projectile | Fire rate, projectile speed, destructible by the player's attack (on/off) |
| Contact damage | Damage to the player on touch |
| Jump strength (jumpers) | Fixed, from a setting |

## Placeable elements

| Element | Behavior |
| --- | --- |
| Player start | Exactly one per map |
| Ability pickup | Unlocks one ability |
| Health pickup / max-HP upgrade | Restores HP or raises max HP |
| Rope / vine | Vertical climbable segment of any length |
| Spikes | Damage + return to last safe ground |
| Moving platform | Moves between two points; carries the player |
| Crumbling platform | Falls shortly after the player stands on it, respawns |
| Checkpoint | Sets the respawn point when touched; saves in-game progress |
| Enemy | Instance of an enemy preset, with optional overrides |
| Exit / goal | Ends the game with a win screen |

## Terrains

A terrain is an auto-tiling brush (systems.md § Tile System).

```json
"terrains": [{
  "id": "grass", "name": "Grass ground", "tileset": "ts_forest",
  "layout": "dualGrid", "kind": "solid",
  "slots": { "0": [12], "5": [13, 14], "15": [20, 21, 22] }
}]
```

- `layout`: `blob` (47 slots), `dualGrid` (16 slots), `single` (1 slot).
- `kind`: the collision kind: `solid`, `oneWay`, `climbable`, `hazard`, `none`.
- `slots`: neighbor-pattern index → tile indices in the tileset; more than one index means random variants, picked by a seed from the cell position.
- Animated tiles: a frame list + fps per tile (shape set when the tile system is built).
- Map layers store **terrain ids per logical cell**, not resolved tile indices; the runtime resolves tiles on load. Plain (non-terrain) tiles are stored as tile references.

## Animation states

`idle`, `run`, `jump`, `fall`, `wall-slide`, `climb-wall`, `climb-rope`, `attack`, `hurt`, `death`. A missing animation falls back to the closest state (e.g. no `fall` → `jump` → `idle`); the full fallback chain is set when sprite import is built.

## Project format (sketch)

A starting shape, not a contract. Versioned from day one so old student projects keep loading.

```json
{
  "formatVersion": 1,
  "meta": { "title": "My Game", "author": "" },
  "settings": {
    "tileSize": 16,
    "player": {
      "runSpeed": 6, "jumpHeight": 3.5, "gravity": 1, "maxFallSpeed": 12,
      "maxHp": 5, "airJumps": 1, "wallSlideSpeed": 2, "climbSpeed": 3,
      "attack": { "damage": 1, "range": 6, "cooldown": 0.25 },
      "startingAbilities": ["jump"]
    }
  },
  "assets": {
    "tilesets": [{ "id": "ts_forest", "src": "...", "tileSize": 16 }],
    "sprites": [{ "id": "spr_player", "src": "...", "frameW": 32, "frameH": 32,
                   "anims": { "idle": { "frames": [0, 3], "fps": 8 } } }]
  },
  "enemyPresets": [{
    "id": "bat", "name": "Bat", "sprite": "spr_bat",
    "movement": "fly", "passWalls": true, "speed": 3, "hp": 2,
    "aggroRange": 8, "idle": "patrol", "contactDamage": 1,
    "projectile": null
  }],
  "map": {
    "width": 400, "height": 150,
    "layers": {
      "background": "<RLE or base64 tile ids>",
      "collision":  "<RLE or base64 tile kinds>",
      "foreground": "<RLE or base64 tile ids>"
    }
  },
  "entities": [
    { "id": "e1", "type": "playerStart", "x": 3, "y": 140 },
    { "id": "e2", "type": "abilityPickup", "x": 40, "y": 120, "ability": "doubleJump" },
    { "id": "e3", "type": "enemy", "x": 60, "y": 118, "preset": "bat", "overrides": { "hp": 3 } },
    { "id": "e4", "type": "rope", "x": 80, "y": 100, "length": 8 },
    { "id": "e5", "type": "movingPlatform", "x": 90, "y": 110, "to": { "x": 98, "y": 110 }, "speed": 2 }
  ]
}
```

Open in the sketch: the layer encoding (RLE vs base64); whether `gravity` is a multiplier or an absolute value; the `collision` layer above still holds "tile kinds" while § Terrains stores terrain ids and tile references per layer (settled with systems.md § Tile System → Open questions); where `terrains` sits in the project (top level or under `assets`).
