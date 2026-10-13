# Changelog

<!-- Entry template — keep each version to 3–6 bullets. Deep narrative goes in `.claude/memory/logs/phases/phaseN-log.md` or `.claude/memory/logs/issues/issNNN-*.md`; link, don't inline.
- **Landed / Bug + fix.** One sentence: what changed, test delta (`NNNN green, +N net`), issues closed/filed. Link to phase/issue log.
- **Key surfaces.** Files touched + key types/functions, one paragraph. Use `[name](path)` links.
- **Design / Schema / Staleness.** Only the non-obvious hedges; skip if trivial.
- **Rollback.** Named trigger + link to baseline, kill-switch flag if any.
- **GDD deltas.** GDD/Operations/Roadmap/Issues touches + commit message verbatim.
Omit any bullet that has nothing to say. If an entry wants >6 bullets, the extra detail belongs in the phase log. -->

## v0.02 — first decisions (2026-10-13) · **docs only, no code**

- **Landed.** The operator settled the open decisions except hosting: a custom runtime with a Canvas 2D renderer (no game engine), a React editor, 16 px tiles, the auto-targeting attack only, wall climb only on climbable tiles, collision only from the collision layer, spikes as hazard tiles (no longer a placed element), all three auto-tile layouts with dual-grid + single built first, fixed default sound effects, a CC0 art pack to start, no student accounts in v1. Schedule: the runtime accepts a start position + ability set from `movement-prototype`, live movement sliders on the test map, a student check halfway through `editor-core`; the one-day engine spike shrinks to a render check inside the scaffold.
- **GDD deltas.** GDD v0.01 → v0.02: [GDD.md](../GDD/GDD.md) (header, Open Questions), [architecture.md](../GDD/architecture.md) (§ Technology), [systems.md](../GDD/systems.md) (Player, World, Tile System, Editor), [entities.md](../GDD/entities.md) (spikes, terrain kind), [platforms.md](../GDD/platforms.md), [content.md](../GDD/content.md), [Glossary.md](../GDD/Glossary.md); [Roadmap.md](Roadmap.md) §1 and §4 (only hosting left open); CLAUDE.md § Project Config. Commit: `v0.02: first decisions — custom Canvas 2D runtime, React editor, 16 px tiles, tile-system rules`.

## v0.01 — project setup (2026-10-13) · **the workflow and the design docs; no code yet**

- **Landed.** daedalus started (*Student Platformer Maker*). The Claude workflow is set up: `.claude/` rules, the agents, the skills (`/scope`, `/new-phase`, `/review`, `/design`, `/update`, `/audit`, `/handshake`), the commit-title hook, the memory ledgers, and the phase and plan templates. No code and no test suite yet.
- **Key surfaces.** [CLAUDE.md](../../.claude/CLAUDE.md) (rewritten for a browser runtime + editor sharing one format); the GDD: [GDD.md](../GDD/GDD.md), [architecture.md](../GDD/architecture.md), [systems.md](../GDD/systems.md), [entities.md](../GDD/entities.md), [platforms.md](../GDD/platforms.md), [content.md](../GDD/content.md), [conventions.md](../GDD/conventions.md), [Glossary.md](../GDD/Glossary.md); [Roadmap.md](Roadmap.md) (milestones by name, out-of-scope features as deferred, Open decisions in §4); the tile system (terrain auto-tiling with blob / dual-grid / single layouts, random and animated tiles, a collision kind per tile) in systems.md and entities.md; the audit skill's clusters keyed to `packages/` + `apps/`.
- **Design / Staleness.** Everything in the GDD is **Draft**. Recommendations are written as recommendations; none of the Open decisions is decided yet (Roadmap §4). The repo layout in CLAUDE.md is a proposal for the scaffold plan to confirm.
- **GDD deltas.** GDD created at v0.01. Commit: `v0.01: project setup — Claude workflow and GDD v0.01`.
