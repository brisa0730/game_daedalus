# Handshake protocol

A document is reviewed to approval by two agents passing files back and forth,
with no human relaying between them. Both sides invoke `/handshake`.

**This file is the contract, and it is the only copy.** Both skills read it
rather than restating it, so the stop conditions and the file layout have
exactly one definition. Change the rules here, in this file.

## The two roles

| Role | Who | Invokes |
| --- | --- | --- |
| **Author** | Claude Code, on the local machine | `/handshake <doc-path>` |
| **Reviewer** | Claude (Cowork), in the cloud | `/handshake` (watch) or `/handshake <slug>` |

The role is never ambiguous — it follows from which agent is running. No mode
flag, no negotiation.

**The Reviewer's shell is conditional.** When the Cowork session is linked to the
local machine with the repo mounted, it can `grep`, `find`, read files and run
short scripts over the connected folders. It may also have none. Neither side
assumes: the Reviewer states what it could run in each round file.

It never runs the app, the test suites (Unity batch mode), a
build, or anything else outside the connected folders, and it cannot give the
Author a turn. Anything on that list is raised as a finding for the Author to run
and report.

A measured finding still gets re-verified by the Author.

Both sides poll on a **3-minute cadence**. Neither needs the other awake — the
files are the rendezvous.

## Where the files go

Review root: **`Documents/Review/`** (named in `CLAUDE.md` § Project Config).
Everything below is relative to it.

```
Documents/Review/
  <slug>/                 live handshake, slug from the doc filename
    status.yml            small, rewritten each round — the only polled file
    log.md                APPEND-ONLY: every round's dispositions
    round-1.md            the Reviewer's reviews
    round-2.md
  archive/
    <slug>/               the whole folder moves here on close
```

The document under review **stays where it lives**. The handshake folder points
at it; it is never moved or copied in.

The slug is the doc's **full filename stem** — `phase3-world-systems.md` →
`phase3-world-systems`, `tile-collision.md` → `tile-collision`. Unique by
construction, because filenames within a directory already are, and
self-describing on sight.

⚠️ **Never shorten the slug** (e.g. to the part before the first `-`). A short
slug is ambiguous (`phase3`) and collides for plans, whose descriptive filenames
often share a first word. A collision silently reuses a live handshake folder and
interleaves two documents' rounds and dispositions in one append-only `log.md`.

Both sides derive the slug from this line, so **a handshake that has already
exchanged a round keeps the folder name it opened under** — renaming mid-flight
moves the rendezvous out from under the other side. Before the first round file
lands, nothing has been exchanged and a rename is free.

## File ownership — never write a file you do not own

| File | Owner | Everyone else |
| --- | --- | --- |
| `<slug>/status.yml` | Author | read-only |
| `<slug>/log.md` | Author | read-only |
| `<slug>/round-<N>.md` | Reviewer | read-only |
| the document under review | Author | read-only |

Two consequences, worth understanding rather than just obeying:

- Round files are **never overwritten** — each round is a new filename — so the
  two writers can never collide on one and no lock is needed.
- The document has exactly **one** writer. The Reviewer never edits it, not even
  to fix a typo. A concurrent write would either clobber the Author's fold-in or
  be refused as stale, and a refused write breaks the Reviewer's loop rather
  than corrupting the file.

**`log.md` is append-only.** This is not a style preference. Dispositions kept in a
rewritten file are destroyed by the next write. The dispositions are the most
valuable output of the loop and the only part with no other home. Append; never
rewrite.

## `status.yml`

Kept small on purpose: the Reviewer reads and parses it every three minutes.

```yaml
doc: Documents/Phases/phase3-world-systems.md
slug: phase3-world-systems
round: 3
status: ready          # ready | working | failed | done
updated: 2026-10-20T17:20:04+09:00
```

`status: failed` is **mandatory** when the Author cannot proceed, with the reason
appended to `log.md`. Silence is indistinguishable from still-working: without
it the Reviewer waits out its full timeout and learns nothing.

## `round-<N>.md`

```yaml
---
slug: phase3-world-systems
round: 3
verdict: changes-requested   # changes-requested | approved
reviewer: claude-cowork
updated: 2026-10-20T17:25:11+09:00
blocking: 2
recommendations: 1
---
```

Then the findings, each with a stable id (`F1`, `F2`, …), type, category,
severity, location, and the concrete defect. **Ids are stable across rounds** — a
finding re-raised in round 4 keeps the id it had in round 3. This is what makes
convergence measurable rather than a matter of opinion.

Every finding has one **type**:

- **blocking** — approval waits for it.
- **non-blocking** — the Author folds it before close.
- **recommendation** — the Reviewer's answer to a decision the document leaves
  to the user: an open question, a deferred choice, an option set. It never
  blocks, and it never counts toward the no-progress comparison (§ Rules that
  keep the loop from spinning).

Every finding also has one **category**: `accuracy` (the document is wrong
about something) or `design` (a choice in it has a consequence the Reviewer
states).

The Reviewer states what it could run, what it verified, and what it could not.
A claim accepted on trust is named as such.

A round file ends with a **`## Reviewer's view`** section: 2–5 lines on whether
the Reviewer would build the document as written. It only summarises findings
already filed above, citing their ids. It never raises anything new.

## The cycle

1. **Author** writes or updates the doc, creates `<root>/<slug>/`, and writes
   `status.yml` with `round: 1`, `status: ready`.
2. **Reviewer** polls. A new `round:` with `status: ready` is its turn: it reads
   the doc, verifies what it can, writes `round-<N>.md`.
3. **Author** polls for `round-<N>.md`. When it lands:
   - **re-verifies the findings before accepting them** — recompute every number,
     re-check every cited line, run the greps and tests it asks for. A finding
     that does not reproduce is a candidate for rejection with a reason, not an
     instruction. A `design` finding is re-verified the same way: reproduce its
     stated consequence (trace the code path, run the measurement, check the
     premise) before accepting or rejecting it. "This is a design choice" is not
     a rejection reason; a reason names why the consequence does not occur or is
     acceptable;
   - folds in what it accepts, and records each recommendation (§ Rules that
     keep the loop from spinning);
   - appends the review verbatim to the doc under `## Review round N`;
   - **appends** a disposition for **every** finding to `log.md`;
   - writes `status.yml` with `round: N+1`, `status: ready`.
4. Repeat until a round file carries `verdict: approved` or a stop condition
   fires.
5. **On approval:** the Author completes the close fold (§ Approval), appends
   the final review, appends closing dispositions, sets `status: done`, and moves
   `<slug>/` to `<root>/archive/<slug>/`.

## Rules that keep the loop from spinning

- **Every finding gets a disposition.** Accepted, or rejected with a reason —
  or, for a recommendation, recorded. A finding silently dropped will be
  re-raised forever.
- **A recommendation is recorded, never accepted or rejected.** The decision is
  the user's. The Author writes it into the document beside the question it
  answers — e.g. "Reviewer recommends: <option> — <reason> (round N, Fk)" — and
  appends a `recorded` disposition to `log.md`. If the Author disagrees it may
  add its own counter-view beside it; it never drops the recommendation.
- **A reasoned rejection is final.** The Reviewer does not re-litigate it without
  new evidence. Corrections flow both ways: the Reviewer records when the Author
  has shown a finding to be wrong.
- **No round cap.** The loop runs until a round file carries `verdict: approved`.
  Three rounds is the expected shape; a document that needs more gets more. The
  Reviewer never stops merely because a round count was reached, and never
  approves to end the loop rather than because the document is ready.
- **No-progress stop.** Compare rounds by **blocking finding id set** — that is
  what the stable ids are for, and it is the one comparison the two sides cannot
  disagree about. If round N's blocking ids are the same set as round N−1's, the
  loop is not converging: stop and say so rather than burning another pass. The
  same ids with rewritten text still count as no progress — a finding the Author
  answered and the Reviewer re-raised under its own id is the deadlock, not a new
  finding. Non-blocking findings and recommendations are excluded from the
  comparison: only the blocking id set counts. With no round cap this is the
  only thing that ends a loop in which both sides are still responding, so it is
  not optional; the Reviewer names the deadlocked ids in its report.
- **Timeout: 1 hour per side.** Record why and stop; never poll on.
- **Rounds 2+ are delta reviews** against the previous round, not fresh full
  passes. The full-context read happens once. This matters most on large docs —
  a long design doc plus its supporting context is a heavy read to repeat.

## Approval

The Reviewer may write `verdict: approved` and end the loop on its own
authority. Before approving, it runs the **late-pushback test**: would its
answer to "what do you think?" contain anything not already in its round files?
If so, it files that first — it may still approve in the same round.

Non-blocking findings and recommendations may be open at approval. On close, the
Author folds every non-blocking finding and records every recommendation before
setting `status: done`. The human reviews the finished document, `log.md`
and the round files as a record — not each round as it happens.

## Reporting to the human

The human did not watch the rounds. Whichever side reports at the end — in
practice the Reviewer, when it approves or stops — writes a report of five items
in this order. Items 1–4 are plain English, a few sentences each at most, no
jargon, and they come before the review record:

1. **What the document is.** The problem or question it addresses and what it
   proposes, in terms someone outside the project would understand — not "a
   phase adding a sixth field to the `SkillDelivery` tuple" but what that field
   is *for* and why anyone asked for it.

2. **What it changes.** The substance: what the system, process or decision
   does differently if this lands, named as concrete behaviours — not a
   restatement of the title. Which things are broken today and stop being
   broken; which are guards against a failure that has not happened yet; what
   is merely bookkeeping. A reader who will never open the document should come
   away able to say what is different afterwards.

   This is the part the reader actually wants, and the part most easily lost,
   because item 3's temporal framing pulls a report toward custody. Collect it
   while reading the document in the round — that is the cheapest moment, and
   reconstructing it afterwards from a doc you have stopped reading is how a
   report ends up describing the process instead of the substance.

3. **What happens next.** The custody: who or what carries it out, and when.
   Approval of a document is not the same as the change existing, so say
   plainly that the change does not exist yet. If the loop stopped unapproved,
   say so instead and name why (no-progress, timeout) in plain terms. If the
   approval rests on something that could not be verified — an unrun test suite,
   a play-check nobody has performed — name it here as a condition rather than
   burying it in the record. List the document's open questions here as the
   user's decisions.

4. **The Reviewer's view.** Whether the Reviewer would build the document as
   written, with the finding ids behind that view.

5. **The review record.** The round files and `log.md`.

Items 2 and 3 are separate on purpose. "What happens now" reads as a question
about the next event, and the honest answer to that is always procedural, so a
single combined item reliably loses the substance. Write all five even when the
person appears to have followed along: this is what a second-hand reader (a
forwarded message, a status report) sees first.

## Scope

The handshake reviews a **document**. It does not implement it, write code, or
run tests. An approved doc is an approved proposal; shipping it is separate work.
