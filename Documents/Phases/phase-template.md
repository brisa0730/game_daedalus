# Phase [N]: [Short Name]

## Overview

*Plain English. Write this LAST, from the finished doc, but it stays FIRST — it's the only section written for a reader who hasn't read the rest. No file paths, no line numbers, no version numbers, no `code`, no Glossary terms that need a lookup. Short sentences. ~150 words total, hard cap. If a sentence can't survive being read aloud to someone who doesn't know the codebase, rewrite it — don't append a clarifying clause.*

**Where things stand:** [1–3 sentences. What the operator or a player would actually see today if they opened the project. Name the problem, not the code that causes it.]

**What this phase changes:**
- [Change 1 — the outcome, not the mechanism.]
- [Change 2]
- [3–6 lines. If you need more than six, the phase is doing too much.]

**How you'll know it worked:** [1–3 sentences. What you'll see, do, or be able to do once it's done — the observable result, not a restatement of the Completion Criteria checklist.]

**Not in this phase:** [One line naming the nearest thing a reader would assume is included but isn't. Point at the section that covers it if there is one.]

---

## Source
- **GDD sections:** [list of `Documents/GDD/` files and sections driving this phase]
- **GDD version:** v[X]
- **Depends on:** Phase [list] — [what must already exist]

## File Manifest
[Consolidated list of every file created or modified in this phase, pulled from the plan]

| Action | File | Purpose |
|--------|------|---------|
| NEW | `path/to/file.py` | [one-line description] |
| MODIFY | `path/to/existing.py` | [what changes] |

---

## Steps

### Step 1: [Title]
- **[MODIFY/NEW] [FileName]**:
    - [Requirement 1]
    - [Requirement 2]
- **Tests:** [What tests to add or update]
- **Verify:** `[automated]` / `[manual]` / `[grep]` — [How to confirm it works. `automated`: pytest covers it. `manual`: log or output inspection. `grep`: import or symbol sanity check.]

### Step 2: [Title]
- ...

### Step N-1: Run Tests
- Run the project's test suite (see CLAUDE.md `Testing` section for command).
- If tests fail, fix the failing tests or the code causing the failure.
- Do not proceed to code review until all tests pass.

### Step N (Final): Code Review
- Run the code-review agent (`agents/workflow/code-review.md`) on all files changed during this phase.
- Fix any HIGH/MED issues.
- Report the review result to the user.

---

## Completion Criteria
[Pulled from the plan. Every item must pass before the phase is done.]

- [ ] [Criterion 1]
- [ ] [Criterion 2]

---

## Notes to Spec
[Deviations from the Spec, implementation decisions not covered by the spec, or open questions. Synced back to the Spec after phase completion. Remove this section if there are none.]

**If anything here contradicts the Overview, fix the Overview.** It is the section that gets read again months later; a stale plain-English summary is worse than none.

---

## Post-Phase Cleanup
After code review passes:
1. Move this prompt to `Documents/Phases/archive/`.
2. Update the phase status in CLAUDE.md.
3. Write `memory/logs/phases/phase[N]-log.md`.
4. Update `MEMORY.md` current state.
5. If applicable: update `CLAUDE.md`, `Documents/GDD/architecture.md`, `memory/lessons-learned.md`, `memory/decisions.md`, or GDD files with any convention/rule the phase established.
6. **Deferred items.** If this phase left known cleanup, dead-code, or non-blocking observability gaps that weren't in scope, file each as an entry in `Documents/Operations/Deferred.md` (Source = this phase). Conversely, if any existing Deferred entry's trigger fired during this phase, delete that entry in the same commit. See CLAUDE.md → Log Discipline.
7. Commit all phase work.

## Workflow
Execute steps sequentially. Proceed to the next step without waiting for user approval. Only stop if blocked. If blocked, stop and report: what step, what was attempted, what failed.
