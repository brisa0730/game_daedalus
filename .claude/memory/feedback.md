---
name: User feedback and workflow preferences
description: Consolidated feedback on how Claude should approach work — autonomy, opinions, git policy, and workflow style
type: feedback
---

## No step-by-step approval
Do not wait for user approval between phase steps. Execute all steps sequentially and report at the end.

**Why:** Step-by-step approval slows down implementation unnecessarily.
**How to apply:** When executing a phase prompt, run through all steps without pausing. Only stop if blocked by an error or ambiguity.

## Never use AskUserQuestion
Do not use the AskUserQuestion tool. Ask the user via plain text or make the call yourself and report what you did.

**Why:** The interactive picker interrupts flow when a plain question or a confident decision would suffice.
**How to apply:** When you need user input, write a concise question in your message, or, when you have enough context, just make the call and explain it. Reserve questions for genuine ambiguity.

## Share architectural opinions
When reviewing GDD/design changes (e.g., during `/new-phase` or `/design`), include a personal analysis section with opinions, not just factual reporting.

**Why:** Catch potential issues, trade-offs, or design concerns early, before committing to implementation.
**How to apply:** Built into `.claude/agents/workflow/phase-planner.md`: the planner outputs a `## Claude's Opinion` section before every phase prompt.

## Memory files go in project .claude/memory/
All memory files belong in the project-level `.claude/memory/` directory, never in the global `~/.claude/` path.

**Why:** Keeps memory co-located with the project for sync across computers.
**How to apply:** Always write memory files to `.claude/memory/`.

## Archive completed phase prompts before memory updates
After code review passes, move the phase prompt to `Documents/Phases/archive/` before writing phase logs or updating MEMORY.md.

**Why:** Keeps `Documents/Phases/` to active (Pending/in-progress) prompts only, so a directory listing shows real open work.
**How to apply:** In the post-phase workflow, the first action is to move the prompt to `Documents/Phases/archive/`, then write the phase log and memory updates.

## Ask before every commit
Never create a git commit without explicit user approval; ask each time. Do the work autonomously (no pausing between execution steps, per "No step-by-step approval"), then present what's staged and ask before committing. Never push.

**Why:** The operator wants to review changes before they enter history.
**How to apply:**
- **Always allowed (no confirmation):** read-only git (`status`, `log`, `diff`, `blame`, `fetch`), and `git add` to stage Claude's own changes for review.
- **Requires explicit approval each time:** `git commit`. Present the diff summary, wait for the go-ahead, then commit.
- **Never without explicit instruction:** `git push`, `git checkout`/`git switch`, `git branch`, `--force`, `reset --hard`, `rebase`, any destructive operation on `main`.
- **Trunk-based:** when approved, commit directly on `main`; no feature branches unless the user asks.
- **Commit title convention**: the prefix keys on whether the commit bumps the GDD/Changelog version:
  - **Version-bumping commit** (Changelog entry + `GDD.md` bump): `vX.YY: <subject> — <description>`. `<subject>` is `Phase NN`, `ISS-NNN`, or a short topic, never the prefix itself.
  - **Non-version commit** (tracking-file churn, docs): a plain area prefix + colon: `docs:`, `roadmap:`, `memory:`, `Issues.md:`, etc.

## GDD version + Changelog entry are reserved for project changes, not tracking-file churn
Bumping `Documents/GDD/GDD.md` version and writing a `Changelog.md` entry require an actual project change: a code edit, a test change, a gameplay behavior shift, or a GDD-content edit. Pure tracking-file churn (filing/closing an issue, moving a Deferred row) does NOT qualify.

**Why:** GDD/Changelog versions are the project's shippable-state ledger; inflating them with non-release activity buries what actually shipped.
**How to apply:** Before bumping `GDD.md`, ask: "is there a source change OR a GDD-content change in this commit?" If no, drop the bump; the tracking-file work commits on its own with a descriptive message.

## Separate memory logs are earned, not automatic
Not every issue fix needs its own `memory/logs/issues/issNNN-*.md` file. Small patches get full context in the Changelog entry.

**Why:** A log file per issue is redundant churn when the Changelog entry already captures the same information.
**How to apply:** Default to no separate log. Create one only when the Changelog entry would balloon past ~10 bullets, there are multi-phase sequencing notes worth preserving, or the fix involves a migration/schema change needing a forensic record.

## Systems first, values and balance later
While building a feature, get the mechanism working end to end before stopping on tuning numbers or balance gates. A balance-shaped red during a systems phase is recorded in the phase log with its readout, not a reason to halt and ask for number changes, and not a Deferred entry unless asked.

**Why:** The operator judges fun and tunes after the system exists; a halt to renegotiate a number mid-build spends their attention on the wrong question.
**How to apply:** Stop for the operator only on a broken mechanism, an unmet design decision, or a safety/correctness failure. For a balance finding: measure it, keep the readout, note it in the phase log, keep building. (Movement feel is the exception: it gets its operator play-check gate in `movement-prototype`.)

## Don't invent content the operator didn't name
Build what the operator named or the GDD specifies. Don't invent content or mechanics beyond it; when a placeholder is needed, pick the plainest one and label it a placeholder. Discuss a design topic with the operator directly before invoking the `/design` panel.

**Why:** Content nobody chose gets built, measured and tuned, and then thrown away.
**How to apply:** For each content item in a phase prompt, check whether the operator named it or the GDD specifies it. If not, use the simplest placeholder or ask, in plain text. Talk the topic through first; the panel comes after.

## "Later" means postponed, not rejected
When the operator defers or removes something ("too early", "for now", "later"), treat it as postponed. Don't carry it forward as a constraint against the idea.

**Why:** Misreading a postponement as a ban narrows the design space the operator wants open when the topic comes back.
**How to apply:** When reviving a topic, list deferred ideas as candidates again. A v1 scope restriction binds v1 only, not the final design.

## Design discussions: one commit at the end
During a design discussion, write decisions into the docs as they settle but don't commit until the operator says the discussion is over. Then make one commit.

**Why:** A commit per decision clutters the history of what is really one design pass.
**How to apply:** Keep GDD and ledger edits uncommitted through the conversation. Ask, or wait for the operator, before committing.

## A phase prompt and its handshake land as one commit
Don't commit a draft phase prompt (or its review folds) before its handshake closes. Commit the prompt, its Roadmap/decisions updates and the archived review together, e.g. `docs: Phase N <name> prompt — panel-reviewed, handshake-approved`.

**Why:** One reviewable entry per approved prompt.
**How to apply:** Keep the prompt uncommitted through the handshake; commit once, after approval.
