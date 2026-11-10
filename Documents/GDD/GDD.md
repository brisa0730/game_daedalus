# daedalus — Game Design Document Index
*`daedalus` is the project code name; the working title is **Student Platformer Maker**.*
**Version:** v0.29 · **Stack:** Unity 6 package `com.orcapafea.daedalus` (minimum 6000.0, developed on 6000.6.8f1 and tested on 6000.0.85f1), C#; custom kinematic sim, Unity Tilemap + sprites, URP 2D, students build in the Unity Editor (scaffold, the movement prototype and Milestone 2 `traps` landed: death and respawn, static and moving traps; the class gallery live at `suwon.orcarin.com` (Korean, public to play since v0.20) with the editor's Share my game, Milestone 3 `class-gallery` complete; package 0.1.0 published on OpenUPM with its Starter sample, 0.2.0 (stages and the Starter's Trap Demo) published on OpenUPM, the first version students install, and 0.2.1 (the trap zone typed in tiles); troll games can be several stages, one scene each; exact versions in `.claude/CLAUDE.md` § Project Config) · **Genre:** 2D platformer maker for students (troll levels first, then an Ori / Hollow Knight-style map); activate the `platformer` (+ `action`) genre profiles

---

> **Draft design.** Everything below is **Draft**. Settled decisions are recorded in `.claude/memory/decisions.md`; what is still open is listed below and in [Roadmap.md](../Operations/Roadmap.md) §4.

## The Pitch

A Unity package where students build their own platformer **inside the Unity Editor**, without code. **The course starts with troll levels** (the genre of games like Level Devil: a level that looks normal, then betrays the player with floors that drop, spikes that pop out and ceilings that fall): short sections, each starting at a checkpoint, built from painted tiles and one **Trap** component. A student who can design those levels is halfway done. Then they grow the same parts into an Ori / Hollow Knight-style map. They paint a big map with the Tile Palette, place objects and turn them into a player, enemies, ropes, checkpoints, ability pickups and an exit by adding our components, tune them with Inspector sliders, press play from anywhere to test, and share the finished game as a WebGL build.

**Audience:** adults in their early 20s who have barely used Unity. **Learning goals:** placing objects, adding components through the Inspector, and tweaking settings; along the way the Scene view, the Tile Palette, prefabs, Play mode and builds. Code is not a goal: students never open a script.

**Why build it:** asset-store platformer kits such as Corgi Engine and Flare are too hard for beginners: abilities spread over many components, each full of complex settings. daedalus covers only the basics of a normal platformer, with few settings per component and good defaults. (v0.03 replaces the v0.01–v0.02 browser tool; see `decisions.md`.)

## Design Pillars

1. **Students use the tool; they don't read its code.** Parts and Inspector settings, never scripts or event logic. When in doubt, cut a feature rather than expose complexity.
2. **Movement feel first.** The player must feel good with the default settings. Everything else is judged against that bar.
3. **One class session.** On a machine with the package installed, in one session a student can paint, place, tune, test from anywhere, save and share.
4. **Play-from-here.** Testing any spot, with any set of abilities, is one keypress away. It is the most important editor tool.
5. **One genre, done well.** One player, one attack, one configurable enemy, one trap, one big continuous map, or a linear run of stages for a troll game. Troll sections, stages and an explorable map are the same platformer built from the same parts. Scope creep pushes students toward scripting: a trap holds its own trigger and reaction, and traps never trigger each other.
6. **Unity's tools first.** Students use Unity's own Scene view, Tile Palette, Inspector, prefabs, undo and builds wherever they exist. We add only what Unity lacks: the game rules as components, play-from-here, validation. Enemy types are ordinary Unity prefabs.
7. **Few settings, good defaults.** One component makes a thing work; each shows at most ~8 settings. If a component needs more, it is doing too much.

## Core Loop

**First lessons (troll levels):** paint a short section → add a Trap to a piece of floor, a spike strip or the exit → play it → watch a classmate fall for it → repeat with the next section, each starting at a checkpoint, or the next stage, a scene of its own.
**Student (maker):** paint (Tile Palette) → place objects and add components (Inspector) → tune (Inspector) → play-from-here → fix → repeat → save → WebGL build → share.
**Classmate (player):** open the shared build in a browser → explore one continuous map → collect ability pickups that open new areas → survive enemies and hazards → reach the exit.

**v1 success:** in one class session a student can (1) paint a large map with tiles; (2) place the player, enemies, ropes, checkpoints, ability pickups and an exit by adding components to objects, and paint hazards; (3) tune the player and enemies with Inspector sliders and toggles; (4) press play from any spot, with chosen abilities, and test; (5) save the scene and share the finished game as a WebGL build on the class gallery.

## Open Questions

- **Auto-tile layouts on Unity's Tilemap**: Unity has no dual-grid tilemap ([Roadmap.md](../Operations/Roadmap.md) §4).

**Decided (v0.26):** a troll game can be a linear run of **stages**, one Unity scene each (reverses v0.09's "one continuous map, no rooms" for troll games, because a troll joke lands better on a fresh screen; the big map stays one scene, and rooms the player walks back and forth between stay deferred). An Exit's optional **Next stage** links them; empty is the win screen. A quick fade, no card. A death stays inside its stage; one death total runs across the game; Play again returns to where Play began. Share my game builds every stage, stops only when nothing can win the chain or a link is broken, and warns about back-links and Exits that differ. HP and abilities will carry between stages when they exist.
**Decided (v0.24):** distribution comes before combat, so students can try what exists: an MIT package at 0.1.0, listed on OpenUPM, with a Starter sample in plain generated art (the default art pack stays in `assets-and-sharing`). Scene autosave is cut: students work in the normal Unity Editor and save for themselves. Students install Unity 6000.0.85f1. A level saved before combat keeps playing exactly as before (a Player setting "Hazard damage" defaults to all HP).
**Decided (v0.20):** the class gallery is **public to browse and play**: anyone with the address sees every game and plays it without signing in, and shares a game's link with 링크 복사; signing in is only for uploading, removing games, passwords and the teacher's page. Games still play only inside the gallery's sandboxed frame. Students appear by a **nickname** the teacher sets (public), and the gallery keeps each student's **real name** for the teacher's pages only; both are editable. Pages ask search engines not to list them (noindex). This replaces v0.12's "only signed-in users can see or play anything" and v0.16's "display name, set by the teacher" (systems.md § Share).
**Decided (v0.16):** the class gallery is in **Korean**; a game card shows the student's display name, set by the teacher (the editor no longer asks for a nickname); Milestone 3 is two phases, `gallery-server` (live) then `share-my-game`, joined by the Share zip contract (platforms.md § Share zip, systems.md § Share).
**Decided (v0.14):** the class gallery's spike settled how games are kept apart from the login: games run on `suwon-play.orcarin.com` in a sandboxed frame; Brotli builds; a 50 MB upload limit; a game keeps one address for life so its saves survive re-uploads; games can reach each other's saves (accepted) (platforms.md § External Services).
**Decided (v0.12):** shared games live on the **class gallery** at `suwon.orcarin.com`, a small web app of ours on the operator's Mac Mini behind a Cloudflare Tunnel: one ID + password account per student plus the teacher's, students upload their own game, only signed-in users can see or play anything; built in Milestone 3 `class-gallery`, right after `traps` (systems.md § Share); the server is Rust (Axum + SQLite).
**Decided (v0.09):** troll levels are how students begin the course, built as Milestone 2 `traps` before combat: one `Trap` component (trigger + one action, no chaining); hazards can respawn at the checkpoint instead of returning to safe ground; every death resets all traps; one continuous map of short sections, no rooms.
**Decided (v0.04):** students build objects with Add Component, one Daedalus component per kind of thing plus a few add-ons; at most ~8 settings per component; enemy types are prefabs (students learn prefabs and overrides).
**Decided (v0.05):** no special Play-mode treatment: edits made in Play mode are thrown away as everywhere in Unity; students learn it (and Copy / Paste Component Values to keep a tuned result).
**Decided (v0.03):** a Unity 6 package, `com.orcapafea.daedalus`, installed from OpenUPM; any Unity 6 (minimum 6000.0), developed on 6000.6.7f1 and tested on 6000.0.85f1 too; code-free for students; a custom kinematic sim (no Unity 2D physics for gameplay) on Unity's Tilemap and sprites; Unity's editor replaces the planned React editor.
**Still in force from v0.02:** 16 px tiles; auto-targeting attack only; wall climb only on climbable tiles; collision only from the collision layer; spikes are hazard tiles; fixed default sound effects; a CC0 art pack to start. (No student accounts in v1 is replaced in v0.12 by class gallery accounts.)

## GDD Files

> **Header convention.** This file's `Version` line is the **version of record** for the GDD as a whole. Every other GDD file carries a `Last updated: vX.YY` line recording the version that last changed *that file*.

| File | Topic |
|------|-------|
| [architecture.md](architecture.md) | Sim / runtime / editor-tools split, assemblies, time model, data flow |
| [systems.md](systems.md) | Per-system contracts: Player, Combat & Damage, Enemies, World & Level Elements, Camera, In-game Progress, Editor Tools, Tile System, Asset Import, Share |
| [entities.md](entities.md) | Authored data, tunable values and defaults, enemy settings, placeable elements, terrains, animation states |
| [conventions.md](conventions.md) | Code + asset conventions, testing & playtesting |
| [platforms.md](platforms.md) | Unity versions, distribution, input, performance budget, storage, hosting |
| [content.md](content.md) | Default art pack, audio, readability, accessibility, student content |
| [Glossary.md](Glossary.md) | Canonical terminology |
| [Changelog.md](../Operations/Changelog.md) | Version history: full narratives |
| [Issues.md](../Operations/Issues.md) | Open bugs & tasks only |
| [Roadmap.md](../Operations/Roadmap.md) | Work plan, deferred features, open decisions |
