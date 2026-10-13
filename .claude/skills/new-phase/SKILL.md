---
name: new-phase
description: Distill this session's design decisions into a focused phase prompt. Use after a design conversation.
disable-model-invocation: false
model: opus
effort: xhigh
argument-hint: "[phase-number] [short-name]"
---

# New Phase: Phase $ARGUMENTS

Generate a phase implementation prompt from the current session's design decisions.

## Pre-check

Validate `$ARGUMENTS` before proceeding:
- Expected format: `[number] [short-name]` (e.g., `3 auth`, `10 integration`)
- **If the number is missing:** take the highest `phaseN-*.md` across `Documents/Phases/` and `Documents/Phases/archive/` (regardless of status), and use `N+1` as the suggested index.
  - ⚠️ **Do NOT read the number off `Roadmap.md` §1 — forward-planned phases deliberately have no number** (see the note at the top of §1). A phase is assigned the next free integer **when it starts**, and keeps it forever. The Roadmap tells you *which work is next by name*; the `phaseN-*.md` files on disk tell you *what number is free*.
- **If the name is missing:** scan the session for the dominant theme (the category with the most decisions in Step 1's categorization) and propose a 1–3 word snake-case short-name derived from it (e.g., `evaluation-enrichment`, `tier-graduation`).
- **If both are missing / arguments empty:** suggest both as above, present the proposed `[N] [name]` to the user, and ask for confirmation or override before proceeding. Do not silently assume.
- Check `Documents/Phases/` — if an un-archived `phase[N]-*.md` with the chosen number already exists (i.e. a Pending or In Progress phase, not yet moved to `archive/`), **warn the user** and ask for confirmation before overwriting.

## Workflow

> **This skill does not write the GDD.** Decisions live in the phase prompt (and `decisions.md`) until the phase is implemented; the GDD is updated when the phase finishes, via `phase-template.md` → *Notes to GDD* + *Post-Phase Cleanup*.

### Step 1: Extract & Categorize (inline)

Review the full conversation and extract every design decision, agreement, or specification discussed. Then:

1. **Group** decisions into categories by topic/system (e.g., "Auth," "API," "Database," "UI").
2. **Rank** categories by scope — which has the most decisions, the deepest impact, or the most interconnected changes.
3. **Assess whether one phase is enough:**
   - **Split into multiple phases if any of these hold:**
     - Two or more categories each carry substantial scope (≥3 non-trivial decisions or touch ≥3 modules) and are not tightly coupled.
     - A single category's change surface is too large for one phase (roughly: >6 files across >3 modules, OR introduces multiple new systems, OR mixes independent concerns like persistence + API + UI).
     - Decisions have natural sequencing (A must land before B is meaningful) — propose them as consecutive phases rather than one blended phase.
   - **Otherwise:** pick the single largest category as the phase focus and defer the rest.
4. **Propose phase layout to the user:**
   - **Single phase:** main theme + decisions included.
   - **Multiple phases:** propose `Phase N: [name]`, `Phase N+1: [name]`, … each with its own theme, decisions, and a one-line rationale for why they're separate. Recommend which to tackle first based on dependencies.
5. **Defer the rest** — anything not in the proposed phase(s) goes to the deferred list with a one-line reason (e.g., "unrelated to main theme," "only one minor decision," "depends on systems not yet built").
6. **Re-evaluate prior deferrals:**
   - Read the Deferred Features section of `Documents/Operations/Roadmap.md` (and `Documents/Operations/Deferred.md`) — check if any previously deferred items match the current main theme (or any of the proposed phases, if split). If matches are found, explicitly flag them to the user and pull them in. Items already captured as understood calibration/observation work stay where they are; items elsewhere get formalized in the GDD when the phase that carries them is implemented.

Present the categorization to the user:
- Proposed phase(s) and the decisions included in each
- If split: the proposed execution order based on dependencies
- **Recovered from backlog** — any prior deferred items that match, showing their tag and when they were originally deferred
- Deferred items (new) and why
- Ask for confirmation before proceeding. Once confirmed, **Steps 2–4 run for each proposed phase** (in dependency order). Each phase gets its own design review, codebase scan, and phase-planner pass. The user may still narrow the set at this prompt (e.g., "generate only phases N and N+1, defer N+2").

### Steps 2–4: Execute per phase

**If Step 1 produced a single phase**, run Steps 2–4 once below.
**If Step 1 produced multiple phases**, run Steps 2–4 as a loop — once per phase, in dependency order. Use each phase's own in-scope decisions (not the combined set) as the input for that iteration. Do not blend reviews or plans across phases.

Track per-phase artifacts as you go so Step 6 can save them all: `{phase_number, phase_name, in_scope_decisions, design_review_output, codebase_scan_output, phase_prompt_draft}`.

#### Step 2: Design Review (optional, parallel with Step 3)

Skip this step for a given phase if it has fewer than 3 in-scope decisions and introduces no new systems.

**Agent — design-review** (Plan):
Read the directive at `.claude/agents/workflow/design-review.md` and execute it.
Feed it only this phase's in-scope decisions. Stash the panel's synthesis with the phase's artifacts.

#### Step 3: Scan codebase (agent)

**Agent — codebase-scan** (Explore):
Read the directive at `.claude/agents/workflow/codebase-scan.md` and execute it.
Use this phase's in-scope decisions as search targets. Stash the scan with the phase's artifacts.

When multiple phases are being generated, Step 2 and Step 3 calls across phases can be dispatched in parallel as long as each is given its own per-phase inputs.

#### Step 4: Plan (after this phase's Steps 2–3 complete)

**Agent — phase-planner** (Plan):
Read the directive at `.claude/agents/workflow/phase-planner.md` and execute it.
Feed it this phase's in-scope decisions, its codebase scan from Step 3, and its design review output from Step 2 (if available).
The phase number and name are this iteration's phase, not `$ARGUMENTS`.
The decisions are **not yet in the GDD** — the phase prompt is their canonical home until implementation, and its *Notes to GDD* section carries them for sync-back when the phase lands.

**Contract:** phase-planner is a Plan-type subagent with no Write tool. It RETURNS the phase prompt as markdown content in its final message (wrapped in a ````markdown` fence). The parent session extracts that block and saves it at Step 6 via `Write`. Do NOT instruct the agent to save the file itself — it will fail with `tool not available` and waste a round trip.

After the loop completes, proceed to Step 5.

### Step 5: Archive Previous Phase

Before saving the new prompt, archive any completed phase. The active/archive directory split *is* the status: a prompt still in `Documents/Phases/` is Pending or In Progress; once its implementation has landed it's Done.
1. List `Documents/Phases/*.md` (ignore `phase-template.md`) — these are the un-archived prompts.
2. For each whose implementation has completed, `git mv` it from `Documents/Phases/` to `Documents/Phases/archive/` (create the folder if needed). If it's unclear whether an un-archived phase is finished, confirm with the user before archiving.

### Step 6: Output

For **each** generated phase:
1. Save the phase prompt to `Documents/Phases/phase[N]-[name].md`
2. The saved prompt file *is* the Pending record — its presence in `Documents/Phases/` (un-archived) marks it Pending. Nothing else to update.

Then once per invocation:
3. Update `Documents/Operations/Roadmap.md` — remove recovered items consumed by any generated phase from §3; add newly deferred items with date and one-line reason. The phase's decisions live in its phase prompt (not the GDD yet) — reference the phase **by name** from Roadmap if they have pre-launch gating (never by number — an unstarted phase has none) (the prompt file gets archived on completion, so don't link its path). The GDD is updated when the phase is implemented.
4. Update `.claude/memory/decisions.md` — append key design decisions from this session (what was decided and why, not implementation details), grouped by the phase they fed into
5. Show the user all generated phase prompts together (each with its Claude's Opinion, Design Panel feedback if available, and Deferred Changes) and ask for approval before implementation. If the user approves only a subset, mark the rest as `Pending` and leave the prompts in place for future invocations.
