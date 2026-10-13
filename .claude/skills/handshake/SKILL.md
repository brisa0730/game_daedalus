---
name: handshake
description: Hand a document to Claude (Cowork) for review and iterate to approval without a human relaying between the two. Use after writing an issue, plan, phase, spec or report that should be vetted before it ships.
disable-model-invocation: false
model: opus
effort: high
argument-hint: "[doc path, e.g. Documents/Phases/phase3-foo.md]"
---

# Handshake (Author): $ARGUMENTS

**Read `protocol.md` beside this file first and follow it.** It is the contract —
review root, file layout, formats, ownership, stop conditions — and it is the
only copy, shared with the Reviewer. This skill covers only how *you*, the
Author, drive your half. Where the two disagree, `protocol.md` wins.

You are the **Author**. The Reviewer is Claude (Cowork). Its shell is conditional
— it states what it could run at the top of each round file. The app,
the test suites and a build it never runs; those arrive as findings for you to
run and report.

## Workflow

### Step 1 — Open the handshake

The document must already exist. If `$ARGUMENTS` is empty, use the document from
the current conversation; if that is ambiguous, ask rather than guess.

1. Resolve the review root and derive the slug per `protocol.md`.
2. Create `<root>/<slug>/`.
3. Write `status.yml`: `round: 1`, `status: ready`, the doc path, the timestamp.
4. Append an opening entry to `log.md` naming the doc and what it proposes.

Do not write `round-*.md` at any point. Those are the Reviewer's.

### Step 2 — Wait for the review

Poll for `<root>/<slug>/round-<N>.md`. Prefer **one backgrounded waiter** — it
returns a single notification when the file lands and blocks nothing meanwhile:

```bash
for i in $(seq 1 120); do
  [ -f <root>/<slug>/round-<N>.md ] && { echo FOUND; exit 0; }
  sleep 30
done
echo TIMEOUT; exit 1
```

Run it with **`run_in_background: true`**. 120 × 30s is the protocol's 1-hour
timeout, and the last line says which outcome fired: `FOUND` → Step 3;
`TIMEOUT` → set `status: failed`, append the reason to `log.md`, stop.

**Fallback, where background Bash is unavailable** — poll in the foreground, one
call per 3 minutes, passing an explicit **`timeout: 210000`**:

```bash
for i in $(seq 1 12); do
  [ -f <root>/<slug>/round-<N>.md ] && { echo FOUND; break; }
  sleep 15
done
```

The loop sleeps 180s and the tool's default ceiling is 120s, so at the default
the call does not fail — it is **moved to the background** two minutes in and
returns a task id instead of the loop's output. That return is neither `FOUND`
nor silence, so calling again on it spawns a second detached poller racing the
first, each notifying out of band. The explicit timeout keeps one poll in the
foreground, where its result is readable. **If it prints nothing, call it
again** — silence means keep waiting, not stop. Up to 20 calls (1 hour), then
`status: failed` as above.

### Step 3 — Verify before you accept

This is the step that carries the loop. Do not treat the review as a task list.

For every finding: recompute the arithmetic, re-read the cited lines, run the
greps and tests it asks for. A finding that does not reproduce is a candidate
for **rejection with a reason** — the Reviewer is wrong sometimes, and under the
protocol a reasoned rejection is final. Say so plainly and move on.

A finding the Reviewer measured itself still gets re-derived — re-grep the
artifact rather than working from the review's list. (CLAUDE.md Behavioral Rule 9,
*Verify Before Done*.)

`design` findings get the same treatment; `protocol.md` § The cycle, step 3 sets
what counts as a reason to reject one.

### Step 4 — Fold in and hand back

1. Apply the findings you accept. Where a finding offers a choice of remedies,
   pick one and record why. Record each recommendation as `protocol.md`
   § Rules that keep the loop from spinning describes.
2. **Re-verify your own fold before handing back.** Step 3 verified the
   Reviewer's incoming findings; nothing has yet checked the text *you just
   wrote*. Re-derive every number and re-read every line your edit introduced or
   re-cited, to the same standard you applied to the review. A fold is new text
   with the same defect rate as any other, written fast and under the authority
   of a correction — so it reads as bookkeeping and skips scrutiny.

   This is the loop's characteristic failure: a fold can open a hole the
   original text did not have, and only a Reviewer that re-executes rather than
   approving on the dispositions will catch it. Expect the residue to
   concentrate in whatever you rewrote most.
   (`memory/lessons-learned.md` § Planning & documents.)
3. Append the review **verbatim** to the end of the document under
   `## Review round N`.
4. **Append** to `log.md` — never rewrite it — a disposition for **every**
   finding: accepted (and what changed), rejected (and why) or, for a
   recommendation, `recorded` — including what you verified. A finding with no
   disposition will be re-raised forever.
5. Rewrite `status.yml` with `round: N+1`, `status: ready`.
6. Return to Step 2 for the next round file.

### Step 5 — Close

When a round file carries `verdict: approved`:

1. Complete the close fold `protocol.md` § Approval requires — and re-verify
   it per Step 4.2.
   **This fold is the riskiest one in the loop**, because it is the only one no
   round ever reviews: whatever you write here ships inside an approved document.
2. Append the final review to the document.
3. Append closing dispositions to `log.md`.
4. Set `status: done`.
5. Move `<root>/<slug>/` to `<root>/archive/<slug>/`. **Check first whether that
   path already exists** — the same document handshaken twice — because `mv`
   nests the new folder *inside* the old one instead of failing, burying the
   second run's record. If it exists, archive as `<slug>-<YYYYMMDD>`.

If the Reviewer stops without approving (no-progress, timeout), record that
outcome in `log.md` the same way and leave the folder live. An unapproved
document is a result, not a failure to hide.

## Scope

The handshake reviews the **document**. It does not implement it. An approved
doc is an approved proposal — shipping it is separate work, and `log.md` should
say plainly that nothing was built.
