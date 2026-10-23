# Memory Index

> **Pure link index: pointers only, no content.** Narrative lives in [Changelog.md](../Documents/Operations/Changelog.md) and the phase logs; rules live in [lessons-learned.md](memory/lessons-learned.md); the plan lives in [Roadmap.md](../Documents/Operations/Roadmap.md). `git log` is the session history; do not maintain a session list here.
> **If a line below needs more than one line, it belongs in one of those files instead.** This file is read at every session start; its job is to say where to look, not to re-tell what happened.

## Current state

Version + stack: [GDD.md](../Documents/GDD/GDD.md) header · Open issues: [Issues.md](../Documents/Operations/Issues.md) · Work plan: [Roadmap.md](../Documents/Operations/Roadmap.md) §1 · Open decisions: Roadmap §4 · Deferred cleanup: [Deferred.md](../Documents/Operations/Deferred.md).

- **Latest: v0.07**: Phase 1 `movement-sim` (pure movement sim, scenario + gate tests; [phase1-log](memory/logs/phases/phase1-log.md)). v0.06: scaffold. v0.05: plain Unity behavior. v0.04: component model. v0.03: Unity platform. v0.02: first decisions. v0.01: project setup.
- **Milestone:** **1 — `movement-prototype`** ([Roadmap.md](../Documents/Operations/Roadmap.md) §1): `movement-sim` done, `movement-runtime` next.
- **Next:** phase 2 `movement-runtime` (prompt ready, handshake-approved: [phase2-movement-runtime.md](../Documents/Phases/phase2-movement-runtime.md)). Next free phase number: **2**.
- ⚠️ **Phase numbers are assigned when a phase STARTS, never in advance.** Take the next free integer from the highest `phaseN-*.md` in `Documents/Phases/` and `Documents/Phases/archive/`, **never** from `Roadmap.md` §1, which lists unstarted work by name and order only.

## Recent work
`git log --oneline --since=7.days`. Per-version narrative: [Changelog.md](../Documents/Operations/Changelog.md), newest on top.

## Rules & preferences
- [feedback.md](memory/feedback.md): workflow preferences, git policy
- [lessons-learned.md](memory/lessons-learned.md): architectural and process rules
- [decisions.md](memory/decisions.md): architectural decision log

## Architecture & references
- [architecture.md](../Documents/GDD/architecture.md): system map, ownership, package boundaries (the GDD is the home)
- [framework.md](memory/framework.md): Unity / package gotchas

## Logs
- [logs/phases/](memory/logs/phases/): per-phase implementation logs
- [logs/issues/](memory/logs/issues/): per-issue narrative logs

## Audits
- [audit-log.md](memory/audit-log.md): findings + dispositions from `/audit` runs
