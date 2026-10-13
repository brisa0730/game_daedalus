# daedalus — Game Design Document Index
*`daedalus` is the project code name; the working title is **Student Platformer Maker**.*
**Version:** v0.02 · **Stack:** browser web app, TypeScript; custom runtime with a Canvas 2D renderer, React editor (no code yet; exact versions will live in `.claude/CLAUDE.md` § Project Config) · **Genre:** 2D platformer maker for students; activate the `platformer` (+ `action`) genre profiles

---

> **Draft design.** Everything below is **Draft**. Settled decisions are recorded in `.claude/memory/decisions.md`; what is still open is listed below and in [Roadmap.md](../Operations/Roadmap.md) §4.

## The Pitch

A browser tool where students build their own Ori / Hollow Knight-style platformer without Unity and without code. They paint a big map with tiles, drop in a player start, enemies, hazards, ropes, checkpoints, ability pickups and an exit, tune the hero and the enemies with sliders, press play from anywhere to test, and share the finished game as a link.

It replaces an earlier idea, a simplified Unity kit: the goal is to remove the Unity learning cost entirely.

## Design Pillars

1. **Students use the tool; they don't read its code.** Parts and settings, never scripts or event logic. When in doubt, cut a feature rather than expose complexity.
2. **Movement feel first.** The player must feel good with the default settings. Everything else is judged against that bar.
3. **One class session.** In one session a student can paint, place, tune, test from anywhere, and save and share.
4. **Play-from-here.** Testing any spot, with any set of abilities, is one keypress away. It is the editor's most important feature.
5. **One genre, done well.** One player, one attack, one configurable enemy, one big continuous map. Scope creep pushes the tool toward Unity.

## Core Loop

**Student (maker):** paint and place → tune → play-from-here → fix → repeat → save → share a link.
**Classmate (player):** open the link → explore one continuous map → collect ability pickups that open new areas → survive enemies and hazards → reach the exit.

**v1 success:** in one class session a student can (1) paint a large map with tiles; (2) place the player start, enemies, hazards, ropes, checkpoints, ability pickups and an exit; (3) tune the player and enemies with sliders and toggles; (4) press play from any spot, with chosen abilities, and test; (5) save the project and share the finished game as a link.

## Open Questions

- **Hosting for shared links**: decide before `assets-and-sharing` ([Roadmap.md](../Operations/Roadmap.md) §4).

**Decided (v0.02):** custom runtime with a Canvas 2D renderer; React editor; 16 px tiles; auto-targeting attack only; wall climb only on climbable tiles; collision only from the collision layer; spikes are hazard tiles; all three auto-tile layouts (dual-grid + single built first); fixed default sound effects; a CC0 art pack to start; no student accounts in v1.

## GDD Files

> **Header convention.** This file's `Version` line is the **version of record** for the GDD as a whole. Every other GDD file carries a `Last updated: vX.YY` line recording the version that last changed *that file*.

| File | Topic |
|------|-------|
| [architecture.md](architecture.md) | Runtime / editor / format split, package boundaries, time model, data flow |
| [systems.md](systems.md) | Per-system contracts: Player, Combat & Damage, Enemies, World & Level Elements, Camera, In-game Progress, Editor, Tile System, Asset Import, Share |
| [entities.md](entities.md) | Project format, tunable values and defaults, enemy settings, placeable elements, terrains, animation states |
| [conventions.md](conventions.md) | Code + asset conventions, testing & playtesting |
| [platforms.md](platforms.md) | Browser target, input, performance budget, storage, hosting |
| [content.md](content.md) | Default art pack, audio, readability, accessibility, student content |
| [Glossary.md](Glossary.md) | Canonical terminology |
| [Changelog.md](../Operations/Changelog.md) | Version history: full narratives |
| [Issues.md](../Operations/Issues.md) | Open bugs & tasks only |
| [Roadmap.md](../Operations/Roadmap.md) | Work plan, deferred features, open decisions |
