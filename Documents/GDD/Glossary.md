# Glossary
**Last updated:** v0.16

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
| Shared build | Draft | A WebGL build of a student's game, uploaded to the class gallery as a Share zip and played in a browser. |
| Class gallery | Draft | The website at `suwon.orcarin.com` where students sign in to upload their games and anyone can browse and play them (since v0.20); our web app on the operator's Mac Mini. |
| Games host | Draft | `suwon-play.orcarin.com`: the class gallery's second hostname, which serves the shared builds into a sandboxed frame so a game can't use the gallery's login. |
| Play grant | Draft | The signed, short-lived token the class gallery puts in a game frame's address; the games host trades it for a per-game cookie. Carries the game, not the user. |
| Share my game | Active | The editor menu item (Daedalus → Share my game) that makes a WebGL build and zips it with a title and a screenshot as a Share zip, ready to upload. |
| Share zip | Active | The one file Share my game makes and the class gallery accepts: `daedalus.json`, `screenshot.png` and the four Brotli build files (platforms.md § Share zip). |
| Nickname | Draft | The name the class gallery shows publicly for a student: on their game cards, the play view and the header. Set and changed by the teacher (`display_name` in the database; was Display name until v0.20). |
| Real name | Draft | The name the teacher knows a student by; the class gallery shows it only on the teacher's pages (students list, account slips, remove confirm). |
| Account slip | Draft | The printable slip the class gallery shows once when the teacher creates or resets an account: address, ID, password. |
| Player page | Draft | The games host's own page that loads a shared build; the zip's `index.html` is never used. |
| Game address | Draft | A shared game's address on the games host, `/g/<id>/`, fixed for life; re-uploads replace the game there, so saves survive. |
| Play-from-here | Draft | Entering Play mode at the Scene-view cursor with a chosen ability set; leaving Play mode returns to the same view. |
| Play from start | Draft | Unity's Play button: starting at the Player start with only the starting abilities, the real experience. |
| Live tuning | Draft | Changing component settings in the Inspector during Play mode to try them out. As everywhere in Unity, the changes are thrown away when Play mode ends; Copy / Paste Component Values keeps a result. |
| Ability | Draft | One of the player's built-in moves (run, jump, double jump, wall jump, wall climb, rope climb, attack); on from the start or unlocked by a pickup. |
| Ability pickup | Draft | A placed element that unlocks one ability. |
| Ability gating | Draft | Progression by making areas reachable only with a given ability. |
| Enemy type | Draft | A prefab of an object with an Enemy component (e.g. "Bat"). |
| Override | Draft | Unity's prefab override: a placed enemy's change to its prefab's values. |
| Safe ground | Draft | The last position the player stood fully on painted non-hazard tiles, outside every trap trigger and trap box; with On hazard = Return to safe ground, hazards return the player there. |
| Checkpoint | Draft | A placed element (`Checkpoint` component) that sets the respawn point when touched and, from `world-systems`, saves in-game progress. |
| Exit | Draft | A placed element (`Exit` component) that ends the game with the win screen when touched. |
| Respawn point | Draft | Where Respawn at checkpoint brings the player back: where Play began, then the latest checkpoint touched. |
| Active radius | Draft | The area (~1.5 screens) around the camera inside which objects update. |
| Collision Tilemap | Draft | The one of the three Tilemaps (Background, Collision, Foreground) whose tiles affect gameplay. |
| Tile kind | Draft | A tile's or terrain's collision type: solid, one-way, climbable, hazard, none. Counts only on the Collision Tilemap. |
| Terrain | Draft | An auto-tiling brush (e.g. "Grass ground"): a tile asset with a layout, a collision kind and the sprites for each neighbor pattern. |
| Auto-tiling | Draft | Picking each painted cell's edge / corner / inner-corner tile from its neighbors. |
| Blob layout | Draft | The 47-tile auto-tile layout (8-neighbor bitmask; a diagonal counts only with both adjacent edges). |
| Dual-grid layout | Draft | The 16-tile auto-tile layout drawn half a tile off the logical grid; collision stays on the logical grid. |
| Tile Palette | Draft | Unity's window for painting tiles and terrains onto a Tilemap. |
| Operator | Draft | The person directing the project and making its design decisions. |
| Contact state | Draft | What the player is touching and moving by: air, ground, one-way top, wall slide, wall cling or rope (`PlayerContact`). |
| Coyote time | Draft | A short window (0.1 s) after walking off a ledge in which a jump still counts as a ground jump. |
| Wall coyote | Draft | The same window after leaving a wall slide or cling: a wall jump still works. |
| Jump buffer | Draft | A jump pressed shortly (0.1 s) before landing or reaching a wall fires as soon as it can. |
| Air jump | Draft | A jump in mid-air with double jump; as many as the Air jumps setting. |
| Minimum hop | Draft | The height (1.25 tiles) of a jump released at once. |
| Jump-press priority | Draft | The one order in which a jump press is resolved: drop-through > ground/coyote > rope > wall > air > buffer. |
| Wall slide | Draft | Sliding slowly down an ordinary wall while pressing into it (needs wall jump). |
| Wall jump | Draft | A jump off a wall that pushes away from it. |
| Wall-jump input lock | Draft | A short time (0.2 s) after a wall jump in which horizontal input is ignored. |
| Wall cling | Draft | Holding onto a climbable wall, climbing up and down it (needs wall climb). |
| Ledge pop | Draft | Climbing over the open top of a climbable wall onto its top. |
| Drop-through | Draft | Down + jump on a one-way platform: falling through it. |
| One-way platform | Draft | A tile you jump through from below and stand on from above. |
| Climbable wall | Draft | A tile (kind climbable) that wall climb can cling to; otherwise a solid wall. |
| Rope | Draft | A vertical, ladder-style rope or vine; in the sim a rope segment (centre x, bottom, top). In the scene, the **Rope** component: it hangs down from its object by its Length (tiles), read at play start; its top snaps onto a one-way surface within 0.5 tile. |
| Re-grab lockout | Draft | A short time (0.2 s) after jumping off or letting go of a rope in which it can't be grabbed again. |
| Hitbox | Draft | The player's collision box: 0.75 × 1.5 tiles; its position is the feet centre. |
| Grip point | Draft | The hitbox centre, 0.75 tile above the feet: where ropes and climbable walls are gripped. |
| Base gravity | Draft | Gravity at multiplier 1: 57.142857 tiles/s², from a 0.35 s time to apex at jump height 3.5. |
| Gravity multiplier | Draft | The Gravity setting: a multiple of base gravity; it changes jump timing, never jump height. |
| Time to full speed | Draft | The setting for how fast the player reaches run speed from rest on the ground, in seconds. |
| Ceiling-corner correction | Draft | Sliding a rising head at most 0.25 tile sideways past a ceiling corner it barely clips. |
| Nearest free start | Draft | Moving a start that is inside solid to the nearest free spot within 8 cells. |
| Map bounds | Draft | The map's sides and top are solid; its bottom is open (falling out is a death: a pit). The map covers the Collision Tilemap's painted cells, grown to include the start, the ropes, traps (and every Move trap's end), checkpoints and exits, plus the **map headroom**: 24 empty rows on top. |
| Tile grid | Draft | The sim's 0-based grid of tile kinds, filled from the Collision Tilemap at play start. |
| Player (component) | Draft | The one Daedalus component that makes an object the player; its placed position is the start. Sections: Movement and Abilities. |
| Daedalus tile | Draft | A tile asset with a collision kind (`DaedalusTile`). Plain tiles on the Collision Tilemap count as solid. |
| Collision kind | Draft | What a tile does on the Collision Tilemap: None, Solid, One-way, Climbable, Hazard (4: doesn't block, kills). |
| Play start | Draft | Where and with which abilities the player starts (`PlayStart`): the placed position and the toggles, or what play-from-here passes. |
| Player session | Draft | The runtime's driver for one Player's sim: clock, ticks, interpolation and restarts (`PlayerSession`). |
| Feet offset | Draft | How far the sprite's bottom edge sits from the object's position; it puts the sprite's bottom on the sim's feet. |
| Reset snap | Draft | Drawing a fall-out reset at the start at once, without sliding there (`Teleported`). |
| Play-start scan | Draft | The one allowed scene search (rule 5 exception): one `FindObjectsByType` scan plus a deterministic sort at play start, never per frame. |
| Scenario test | Draft | An EditMode test that steps the sim through a scripted input sequence on a small ASCII map and checks it tick by tick. |
| Troll level | Draft | A short section that looks normal, then betrays the player with traps (the genre of games like Level Devil); how students begin the course. |
| Section | Draft | One short stretch of the continuous map, starting at a checkpoint; troll levels are built as sections side by side. |
| Trap | Draft | The Daedalus component (`Trap`) that makes a box of tiles move, fall, vanish or appear when the player touches it or enters its zone; fires once per life; never triggers another trap. |
| Trap zone | Draft | A trap's trigger box, drawn in the Scene view and stored relative to the trap's object (default: the trap's box grown up by 1 tile); traps sharing a zone fire together. |
| Trigger | Draft | What sets a trap off: Player enters zone, or Player touches (the contact margin). |
| Touch | Draft | Contact on the player's hitbox grown by the contact margin (0.0625 tile); used by Touch triggers, the Checkpoint and the Exit. |
| Hazard core | Draft | The player's hitbox inset by 0.2 tile per side; a hazard kills only when it overlaps the core. |
| Re-arm | Draft | A trap that a death reset fires again only after the player has been outside its trigger, from every start source. |
| Push-out | Active | A Solid trap appearing on the player moves it out the shortest free way (ties: up, left, right, down); with no way free, it crushes. |
| Trap cells | Draft | The sim's second cell layer in `TileGrid` for present traps; painted tiles stay in charge, Solid beats Hazard. |
| Moving solid | Active | A Solid trap in motion (Move or Fall): it carries a player standing on it, pushes with its leading face and can crush the player; carrying is soft. |
| Moving-box core | Active | The sim layer for traps in motion: boxes between cells, beside the grid; a trap at rest is trap cells again. Reused by `world-systems`' moving and crumbling platforms. |
| Crush | Active | Death when a moving solid pushes the player into the grid (after the crush nudge), two pushes squeeze it, or a Solid trap appears on the player with no way out. |
| Rider (carry) | Draft | A player standing on a moving Solid trap's top; the trap carries it, Y then X, softly (a wall stops it). |
| Pull-away | Draft | A trap going down faster than the rider's ride speed leaves it: the player is in the air with the 6-tick coyote window. |
| Ride speed | Draft | How fast a trap going down may sink and keep its rider: the player's fall speed on landing (0 if standing before it moved), growing by fall gravity up to Max Fall Speed. |
| Leading face | Draft | The face of a moving trap that moves into the player; only it pushes. |
| Crush nudge | Draft | The up-to-0.25-tile shift at right angles to a blocked push that saves the player from a crush (`LevelRules.CrushNudge`). |
| Move end | Draft | Where a Move trap stops: its box plus its Offset; dragged in the Scene view by a handle that snaps to whole tiles. |
| Landing outline | Draft | The Scene-view dotted box where a Fall trap will land (from the sim's own landing rule), or "Falls out of the map". |
| Runaway Exit | Draft | An Exit with a Move trap on it: it runs as the player comes near and is won at its end. |
| On hazard | Draft | The Player setting for what a hazard does: Return to safe ground (default) or Respawn at checkpoint. |
| Death beat | Draft | The 0.4 s (24-tick) flash and burst after any death before the player reappears; input is ignored; the same for spikes, pits and crush. |
| Death counter | Draft | The skull and number in the top-left corner counting every death since Play began. |
| Win screen | Draft | The dimmed freeze after touching the Exit: "You made it!", the deaths, and after 1 s any key plays again. |
| HUD | Draft | The on-screen layer built in code at play start (uGUI): the death counter and the win screen. |
| Follow camera | Draft | The main camera following the player, clamped to the map, cut on respawn (a snap follow in v0.10; smoothing in `world-systems`). |
| Level stepper | Draft | `LevelStepper`: the sim's world step around `PlayerStepper` (death, respawn, checkpoints, exit, traps). |
| Level rules | Draft | `LevelRules`: the one source of death, respawn and trap numbers. |
