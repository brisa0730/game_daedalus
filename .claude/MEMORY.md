# Memory Index

> **Pure link index: pointers only, no content.** Narrative lives in [Changelog.md](../Documents/Operations/Changelog.md) and the phase logs; rules live in [lessons-learned.md](memory/lessons-learned.md); the plan lives in [Roadmap.md](../Documents/Operations/Roadmap.md). `git log` is the session history; do not maintain a session list here.
> **If a line below needs more than one line, it belongs in one of those files instead.** This file is read at every session start; its job is to say where to look, not to re-tell what happened.

## Current state

Version + stack: [GDD.md](../Documents/GDD/GDD.md) header · Open issues: [Issues.md](../Documents/Operations/Issues.md) · Work plan: [Roadmap.md](../Documents/Operations/Roadmap.md) §1 · Open decisions: Roadmap §4 · Deferred cleanup: [Deferred.md](../Documents/Operations/Deferred.md).

- **Latest: v0.16**: Phase 5 `gallery-server` (the Korean class gallery live at `suwon.orcarin.com` under launchd: accounts, teacher page, upload, play, backups; ISS-002 open; [phase5-log](memory/logs/phases/phase5-log.md)). v0.15: ISS-001 fixed (a stale Build Settings scene blanked every WebGL build; [plan](../Documents/Plans/archive/webgl-blank-builds.md)). v0.14: class gallery spike (games on `suwon-play.orcarin.com` in a sandboxed frame, Brotli, 50 MB uploads; [plan](../Documents/Plans/archive/class-gallery-spike.md)); ISS-001 found (WebGL builds draw nothing). v0.13: Phase 4 `moving-traps` (Move and Fall on the moving-box core, riding, pushing, crush nudge, runaway exit, Scene-view previews; Milestone 2 `traps` complete; [phase4-log](memory/logs/phases/phase4-log.md)). v0.12: class gallery decided (`suwon.orcarin.com`, logins + upload, Rust + SQLite server, new Milestone 3 `class-gallery` after `traps`). v0.11: moving traps designed (Move and Fall rules, editor previews). v0.10: Phase 3 `death-and-respawn` (hazards, pits, checkpoint, exit, static traps, HUD, follow camera; editor check and play-check passed; [phase3-log](memory/logs/phases/phase3-log.md)). v0.09: design: troll levels open the course, new Milestone 2 `traps`. v0.08: Phase 2 `movement-runtime` (Player, Rope, Collision Tilemap read, controls, dev scene, play-check passed; [phase2-log](memory/logs/phases/phase2-log.md)). v0.07: Phase 1 `movement-sim` ([phase1-log](memory/logs/phases/phase1-log.md)). v0.06: scaffold. v0.05: plain Unity behavior. v0.04: component model. v0.03: Unity platform. v0.02: first decisions. v0.01: project setup.
- **Milestone:** 3 **`class-gallery`**: Phase 5 `gallery-server` done (v0.16, live); next the `share-my-game` phase ([Roadmap.md](../Documents/Operations/Roadmap.md) §1).
- **Next:** the `share-my-game` phase prompt (`/new-phase`), built to platforms.md § Share zip. Next free phase number: **6**. Open: ISS-002 (a game sometimes stays black). Live gallery ops: CLAUDE.md § Build / run (redeploy line).
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
