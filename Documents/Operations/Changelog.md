# Changelog

<!-- Entry template — keep each version to 3–6 bullets. Deep narrative goes in `.claude/memory/logs/phases/phaseN-log.md` or `.claude/memory/logs/issues/issNNN-*.md`; link, don't inline.
- **Landed / Bug + fix.** One sentence: what changed, test delta (`NNNN green, +N net`), issues closed/filed. Link to phase/issue log.
- **Key surfaces.** Files touched + key types/functions, one paragraph. Use `[name](path)` links.
- **Design / Schema / Staleness.** Only the non-obvious hedges; skip if trivial.
- **Rollback.** Named trigger + link to baseline, kill-switch flag if any.
- **GDD deltas.** GDD/Operations/Roadmap/Issues touches + commit message verbatim.
Omit any bullet that has nothing to say. If an entry wants >6 bullets, the extra detail belongs in the phase log. -->

## v0.01 — project setup (2026-10-13) · **the workflow and the design docs; no code yet**

- **Landed.** daedalus started (*Student Platformer Maker*). The Claude workflow is set up: `.claude/` rules, the agents, the skills (`/scope`, `/new-phase`, `/review`, `/design`, `/update`, `/audit`, `/handshake`), the commit-title hook, the memory ledgers, and the phase and plan templates. No code and no test suite yet.
- **Key surfaces.** [CLAUDE.md](../../.claude/CLAUDE.md) (rewritten for a browser runtime + editor sharing one format); the GDD: [GDD.md](../GDD/GDD.md), [architecture.md](../GDD/architecture.md), [systems.md](../GDD/systems.md), [entities.md](../GDD/entities.md), [platforms.md](../GDD/platforms.md), [content.md](../GDD/content.md), [conventions.md](../GDD/conventions.md), [Glossary.md](../GDD/Glossary.md); [Roadmap.md](Roadmap.md) (milestones by name, out-of-scope features as deferred, Open decisions in §4); the tile system (terrain auto-tiling with blob / dual-grid / single layouts, random and animated tiles, a collision kind per tile) in systems.md and entities.md; the audit skill's clusters keyed to `packages/` + `apps/`.
- **Design / Staleness.** Everything in the GDD is **Draft**. Recommendations are written as recommendations; none of the Open decisions is decided yet (Roadmap §4). The repo layout in CLAUDE.md is a proposal for the scaffold plan to confirm.
- **GDD deltas.** GDD created at v0.01. Commit: `v0.01: project setup — Claude workflow and GDD v0.01`.
