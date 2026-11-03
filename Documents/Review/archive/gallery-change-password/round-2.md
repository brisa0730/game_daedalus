---
slug: gallery-change-password
round: 2
verdict: approved
reviewer: claude-cowork
updated: 2026-11-03T16:09:00+09:00
blocking: 0
recommendations: 0
---

# Review round 2: gallery-change-password (delta vs round 1)

## What I could run

I used the same shell access as in round 1 (`device_bash` over the Mac mini repo). This round is a delta review: I checked the round-1 dispositions in `log.md` against the revised plan text (Decision 4; Step 2 at lines 68–80; Step 3.3; Step 4, tests 4, 5, 9 and 10; Completion Criteria; Open Risks; Post-Plan Cleanup 4). I did not build or run anything.

## Round-1 findings

- **F1: closed.** Step 2 now has three db-level tests, and the reset-race test calls `set_password`, then `change_own_password` with the old hash. This path can only be stopped by the `EXISTS` guard, so deleting the guard makes the test fail. Router test 9 is relabelled as the middleware path. Criteria 1 and 3 cite the db tests.
- **F2: closed.** Step 2.4 now uses two explicit statements, and the "No known cookie" db test covers the `None` branch.
- **F3: closed.** `clear_reservation` now runs only when this request reserved. The `post_login` quirk is filed for Deferred.md with a trigger and fix shape, and it is in Open Risks.
- **F4: closed.** Step 2.2 uses `INSERT … SELECT …, created_at, ? FROM sessions WHERE token_hash = ?`. **Seam check:** this runs after step 1 has confirmed the old row exists, and before step 3 deletes it, all inside one transaction. SQLite takes the write lock at step 1's UPDATE, so no other writer can remove the row between steps 1 and 2. The rotation test asserts that the old `created_at` is kept.
- **F5: closed** as an accepted residual in Open Risks. I did not verify the "~66 ms" figure; it is the Author's estimate.
- **F6: closed.** Test 4 now asserts `hasher.runs` is unchanged.
- **F7: closed.** Cases that assert an immediate 429 use `throttle_first_secs = 60`.

## New finding

### F8: "No 429" assertions can still pass falsely at the 1 s first delay
- **Type:** non-blocking · **Category:** design · **Severity:** LOW
- **Location:** Step 4, tests 3, 4 and 10 (the F7 fold covers only cases that *expect* a 429)
- **Defect:** this is the mirror of F7. Tests 3 ("both answer 401, not 429"), 4 ("no 429 on a following try") and 10 ("an immediate retry isn't 429") prove that *no* delay was set. At a 1 s first delay, a broken handler's delay can expire if the retry crosses a second boundary, so the test passes when it should fail. It only happens intermittently, so it is a weak guard rather than a false failure. Test 4 is backed by the new `hasher.runs` assertion; tests 3 and 10 have no backstop.
- **Fix shape:** build tests 3, 4 and 10 with `throttle_first_secs = 60` as well. The simplest version is the same `TestApp::with` for every case in `tests/password.rs`, plus `clear_throttle` where a case signs in after a deliberate failure, which the plan already does.

## Late-pushback check
Is there anything I would say about this plan that isn't in my round files? No. I have nothing further.

## Reviewer's view
I would build this plan as written. F1's race guard now has a test that fails without it, and the F2–F7 folds introduced no new defects. The `INSERT … SELECT` ordering for F4 is sound. F8 is a small test-strength fold for close. **Approved.**
