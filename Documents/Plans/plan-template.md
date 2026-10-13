# Plan: [Short Name]

**Filename:** descriptive (`onboarding-gap.md`), not issue-indexed (`iss031-onboarding-gap.md`). ISS-NNN lives in **Trigger** / **Related issues** below, not the filename — issue rows get deleted on resolution, plans outlive them.

**Date:** YYYY-MM-DD | **Status:** [draft / approved / executing / complete]

## Overview

*Plain English. Write this LAST, from the finished plan, but it stays FIRST — it's the only section written for a reader who hasn't read the rest. No file paths, no line numbers, no version numbers, no `code`, no Glossary terms that need a lookup. Short sentences. ~150 words total, hard cap. If a sentence can't survive being read aloud to someone who doesn't know the codebase, rewrite it — don't append a clarifying clause.*

**Where things stand:** [1–3 sentences. What's true today, and what's wrong with it. Name the problem, not the code that causes it.]

**What this plan changes:**
- [Change 1 — the outcome, not the mechanism.]
- [Change 2]
- [3–6 lines. If you need more than six, this is probably a phase, not a plan — re-run `/scope`.]

**How you'll know it worked:** [1–3 sentences. What you'll see, do, or be able to do once it's done — the observable result, not a restatement of the Completion Criteria checklist.]

**Not in this plan:** [One line naming the nearest thing a reader would assume is included but isn't.]

**Why a plan and not a patch:** [One line. If this is hard to answer, it's a patch.]

**Design choice (optional):** [If the plan picks between real alternatives, record which and why. Prevents the later replay "why didn't you just X?"]

---

## Source
- **Trigger:** [what surfaced the need — issue ID, user request, observation, audit finding]
- **Related issues:** [ISS-NNN references, or "none"]
- **Scope:** [one-line — what's being changed at a high level]
- **Depends on:** [prior plans/phases/state that must already exist]

## File Manifest
[Consolidated list of every file created, modified, or deleted by this plan. **Code + active tooling only** (src/, tests, hooks, skills, agents, CLAUDE.md). Operations/Changelog/Issues/Roadmap and `.claude/memory/*` updates go in Post-Plan Cleanup, not here. For doc-only plans, this table may be replaced with a bullet list of touched files.]

| Action | File | Purpose |
|--------|------|---------|
| NEW | `path/to/new.md` | [why it's being added] |
| MODIFY | `path/to/existing.md` | [what changes and why] |
| DELETE | `path/to/gone.md` | [why it's being removed] |
| RENAME | `old/path.md → new/path.md` | [why renamed] |

---

## Steps

*Each step is a drafting unit, not a commit. The plan lands as ONE commit at the end (or a small number of logically-grouped commits if the diff is too large to review as one) — don't add per-step `**Commit:**` trailers or structure as "Commit 1 / Commit 2 / …". If a step's "why" is non-obvious, inline the rationale on the bullet that needs it — don't narrate every line.*

### Step 1: [Title]
- **[NEW/MODIFY/DELETE/RENAME] [FileName]**:
    - [Requirement 1]
    - [Requirement 2]
- **Verify:** `[grep / ls / git status / pytest]` — [one-line check]

### Step 2: [Title]
- ...

### Step N-2: Run Tests
*Only when the plan touches code. Remove this step for doc-only plans.*
- Run the project's test suite (see CLAUDE.md `Testing` section for command).
- Fix any failures before proceeding.

### Step N-1: Verify
- Walk the Completion Criteria checklist below.
- Fix any failures before proceeding to code review.

### Step N (Final): Code Review
- **If the plan modified code or active tooling** (CLAUDE.md, skills, agents, hooks): run the code-review agent (`agents/workflow/code-review.md`) on all changed files.
- **If the plan is doc-only**: run a critic sub-agent (prompt: "read the plan + the files it touched; report HIGH/MED/LOW issues") unless already run at draft time.
- Fix any HIGH/MED issues. Fold fixes back into the plan where the plan was wrong.
- Report the review result to the user.

---

## Completion Criteria
[Pulled from the plan. Every item must pass before the plan is done. Prefer concrete runnable checks over prose.]

- [ ] [`ls path/` returns X] / [`grep -c Y file` returns N] / [`pytest` passes]
- [ ] [Criterion 2]

---

## Notes to Spec / Open Risks
[Deviations discovered during execution, convention changes that affect `CLAUDE.md` or Spec files, open questions, or known risks accepted at drafting time. Synced back after completion. Remove this section if none.]

**If anything here contradicts the Overview, fix the Overview.** It is the section that gets read again months later; a stale plain-English summary is worse than none.

---

## Post-Plan Cleanup
After code review passes:
1. Append execution trailers to this plan file:
   - `**Approval:** User confirmed YYYY-MM-DD.`
   - `**Execution:** COMPLETE YYYY-MM-DD.`
   - `**Verification:** <one-line summary of what passed>`
   - `**Version note:** <optional — deviations between plan and execution, e.g. "shipped as GDD v1.48, not v1.47 — version taken by another PR">`
2. Move this plan to `Documents/Plans/archive/`.
3. If applicable: update `CLAUDE.md`, `Documents/GDD/architecture.md`, `memory/lessons-learned.md`, `memory/decisions.md`, or GDD files with any convention/rule the plan established.
4. **Deferred items.** If this plan left known cleanup, dead-code, or non-blocking observability gaps that weren't in scope, file each as an entry in `Documents/Operations/Deferred.md` (Source = this plan). Conversely, if any existing Deferred entry's trigger fired during this plan, delete that entry in the same commit. See CLAUDE.md → Log Discipline.
5. Commit all plan work.

## Workflow
Execute steps sequentially. Proceed to the next step without waiting for user approval. Only stop if blocked. If blocked, stop and report: what step, what was attempted, what failed.
