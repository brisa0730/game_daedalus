---
slug: gallery-change-password
round: 1
verdict: changes-requested
reviewer: claude-cowork
updated: 2026-11-03T16:02:00+09:00
blocking: 1
recommendations: 0
---

# Review round 1: gallery-change-password

## What I could run

I had shell access to the Mac mini repo folder (`device_bash`: `grep`, `sed`, `cat` over `Deploy/gallery/`, `Documents/`, `.claude/`). I did not run `cargo build`, `cargo test`, clippy or the live server. Everything below is checked against source text only.

**Verified against the code:**
- `session.rs`: `post_login`'s order (known-browser check, `reserve`, `verify`, `fail` / `clear_reservation` / `clear`), the cookie attributes, `SessionHash`, `CurrentUser`, `is_token`, and `require_access` (it redirects before the handler when the session is missing).
- `Throttle`: `reserve` / `fail` / `clear` / `clear_reservation` semantics, and `delay_after` = 1, 2, 4 … 30.
- `db.rs`: `set_password` (one transaction, deletes every session and known browser), `insert_session` columns, `GAME_COLUMNS`, `Game.bytes` / `updated_at` (read nowhere outside `from_row`), and `NewGame.bytes` (written).
- `migrations/0001_init.sql`: `known_browsers` PK is `(token_hash, user_id)` (a shared laptop's token is known for several logins), and `sessions` PK is `token_hash`.
- `files.rs`: `safe_join` is used only by its own test. `response_headers` is reached only from `play.rs:161`, after `play.rs:143` restricts the name to `build_files` (`.loader.js`, `.data.br`, `.framework.js.br`, `.wasm.br`). `share_zip.rs:192–194` rejects `.gz`, so Step 2b's removals are safe. The whole `use std::path::{…}` import goes with `safe_join` and its test.
- `app.rs`: the route table, `Access::Page`, and the `gallery_layer`. `tests/routing.rs`: the POST cross-origin sweep, the no-session sweep and the signed-in-page sweep, all of which iterate `gallery_routes()` and will pick up `/password`.
- `pages.rs` `div.who`, `gallery.css` (`--accent`, `.error` at line 33), `strings.rs` (`LOGIN_BUSY`, `wait_seconds`), `Hasher.permits` is `pub` (test 10 is feasible), `teacher.rs:275–278` (reset → `set_password` + `throttle.clear`), and `cli.rs` `teacher_reset`.
- The Deferred.md entry "Leftover spike code in the gallery crate" exists, and its trigger ("the next change to `files.rs` or `db.rs`") fires here.
- The critic's M7 rejection holds: `.claude/memory/framework.md:88` is `## Gallery server`.
- The counts are right: there are twelve test cases and six error lines. The live-check wait of at least 2 s covers the 1 s first delay.

**Not verified:** that the plan compiles, that the tests pass, and anything about the live server. These are Steps 5–6, for the Author.

## Findings

### F1: Test 9 never reaches the conditional UPDATE it is meant to prove
- **Type:** blocking · **Category:** accuracy · **Severity:** MED
- **Location:** Step 4, test 9 (and the critic-M3 fix in Step 2.1)
- **Defect:** the test deletes the student's sessions *between loading the page and posting*. That means the POST arrives with a session that no longer exists, so `require_access` (`session.rs`, `current_user` → `None` → `see_other("/login")`) answers 303 `/login` before `post_password` runs. The `UPDATE … WHERE EXISTS (SELECT 1 FROM sessions …)` guard is never executed. The test's assertions ("303 `/login`", "the password is the reset's") both hold with the guard deleted, so the test cannot fail when the M3 fix is missing. The only window where `Ok(false)` happens is *inside* the handler, after `require_access` and during the two Argon2 runs, and a router test can't hit that window by ordering requests.
- **Failure scenario:** someone later simplifies `change_own_password` to a plain `UPDATE users SET password_hash = ? WHERE id = ?`. All twelve tests still pass, and a self-change that races a teacher reset overwrites the reset.
- **Fix shape:** prove the guard at the layer that holds it. Add a `db.rs` unit test: insert a session, call `set_password` (the reset), then call `change_own_password` with the old session hash. Assert that it returns `Ok(false)`, that `password_hash` is still the reset's, that no session row was inserted, and that known browsers are untouched. Keep router test 9 if you like, but describe it as covering the middleware path, not M3. Update Completion Criteria 3 to cite the db test.

### F2: "Every known browser when there is none" silently deletes nothing if written as the one statement
- **Type:** non-blocking · **Category:** design · **Severity:** LOW
- **Location:** Step 2.4; tests
- **Defect:** `DELETE FROM known_browsers WHERE user_id = ? AND token_hash != ?` bound with `None` (SQL NULL) matches **no** rows, because `x != NULL` is NULL. The plan says "or every known browser of the user when there is none", but it doesn't say this needs a separate statement. No test covers the `None` path: test 5 always posts from browser A, which has a known cookie.
- **Failure scenario:** a session whose browser has no (or a malformed) known cookie changes the password. Every other known browser of that user keeps skipping the throttle, which contradicts Decision 4.
- **Fix shape:** spell out two statements (`… AND token_hash != ?` when `Some`, `DELETE … WHERE user_id = ?` when `None`). Then either add a test case that posts with only the session cookie and asserts that a previously known browser B is no longer known, or cover it in the F1 db test.

### F3: `Busy` on the current-password check releases a reservation that a known browser never made
- **Type:** non-blocking · **Category:** design · **Severity:** LOW
- **Location:** Step 3, `post_password` 3.3 ("`Busy` → `clear_reservation`")
- **Defect:** a known browser skips `reserve`. On `Busy`, `clear_reservation` still runs. When the account already has an entry with `failures > 0` (someone else's wrong tries), it sets `next_allowed = touched` (`session.rs` `clear_reservation`), which erases that pending delay. `post_login` has the same quirk, and the plan copies it via "exactly as `post_login`".
- **Failure scenario:** a classmate builds up a 16 s delay on kim's ID. Kim's own known browser posts the change form while the hashing queue is full. The classmate's delay is wiped and their next guess goes through at once. This is minor, since it needs a full hash queue at that moment.
- **Fix shape:** call `clear_reservation` only when this request reserved (`!known`). Whether to fix `post_login` too is the Author's call, either surgically here or as a Deferred entry.

### F4: Rotating the session resets the 12-hour sign-in limit
- **Type:** non-blocking · **Category:** design · **Severity:** LOW
- **Location:** Step 2.2 (`INSERT INTO sessions … created_at`), GDD systems.md:206 ("A sign-in lasts until the browser closes, 1 hour idle or 12 hours in all")
- **Defect:** the new session row gets `created_at = now`, so a password change restarts the `session_max_secs` clock. This is defensible, since the user just re-proved the password, but it isn't stated anywhere, and the GDD line becomes subtly untrue.
- **Fix shape:** either carry the old session's `created_at` into the new row (read it in the same transaction), or say in Decision 4 and the GDD note that a change counts as a fresh sign-in.

### F5: A sign-in racing the change can keep a session made with the old password
- **Type:** non-blocking · **Category:** design · **Severity:** LOW
- **Location:** Decision 4 / Completion Criteria 3 ("ends every other session")
- **Defect:** `post_login` verifies against the hash it read before the change committed, then calls `insert_session` after it. A sign-in with the old password whose Argon2 verify overlaps the change's commit therefore leaves a live session. The window is about one Argon2 run. The teacher reset (`set_password`) has the same race today.
- **Fix shape:** record it in Notes / Open Risks as an accepted residual, shared with the teacher reset. Alternatively, make `insert_session` conditional on the hash it verified (`INSERT … SELECT … WHERE (SELECT password_hash FROM users WHERE id = ?) = ?`), but that changes `post_login` and is probably out of scope.

### F6: Test 4 doesn't prove the "no hash" half of its criterion
- **Type:** non-blocking · **Category:** accuracy · **Severity:** LOW
- **Location:** Step 4, test 4; Completion Criteria 2 ("a rule mistake takes no throttle slot and no hash")
- **Defect:** test 4 asserts "no 429 on a following try", which covers the throttle slot only. Nothing checks that no Argon2 run happened, so a handler that hashed first and checked the rule second would pass.
- **Fix shape:** assert that `app.state.hasher.runs` (public, "tests read it") is unchanged across the five rule-problem POSTs.

### F7: The "immediate retry → 429" assertions depend on not crossing a second boundary
- **Type:** non-blocking · **Category:** design · **Severity:** LOW
- **Location:** Step 4, tests 2 and 5 ("B's no longer does")
- **Defect:** the throttle works in whole Unix seconds with a 1 s first delay (`delay_after(1) = 1`, `wait` is `now < next_allowed`). A wrong try at T.99 followed by a retry at T+1.00 sees no wait, so the test intermittently gets 401 instead of 429. `tests/sign_in.rs:35–44` already carries this exposure, and the plan adds two more instances.
- **Fix shape:** build those cases with `TestApp::with(|c| c.throttle_first_secs = 60)` (the cap is irrelevant there), and use `clear_throttle` where a later sign-in is needed, as the plan already does.

## Reviewer's view
I would build this plan once F1 is fixed. The design is sound and closely follows `post_login` and `set_password`, and every code claim I checked held, including the Step 2b deletions. F1 matters because the race guard is the plan's one new safety property, and as written, nothing would catch its removal. F2–F7 are small and can be folded at close. F2 and F6 are the ones most likely to hide a real regression.
