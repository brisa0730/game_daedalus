# Plan: Gallery change-password

**Date:** 2026-11-03 | **Status:** draft (critic-reviewed; handshake-approved, 2 rounds)

## Overview

**Where things stand:** Nobody can change their own password in the class gallery. Every password is made by the gallery: the teacher resets a student's, and only the operator's command line resets the teacher's. A student who wants a password they can remember has no way to set one.

**What this plan changes:**
- Every signed-in student and the teacher get a "change password" page, linked next to the sign-out button.
- To change it they type their current password, then the new one twice.
- A new password has at least 8 characters and isn't the login ID or the old password.
- Wrong current passwords are slowed down the same way wrong sign-ins are.
- Changing the password signs out every other browser, including any copy of this one, and keeps this browser signed in.
- Teacher resets keep working as the way back from a forgotten password.

**How you'll know it worked:** On the live gallery, the test student changes their password, the old one stops working, the new one signs in, and a second browser signed in as them is sent to the sign-in page.

**Not in this plan:** recovering a forgotten password without the teacher (no email, no questions).

**Why a plan and not a patch:** a security change on an internet-facing server, touching the database, routes, pages, text and tests, then a live redeploy.

**Design choice:** the operator chose every recommended option (2026-11-03): all users, the current password required, at least 8 characters of any kind, other browsers signed out, teacher reset kept.

---

## Source
- **Trigger:** operator request (2026-11-03): "I still want it. let's have it", after learning nobody can change their password.
- **Related issues:** none.
- **Scope:** the class gallery server (`Deploy/gallery/`) only; the Unity package is untouched.
- **Depends on:** Phase 5 `gallery-server` (v0.16): sessions, the throttle, the hasher, the route table, the guard layers. Live under launchd.

### Decisions (operator, 2026-11-03, all as recommended)
1. Every signed-in user (students and the teacher) can change their own password.
2. The form asks for the current password, then the new one twice. A wrong current password goes through **the same per-account throttle as sign-in, with the same known-browser skip** (a browser that has signed in to that ID before isn't slowed, exactly as at `/login`). Leaving the skip out would protect nothing: that browser can already guess at `/login` unthrottled. It would only let a classmate scripting wrong sign-ins also block the owner's change form (critic M2).
3. New password: at least 8 characters (Unicode scalar values, so 8 Korean syllables count), at most 128; any characters; not equal to the current password; not equal to the login ID (ASCII case ignored). Kept exactly as typed: no trimming, no Unicode normalization (accepted: a password typed as decomposed jamo won't match one typed precomposed; IMEs type precomposed).
4. A change **issues a new session token for this browser**, ends every other session of the user (so a copied cookie of this browser dies too) and forgets the user's other known browsers. This browser stays signed in and known. The new session keeps the old one's start time, so the 12-hour sign-in limit still counts from the original sign-in (handshake round 1, F4).
5. The teacher's reset stays (it still signs the student out everywhere). `gallery teacher reset` stays as the teacher's fallback.
6. A "비밀번호 변경" link in the header next to 로그아웃, opening its own page. Korean text in `strings.rs`.

## File Manifest

| Action | File | Purpose |
|--------|------|---------|
| MODIFY | `Deploy/gallery/src/text.rs` | `check_new_password` and `PasswordProblem`, with unit tests |
| MODIFY | `Deploy/gallery/src/db.rs` | `change_own_password` (one transaction, conditional on this session); Step 2b drops `Game`'s unread fields |
| MODIFY | `Deploy/gallery/src/session.rs` | `password_page` (GET) and `post_password` (POST) |
| MODIFY | `Deploy/gallery/src/app.rs` | Routes `GET /password` and `POST /password`, access `Page` |
| MODIFY | `Deploy/gallery/src/pages.rs` | The header link next to the sign-out button |
| MODIFY | `Deploy/gallery/src/strings.rs` | The page's Korean text: labels, the rule line, six error lines, the done line |
| MODIFY | `Deploy/gallery/static/gallery.css` | `.done` (the success line), next to `.error` |
| MODIFY | `Deploy/gallery/src/files.rs` | Deferred.md cleanup (Step 2b): unused `safe_join`, unreachable `.gz` / `html` branches |
| MODIFY | `Deploy/gallery/tests/support/mod.rs` | `url_encode` moved here; `sign_in` encodes its form; `sign_in_keeping_browser` returns the session and known cookies; `clear_throttle` |
| MODIFY | `Deploy/gallery/tests/teacher.rs` | Uses the shared `url_encode` |
| NEW | `Deploy/gallery/tests/password.rs` | Router tests for the whole flow |

---

## Steps

### Step 1: The password rule
- **MODIFY `src/text.rs`**:
    - `pub enum PasswordProblem { Mismatch, TooShort, TooLong, SameAsLogin, SameAsCurrent }`.
    - `pub fn check_new_password(new: &str, confirm: &str, current: &str, login: &str) -> Result<(), PasswordProblem>`, in this order: `new != confirm` → Mismatch; `new.chars().count() < 8` → TooShort; `> 128` → TooLong; `new.eq_ignore_ascii_case(login)` → SameAsLogin (logins are ASCII); `new == current` → SameAsCurrent. Every input is typed text, so the rule reveals nothing and costs no hash.
- **Tests (unit, in `text.rs`):** each problem; exactly 8 and exactly 128 characters pass, 7 and 129 fail; 8 Korean syllables pass; the login in upper case is refused; 8 spaces pass (kept as typed).
- **Verify:** `cargo test text::`.

### Step 2: The database change
- **MODIFY `src/db.rs`**: `pub async fn change_own_password(&self, user_id: i64, hash: &str, old_session: &[u8], new_session: &[u8], keep_browser: Option<&[u8]>, now: i64) -> Result<bool>`, one transaction:
    1. `UPDATE users SET password_hash = ? WHERE id = ? AND EXISTS (SELECT 1 FROM sessions WHERE token_hash = ? AND user_id = ?)` with `old_session`. **0 rows → roll back, return `Ok(false)`**: the session ended meanwhile (a teacher reset deletes every session), so the change must not overwrite the reset (critic M3).
    2. `INSERT INTO sessions (token_hash, user_id, created_at, last_seen) SELECT ?, user_id, created_at, ? FROM sessions WHERE token_hash = ?`: the new session carries the old one's `created_at` (Decision 4) with `last_seen = now`.
    3. `DELETE FROM sessions WHERE user_id = ? AND token_hash != ?` with `new_session` (the old token goes too).
    4. Known browsers, **two explicit statements** (`token_hash != NULL` matches nothing in SQL; handshake round 1, F2): with `Some(keep)`, `DELETE FROM known_browsers WHERE user_id = ? AND token_hash != ?`; with `None`, `DELETE FROM known_browsers WHERE user_id = ?`.
- **Tests (in `db.rs`'s test module or `tests/password.rs` against `state.db`; handshake round 1, F1, the layer that holds the race guard):**
    - **Reset race:** create a user with a session and a known browser; call `set_password` (the reset); then `change_own_password` with the old session hash → `Ok(false)`; `password_hash` is still the reset's; no session row exists; the known-browser rows are as `set_password` left them.
    - **Rotation:** with two sessions and two known browsers, `change_own_password(…, Some(browser A))` → `Ok(true)`; only the new session remains, with the old `created_at`; only browser A stays known.
    - **No known cookie:** the same with `None` → no known browser of the user remains.
    - The doc comment says how it differs from `set_password` (the teacher's reset forgets every session and browser).
- **Verify:** `cargo build`.

### Step 2b: Roll in the triggered gallery-crate cleanup
- Deferred.md "Leftover spike code in the gallery crate" fires on this plan's `db.rs` change (its trigger: the next change to `files.rs` or `db.rs`). Checked by the critic: nothing reaches the removed code (`headers_for` is called only for `build_files` names from `play.rs`; gzip builds are rejected; the player page sets its own headers; `safe_join` is used only by its test; `Game.bytes` / `updated_at` are read nowhere).
- **MODIFY `src/files.rs`**: delete `safe_join`, its test and its now-unused `use std::path` import; drop the `.gz` branch and the `html` type from `headers_for`, the `.gz` and `index.html` lines from its test, and the gzip / safe-path wording from the doc comments (lines 1 and 9).
- **MODIFY `src/db.rs`**: drop `bytes` and `updated_at` from `Game` and its `row.get` lines, and `g.bytes, g.updated_at` from `GAME_COLUMNS`. The callers' `ORDER BY g.updated_at` stays (valid SQLite: ORDER BY may name any column of the FROM tables). `NewGame.bytes` stays (it is written).
- **Verify:** `cargo build`; `grep -rn "safe_join" src` returns nothing; `cargo test` still green.

### Step 3: The page and the handler
- **MODIFY `src/strings.rs`** (Korean, 해요체 like the rest): `NAV_PASSWORD` "비밀번호 변경", `PASSWORD_TITLE` "비밀번호 변경", `PASSWORD_CURRENT` "지금 비밀번호", `PASSWORD_NEW` "새 비밀번호", `PASSWORD_CONFIRM` "새 비밀번호 확인", `PASSWORD_BUTTON` "바꾸기", `PASSWORD_RULE` "8글자 이상. 아이디나 지금 비밀번호와 달라야 해요.", and the six error lines `PASSWORD_WRONG_CURRENT` "지금 비밀번호가 틀렸어요.", `PASSWORD_MISMATCH` "새 비밀번호 두 칸이 서로 달라요.", `PASSWORD_TOO_SHORT` "새 비밀번호는 8글자 이상이어야 해요.", `PASSWORD_TOO_LONG` "새 비밀번호는 128글자까지예요.", `PASSWORD_SAME_AS_LOGIN` "새 비밀번호가 아이디와 같아요.", `PASSWORD_SAME_AS_CURRENT` "새 비밀번호가 지금 비밀번호와 같아요."; `PASSWORD_CHANGED` "비밀번호를 바꿨어요. 다른 브라우저에서는 로그아웃됐어요." Wording is the operator's to adjust.
- **MODIFY `static/gallery.css`**: `.done { color: var(--accent); font-weight: 600; }` next to `.error`.
- **MODIFY `src/session.rs`**:
    - `password_page` (GET, `Extension<CurrentUser>`, `Query` with an optional `changed`): rendered with `pages::page(status, s::PASSWORD_TITLE, Some(&user), body)` (the routing test checks every signed-in page shows the name and sign-out). `form.login method="post" action="/password"`: a hidden `input name="username" autocomplete="username" value=(user.login)` so password managers update the right entry; three `type="password"` fields (`current-password`, `new-password`, `new-password`), the rule line, then `p.error role="alert"` or `p.done` (when `?changed=1`). `Cache-Control: no-store`. The form never echoes a typed password.
    - `post_password` (POST, `Extension<CurrentUser>`, `Extension<SessionHash>`, headers, `Form { current, new, confirm }`), in order:
        1. **Rule first:** `check_new_password(new, confirm, current, user.login)`; a problem → 400 with its line. No throttle slot, no hash (critic M1).
        2. **Throttle**, exactly as `post_login`: the known-browser check with this request's `__Host-gallery_known` cookie; when not known, `reserve(user.id, now)`; a wait → 429 with `s::wait_seconds(wait)`.
        3. **Verify the current password:** false → `fail(user.id)`, 401 `PASSWORD_WRONG_CURRENT`; `Busy` → `clear_reservation` **only if this request reserved** (not for a known browser, which would otherwise erase someone else's pending delay; handshake round 1, F3), 503 `LOGIN_BUSY`; `Internal` → 500 `LOGIN_BUSY`.
        4. `clear(user.id)`.
        5. **Hash the new password:** `Busy` → 503 `LOGIN_BUSY`; `Internal` → 500 `LOGIN_BUSY`.
        6. `change_own_password(user.id, hash, old session hash, new session hash, this browser's known-token hash if well-formed, now)`: `Ok(false)` → 303 `/login` (the session ended meanwhile); `Err` → 500 `LOGIN_BUSY`.
        7. Log `password changed login=…` (never a password). **303 to `/password?changed=1`** (Post/Redirect/Get, so a refresh doesn't re-submit and count as a wrong current password) with `Set-Cookie` for the new `__Host-gallery_session` (same attributes as `post_login`).
- **MODIFY `src/app.rs`**: `route(Method::GET, "/password", Page, get(session::password_page))`, `route(Method::POST, "/password", Page, post(session::post_password))`. The gallery layer's same-origin check covers the POST, and `tests/routing.rs` already sweeps every route for the cross-origin refusal and the sign-in redirect.
- **MODIFY `src/pages.rs`**: in `div.who`, `a href="/password" { (s::NAV_PASSWORD) }` before the sign-out form.
- **Verify:** `cargo build`; `cargo clippy --all-targets -- -D warnings`.

### Step 4: Router tests
- **MODIFY `tests/support/mod.rs`**: move `url_encode` here (from `tests/teacher.rs`, which then uses it); `sign_in` percent-encodes login and password; `sign_in_keeping_browser(login, password, known: Option<&str>) -> (session_cookie, known_cookie)` for the known-browser cases; `clear_throttle(user_id)` (clears `state.throttle` for one account, so cases can sign in again right after a deliberate failure).
- **NEW `tests/password.rs`** (each case clears the throttle, or orders its sign-ins good-first, wherever a deliberate failure precedes a sign-in; critic H2). **Every case** builds its app with `TestApp::with(|c| c.throttle_first_secs = 60)`, so neither an expected 429 (F7) nor an expected "no 429" (tests 3, 4, 10; handshake round 2, F8) can be decided by a retry crossing a 1 s boundary:
    1. `GET /password` signed in → 200, three password fields, the hidden username, `no-store`. (The redirect without a session and the cross-origin refusal are covered by the routing sweep.)
    2. **Wrong current password** → 401 with its line; an immediate retry → 429; **`/login` for that account without a known cookie → 429 too** (one throttle); after `clear_throttle`, the old password still signs in (unchanged).
    3. **A known browser isn't slowed:** with a known cookie, a wrong current password and an immediate retry both answer 401, not 429.
    4. **Each rule problem** (mismatch, 7 characters, 129 characters, the login in upper case, equal to the current) → 400 with its line, no 429 on a following try (no throttle slot taken), **`state.hasher.runs` unchanged across the five POSTs** (no hash; handshake round 1, F6), password unchanged.
    5. **Success:** sign in browser A (with its known cookie) and browser B, and a second session as a "copy" of A's cookie value. (A POST from a session with **no** known cookie forgets every known browser: covered by the db test "No known cookie".) POST from A → 303 to `/password?changed=1` with a new session cookie; the new cookie opens `/`; **A's old cookie value gets 303 `/login`**; B's session gets 303 `/login`; another user's session still opens `/`; the new password signs in, and after `clear_throttle` the old one is refused; A's known cookie still skips the throttle, B's no longer does.
    6. **A Korean password** (`가나다라마바사아`) and **a password with `& + % = ` and spaces** can be set and sign in (form encoding).
    7. **The teacher** can change their own password.
    8. **Teacher reset after a self-change** still works: the generated password signs in; after `clear_throttle`, the self-chosen one is refused.
    9. **A POST after a reset** (the middleware path, not the race guard, which the db test proves): reset the student between loading the page and posting; the POST → 303 `/login` (`require_access`), and the password is the reset's.
    10. **Busy:** hold every `state.hasher.permits` permit; the POST → 503 with `LOGIN_BUSY`; release; an immediate retry isn't 429 (the reservation was released).
    11. **No password in any answer:** across cases 2–7, no response body or header contains the typed current, new or confirm text.
    12. The header of a signed-in page links to `/password`.
- **Verify:** `cargo test`.

### Step 5: Run Tests
- In `Deploy/gallery/`: `cargo test` and `cargo clippy --all-targets -- -D warnings`. Fix any failures. `grep -n "tracing::" src/session.rs` shows no field carrying a password.

### Step 6: Redeploy and check on the live gallery (operator-approved with this plan)
- Keep the current binary for rollback: copy `~/Library/Application Support/daedalus-gallery/bin/gallery` to `gallery.previous`. (No schema change, so the database is safe either way.)
- Redeploy per CLAUDE.md § Build / run: `cargo build --release`, `install -m 0755` into that `bin/`, `launchctl kickstart -k gui/$(id -u)/com.orcapafea.daedalus.gallery`; `/login` answers 200 through the tunnel. Outside class time (open games show the "reopen" line after a restart).
- Headed Chrome over CDP, two fresh profiles A and B, test student `kbiny` (current password from the operator, passed by environment variable; the new test password generated at run time and **never printed or logged**; profiles deleted afterwards):
    1. A and B sign in.
    2. A opens 비밀번호 변경 from the header; a wrong current password is refused (A is a known browser, so no wait).
    3. A changes the password; A lands on the done line and stays signed in.
    4. B's next page goes to the sign-in page.
    5. In B, the old password is refused; wait ≥ 2 s (B is no longer known, so it was throttled), or retry on a wait line; the new password signs in.
    - Screenshots of the page, the error line and the done line to `TestResults/`; look at them.
- Afterwards the operator resets `kbiny` or removes it. Rollback if anything misbehaves: `install` `gallery.previous` back and kickstart.
- **Verify:** the screenshots and the driver's step log (no passwords in it).

### Step 7: Verify
- Walk the Completion Criteria.

### Step 8 (Final): Code Review
- Run the code-review agent (`.claude/agents/workflow/code-review.md`) on all changed files. Fix any HIGH/MED issues. Report the review result to the user.

---

## Completion Criteria
- [ ] `cargo test` passes, including `tests/password.rs` (all twelve router cases and the three db-level cases: reset race, rotation, no known cookie), the `text.rs` rule tests and the routing sweep with the new routes; `cargo clippy --all-targets -- -D warnings` clean.
- [ ] A wrong current password is throttled through the same per-account throttle as sign-in, with the same known-browser skip; a rule mistake takes no throttle slot and no hash.
- [ ] A change rotates this browser's session token (keeping its start time), ends every other session of that user (a copied cookie included), forgets the user's other known browsers (all of them when this browser has no known cookie), and leaves other users alone; a change racing a teacher reset loses to the reset (the db-level race test).
- [ ] No response, header or log line contains a password (case 11; the `tracing::` grep; the live driver log).
- [ ] `safe_join`, the `.gz` / `html` branches and `db::Game`'s unread fields are gone (Step 2b).
- [ ] The redeployed gallery answers through the tunnel; the live two-browser check passed and its screenshots were looked at.
- [ ] Code review: no open HIGH/MED.

---

## Notes to Spec / Open Risks
- **GDD change (systems.md § Share, "Sign in"):** keep "no email, no self-service recovery, no forced change" (all still true: nobody is forced to change at first sign-in); add that any signed-in user can change their own password with the current one (decisions 1–6); "a forgotten password → the teacher resets it" stays as the recovery path. GDD version bump. decisions.md: a new row; the teacher can now change their own password on the site, and the command line stays the way back from a forgotten teacher password.
- **Shared laptops:** someone who finds a signed-in browser can't change the password without the current one; they could still use the open session, as today. A known browser can guess the current password unthrottled, exactly as at `/login` (Phase 5's accepted residual risk; unchanged).
- **Throttle interplay:** a wrong current password slows that account's sign-ins on unknown browsers too (one throttle per account, as at `/login`). A teacher reset clears a student's; `gallery teacher reset` runs in another process and can't clear the server's in-memory throttle, so the teacher's delay simply expires (≤ 30 s).
- **Unicode:** passwords are compared exactly (no normalization), as at `/login`.
- **A sign-in racing the change** (accepted residual, handshake round 1, F5): a sign-in with the old password whose Argon2 check overlaps the change's commit can still create a session, because `post_login` checks the hash it read before. The window is about one Argon2 run (~66 ms); the teacher reset has the same race today. Not fixed here (it would change `post_login`).
- **`post_login`'s Busy path** releases a reservation a known browser never made (the F3 quirk), erasing someone else's pending delay when the hashing queue is full. Fixed in the new handler only; `post_login` is filed in Deferred.md (Post-Plan Cleanup) to keep this plan surgical.

**If anything here contradicts the Overview, fix the Overview.**

---

## Post-Plan Cleanup
After code review passes:
1. Append execution trailers to this plan file.
2. Move this plan to `Documents/Plans/archive/`.
3. Update `Documents/GDD/systems.md` § Share ("Sign in", per Notes) and `architecture.md`'s Gallery server row if the account duties need a word, bump the GDD version, add the decision row to `.claude/memory/decisions.md`, add a Changelog entry, and note anything new in `.claude/memory/framework.md` § Gallery server.
4. Deferred.md: delete "Leftover spike code in the gallery crate" (done in Step 2b; record it in the Changelog). File "`post_login`'s Busy path clears a reservation a known browser never made" (LOW; trigger: the next change to `post_login` or the throttle; fix shape: release only when `!known`, as `post_password` does). File any other leftover.
5. Commit all plan work.

## Workflow
Execute steps sequentially. Proceed to the next step without waiting for user approval. Only stop if blocked. If blocked, stop and report: what step, what was attempted, what failed.

---

## Critic review (draft round), folded
- H1 (session token not rotated) → Decision 4, Steps 2 and 3.7, test 5. H2 (tests vs the throttle) → `clear_throttle`, good-first ordering, Step 4 note. H3 (live check vs the throttle, password logging) → Step 6 waits/retry, no printing.
- M1 (rule after hashing) → rule first, criterion reworded. M2 (no-skip rationale) → Decision 2 now keeps the known-browser skip, test 3. M3 (reset race) → conditional UPDATE, test 9. M4 (helpers) → support changes. M5 (coverage) → tests 2, 3, 5, 10, 11; routing sweep noted; `pages::page` with the user. M6 (GDD edit would drop "no forced change") → Notes. M7 (`framework.md § Gallery server` missing) → **rejected**: it is `.claude/memory/framework.md`, which has that section; the path is now spelled out.
- L1 → `eq_ignore_ascii_case`, normalization recorded as accepted. L2 → unchanged (the same 2 MB form limit as `/login`). L3 → PRG and the hidden username. L4 → Notes. L5 → Step 2b wording. L6 → six lines, `.done`. L7 → Overview reworded. L8 → Step 8 line. L9 → rollback in Step 6.

---

## Review round 1

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

---

## Review round 2

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
