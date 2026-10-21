# daedalus — Game Design Document Index
*`daedalus` is the project code name; the working title is **Student Platformer Maker**.*
**Version:** v0.06 · **Stack:** Unity 6 package `com.orcapafea.daedalus` (minimum 6000.0, developed on 6000.6.7f1 and tested on 6000.0.85f1), C#; custom kinematic sim, Unity Tilemap + sprites, URP 2D, students build in the Unity Editor (scaffold landed; exact versions in `.claude/CLAUDE.md` § Project Config) · **Genre:** 2D platformer maker for students; activate the `platformer` (+ `action`) genre profiles

---

> **Draft design.** Everything below is **Draft**. Settled decisions are recorded in `.claude/memory/decisions.md`; what is still open is listed below and in [Roadmap.md](../Operations/Roadmap.md) §4.

## The Pitch

A Unity package where students build their own Ori / Hollow Knight-style platformer **inside the Unity Editor**, without code. They paint a big map with the Tile Palette, place objects and turn them into a player, enemies, ropes, checkpoints, ability pickups and an exit by adding our components, tune them with Inspector sliders, press play from anywhere to test, and share the finished game as a WebGL build.

**Audience:** adults in their early 20s who have barely used Unity. **Learning goals:** placing objects, adding components through the Inspector, and tweaking settings; along the way the Scene view, the Tile Palette, prefabs, Play mode and builds. Code is not a goal: students never open a script.

**Why build it:** asset-store platformer kits such as Corgi Engine and Flare are too hard for beginners: abilities spread over many components, each full of complex settings. daedalus covers only the basics of a normal platformer, with few settings per component and good defaults. (v0.03 replaces the v0.01–v0.02 browser tool; see `decisions.md`.)

## Design Pillars

1. **Students use the tool; they don't read its code.** Parts and Inspector settings, never scripts or event logic. When in doubt, cut a feature rather than expose complexity.
2. **Movement feel first.** The player must feel good with the default settings. Everything else is judged against that bar.
3. **One class session.** On a machine with the package installed, in one session a student can paint, place, tune, test from anywhere, save and share.
4. **Play-from-here.** Testing any spot, with any set of abilities, is one keypress away. It is the most important editor tool.
5. **One genre, done well.** One player, one attack, one configurable enemy, one big continuous map. Scope creep pushes students toward scripting.
6. **Unity's tools first.** Students use Unity's own Scene view, Tile Palette, Inspector, prefabs, undo and builds wherever they exist. We add only what Unity lacks: the game rules as components, play-from-here, validation. Enemy types are ordinary Unity prefabs.
7. **Few settings, good defaults.** One component makes a thing work; each shows at most ~8 settings. If a component needs more, it is doing too much.

## Core Loop

**Student (maker):** paint (Tile Palette) → place objects and add components (Inspector) → tune (Inspector) → play-from-here → fix → repeat → save → WebGL build → share.
**Classmate (player):** open the shared build in a browser → explore one continuous map → collect ability pickups that open new areas → survive enemies and hazards → reach the exit.

**v1 success:** in one class session a student can (1) paint a large map with tiles; (2) place the player, enemies, ropes, checkpoints, ability pickups and an exit by adding components to objects, and paint hazards; (3) tune the player and enemies with Inspector sliders and toggles; (4) press play from any spot, with chosen abilities, and test; (5) save the scene and share the finished game as a WebGL build.

## Open Questions

- **Hosting for shared builds**: decide before `assets-and-sharing` ([Roadmap.md](../Operations/Roadmap.md) §4).
- **Auto-tile layouts on Unity's Tilemap**: Unity has no dual-grid tilemap ([Roadmap.md](../Operations/Roadmap.md) §4).

**Decided (v0.04):** students build objects with Add Component, one Daedalus component per kind of thing plus a few add-ons; at most ~8 settings per component; enemy types are prefabs (students learn prefabs and overrides).
**Decided (v0.05):** no special Play-mode treatment: edits made in Play mode are thrown away as everywhere in Unity; students learn it (and Copy / Paste Component Values to keep a tuned result).
**Decided (v0.03):** a Unity 6 package, `com.orcapafea.daedalus`, installed from OpenUPM; any Unity 6 (minimum 6000.0), developed on 6000.6.7f1 and tested on 6000.0.85f1 too; code-free for students; a custom kinematic sim (no Unity 2D physics for gameplay) on Unity's Tilemap and sprites; Unity's editor replaces the planned React editor.
**Still in force from v0.02:** 16 px tiles; auto-targeting attack only; wall climb only on climbable tiles; collision only from the collision layer; spikes are hazard tiles; fixed default sound effects; a CC0 art pack to start; no student accounts in v1.

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
