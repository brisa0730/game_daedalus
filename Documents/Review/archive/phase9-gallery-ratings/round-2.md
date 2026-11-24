---
slug: phase9-gallery-ratings
round: 2
verdict: approved
reviewer: claude-cowork
updated: 2026-11-24T14:07:48+09:00
blocking: 0
recommendations: 0
---

# Round 2: Phase 9, gallery ratings (delta)

**What I could run:** the same shell as in round 1 (`cat`, `sed`, `grep` over `daedalus`). I compared the revised Overview and Steps 2 and 6–11, the Completion Criteria and the round-1 dispositions in `log.md` against `round-1.md`. Nothing was executed. `cargo test`, clippy, the look check and the operator check remain for the Author, as the document says.

## Round-1 findings: status

- **F1 (blocking): resolved.** In Step 7 the countdown now always starts from the rendered `data-wait`, so the stars appear even when the start-up open fails. A 409 re-sends the open POST and counts down from that POST's `wait`. If that open fails too, the page shows `msgFailed`, the stars stay, and the next click retries. I traced the failure cases:
  - Start-up open fails: the stars appear after `data-wait`. The click gets a 409, a re-open records the row, and a countdown from the true wait follows. Rating then succeeds.
  - Open row missing for any other reason: same recovery.
  - Cookie refused: a fresh token on every open, so it never converges. The document now states this case as accepted, and it fails closed.

  `/rate` still never records an open. Completion Criteria 2 and Step 14 name the recovery.
- **F2: resolved.** The hover rules are now scoped to `span#rate-buttons`, which holds only the buttons, so the label sits outside it. `#rate-buttons:hover .star` is (1,2,0) and `#rate-buttons .star:hover ~ .star` is (1,3,0), so the unlit rule beats both the lit rule and `.star.on` (0,2,0). I recomputed the specificities, and they're correct. The `[hidden]` guard was added. `rate-buttons` joined the Step 10 wire list.
- **F3: resolved.** Step 2 pins the exact boundary with explicit times: `opened_at = 1000`; `opened_by = 999` refused and `1000` accepted, against `opened_at <= opened_by`. Since `opened_by = now − gate`, that is now 1179 refused and now 1180 accepted for a 180 s gate. The HTTP test now uses 170 s → 409 and 180 s → 200, and neither can flake.
- **F4: resolved.** Each new look page names its game, and lee's countdown is on `old`, which lee doesn't own. The five non-owner pages and the two owner pages (`play` and `play-own`) are listed, each with its own assertions.
- **F5: resolved.** The "GET writes nothing" rule is now limited to `game_opens`, `ratings` and the rater cookie, in Completion Criteria 2, Step 9 test 10 and Step 14.
- **F6: resolved.** `aria-pressed="true"` is on star `mine` only, while `on` stays on stars 1…k. The Step 6 and test 10 assertions match.
- **F7: resolved.** Step 11's rollback now lists everything a rollback loses and says "roll back soon or not at all".
- **F8: resolved.** Overview bullet 2 now matches decision 3.

## New findings
None. The folds introduce no new defect. I checked that the Step 7 timer has to be restartable: the 409 path restarts it, and the start-up `ok` resets it. The document covers both.

## Late-pushback test
Would my answer to "what do you think?" contain anything not already in my round files? No. Everything I'd say is in F1–F8, and all eight are folded.

## Reviewer's view
I would build this as written. F1, the one blocking finding, is fixed: a failed open now recovers on the next click, and the only case that never converges (a browser refusing cookies) fails closed and is stated. F2–F8 are folded and verified. What remains rests on execution, not on the document: the cargo suites, the look-check screenshots (the 220 px budget at 1366 × 768) and the operator's live check of keyboard focus after a star click.
