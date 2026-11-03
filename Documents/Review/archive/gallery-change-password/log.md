# Handshake log: gallery-change-password

## Opened 2026-11-03T15:56:01+09:00 (Author: Claude Code)
**Doc:** Documents/Plans/gallery-change-password.md (a plan; critic-reviewed in draft: 3 HIGH, 7 MED, 9 LOW folded, M7 rejected with reason; not yet operator-approved; nothing built).
**Proposes:** a self-service change-password page on the live class gallery (`Deploy/gallery/`). Every signed-in user types the current password and the new one twice. The new one must be 8–128 characters and differ from the login and from the current password. Wrong current passwords go through the sign-in throttle, with the same known-browser skip. A change rotates this browser's session, ends every other session and known browser, and loses to a racing teacher reset. It also rolls in a triggered Deferred cleanup, and finishes with twelve router tests, a redeploy and a live two-browser Chrome check. Operator decisions are recorded in § Decisions.

## Round 1 dispositions (2026-11-03T16:03:54+09:00, Author)
Verified first: `require_access` answers 303 `/login` before the handler when the session is gone (`session.rs` `current_user` → None); `clear_reservation` sets `next_allowed = touched` when failures > 0; `hasher.runs` is a public counter (`accounts.rs:28`, used by `accounts.rs:150`); `tests/sign_in.rs:35-44` has the same second-boundary exposure.
- **F1 (blocking): accepted.** Reproduced by reading: router test 9 stops at `require_access`, so the conditional UPDATE never runs. Added three db-level tests against `state.db`: the reset race (`Ok(false)`, the reset's hash kept, no session inserted, known browsers untouched), rotation, and no known cookie. Router test 9 is now described as the middleware path. Completion Criteria 1 and 3 cite them.
- **F2: accepted.** `token_hash != NULL` matches nothing. Step 2.4 now spells out two statements, covered by the db test "No known cookie".
- **F3: accepted.** `post_password` calls `clear_reservation` only if it reserved. `post_login`'s same quirk is filed for Deferred.md in Post-Plan Cleanup, to keep the plan surgical, and noted in Open Risks.
- **F4: accepted (option: carry the old start).** The new session row copies the old one's `created_at` in the same transaction (`INSERT … SELECT`), so the GDD's 12-hour line stays true. Recorded in Decision 4; the rotation test checks it.
- **F5: accepted as residual.** Recorded in Open Risks (about one Argon2 run; the teacher reset shares it). `post_login` is unchanged.
- **F6: accepted.** Test 4 asserts `state.hasher.runs` is unchanged across the five rule POSTs.
- **F7: accepted.** Cases asserting an immediate 429 use `TestApp::with(|c| c.throttle_first_secs = 60)`.
Re-verified my fold: re-read Decision 4, Step 2 (the INSERT…SELECT runs after the UPDATE and before the DELETE, so the old row still exists to copy from), Step 3.3, Step 4 tests 4, 5 and 9, Completion Criteria and Open Risks. Nothing built.

## Round 2: approved; closing dispositions (2026-11-03T16:08:40+09:00, Author)
- **F8: accepted.** Verified: tests 3, 4 and 10 assert that *no* delay was set, and at the 1 s first delay a retry crossing a second boundary can hide a delay a broken handler set. Fold: every case in `tests/password.rs` builds with `throttle_first_secs = 60` (the Step 4 preamble now says so); `clear_throttle` still precedes sign-ins after deliberate failures.
Re-verified the close fold: re-read the Step 4 preamble; it covers tests 2, 3, 4, 5 and 10, and contradicts no case, since every case that signs in after a failure already calls `clear_throttle`. No finding is left unfolded. The plan is approved as a proposal; **nothing was built**. Operator approval of the plan is still needed before execution and the live redeploy.
