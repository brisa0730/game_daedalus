---
name: scope
description: Evaluate a work request, decide patch / plan / phase, and execute or route accordingly. Use when the user wants the routing decision made for them; skip when they already know the scope.
disable-model-invocation: false
model: opus
effort: high
argument-hint: "[work description]"
---

# Scope: $ARGUMENTS

Decide the right level of ceremony for this work, then execute or hand off.

## Decision Algorithm

Ask these in order. **First yes wins.**

1. **Feature build?** — New system, save-format migration, new mechanic, GDD delta, touches 3+ systems. → **Phase**
2. **Rule / convention / structural change?** — CLAUDE.md, skills, agents, memory layout, GDD section structure, hooks, `.claude/settings.json`. → **Plan**
3. **Blast radius > 3 files OR > 1 directory?** — Multi-file refactor, doc restructure, cross-cutting rename. → **Plan**
4. **3+ sequential steps with distinct failure modes between them?** — Where a failure mid-sequence leaves the repo in a half-migrated state. → **Plan**
5. **Otherwise** → **Patch**

If two rules could apply, pick the heavier ceremony and call out the ambiguity to the user.

## First action

Before executing, state the decision in one sentence:
> "Scoping as [patch / plan / phase] because [one reason from the algorithm]."

If the user pushes back ("that's overkill" / "this deserves more care"), re-run the algorithm with their additional context.

---

## Patch path

No plan file, no ceremony.

1. Do the work.
2. Run tests if code changed (see CLAUDE.md `Testing` section for command).
3. Follow `Log Discipline — Write Once` in CLAUDE.md: update Issues.md + Changelog.md as applicable. Skip state/MEMORY touches — those are covered by the header/index convention.
4. Commit with a clear, focused message.

## Plan path

Follow `Documents/Plans/plan-template.md`.

1. Draft `Documents/Plans/[short-name].md` **from the template file itself** (read `Documents/Plans/plan-template.md` — don't crib from sibling plans in `archive/`, they may predate the current template or carry local additions). Fill in: Overview (plain English, written LAST — see the template's inline rule) / Source / File Manifest (code + tooling only — no Operations/Changelog/Issues/memory) / Steps (drafting units, not commits) / Completion Criteria / Notes to GDD / Open Risks (if any).

   **Anti-patterns to catch at drafting time:**
   - A step titled "Changelog + GDD bump" or "Update Issues.md" — doc/memory updates are Post-Plan Cleanup, not plan steps.
   - Operations/Changelog/Issues/Roadmap/memory files in the File Manifest table.
   - Per-step `**Commit:**` trailers or a "Commit 1 / Commit 2 / …" structure — plan lands as one commit.
2. Self-review: verify the Completion Criteria actually cover the goal.
3. Run a critic sub-agent. Prompt:
   > "Read this plan at `Documents/Plans/[short-name].md` + the files it proposes to change. Report HIGH/MED/LOW issues: information loss, broken links, plan gaps, CLAUDE.md conflicts. Be concrete — cite file:line."
4. Fold critic findings back INTO the plan (revise, don't annotate). Mark fixed items in the plan's trailer.
5. Present plan to user for approval. List unresolved critic findings explicitly.
6. On approval: execute step-by-step. Do **not** commit between steps — commit once at the end after code review passes (or a small number of logically-grouped commits if the diff is too large to review as one).
7. Follow the `Post-Plan Cleanup` section of the template — append execution trailers, `git mv` to `Documents/Plans/archive/`.

## Phase path

Phases are GDD-driven feature implementations. Defer to `/new-phase`.

1. Verify the work matches the phase shape (GDD delta, multi-system, design-review-worthy). If it looks like "just a big plan," downgrade to Plan path.
2. Pick the next phase number — highest `phaseN-*.md` across `Documents/Phases/` and `Documents/Phases/archive/`, plus 1. ⚠️ **Never from `Roadmap.md` §1: forward-planned phases carry names and order only, no numbers**. Numbers are assigned at execution and are permanent.
3. Propose `[N] [short-name]` to the user, where `[N]` is the number from step 2 and `[short-name]` comes from the Roadmap entry's **name** (e.g. `1 movement-prototype`).
4. On confirmation: invoke `/new-phase [N] [short-name]`. That skill owns the rest — extract & categorize, GDD update, design review, codebase scan, phase-planner agent.

## When NOT to invoke this skill

- The user already said "patch X" / "plan X" / "phase X" — skip the decision, just execute.
- Trivial single-file edits the user described concretely ("rename foo to bar in file Y").
- Questions or discussions (no work to scope yet).

This skill is a pause-and-think mechanism. It has value when scope is genuinely ambiguous. It has cost (one extra round trip) when it isn't.
