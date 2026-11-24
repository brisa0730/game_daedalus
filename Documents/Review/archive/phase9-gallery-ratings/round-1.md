---
slug: phase9-gallery-ratings
round: 1
verdict: changes-requested
reviewer: claude-cowork
updated: 2026-11-24T14:03:22+09:00
blocking: 1
recommendations: 0
---

# Round 1: Phase 9, gallery ratings

**What I could run:** a shell over the connected `daedalus` folder: `cat`, `grep`, `sed`. I read the whole prompt, the project's `CLAUDE.md`, and these files in full: `Deploy/gallery/src/db.rs`, `app.rs`, `config.rs`, `main.rs`, `lib.rs`, `gallery.rs`, `guard.rs`, `static/gallery.js`, `static/gallery.css`, `tests/routing.rs`, `tests/look.rs`, all three migrations. I also read the relevant parts of `session.rs` (cookie, `new_token`, `token_hash`, `is_token`, `current_user`, `require_access`), `upload.rs` (`json_answer`, `valid_game_id`), `teacher.rs` (`remove`), `backup.rs` (header) and `tests/support/mod.rs` (`Answer`, `TestApp::with`, `get`, `post_form`). I did not run `cargo test`, clippy or a browser. Anything that needs one of those is a finding for the Author.

**Verified against the code:**
- `GAME_COLUMNS` (db.rs:111) ends with `FROM games g LEFT JOIN users u …`, so "two correlated subqueries before `FROM`" fits. `Game` is built only in `Game::from_row` (no struct literals anywhere else in `src/` or `tests/`), so adding two fields touches one place. The `ratings` primary key `(game_id, rater_hash)` gives the subqueries a leading `game_id` index.
- `tombstone_game` (db.rs:494) is a single `UPDATE` today. Owner and teacher game removal both go through `gallery::remove` → `remove_game`, and account removal (`teacher::remove`, teacher.rs:376) calls `remove_game` per game before `delete_user`. So one transaction in `tombstone_game` covers all three paths, as Step 3 says.
- `the_real_name_migration_keeps_todays_names` (db.rs:539) slices `migrations[..1]`. The Step 1 test can slice `[..3]` the same way.
- `session::is_token` is private (session.rs:54). `cookie`, `new_token` and `token_hash` (SHA-256) are already `pub`. The known-browser cookie is `__Host-gallery_known`, with the same attribute string the rater cookie copies (session.rs:269).
- The same-origin guard (`guard::gallery_layer`) refuses every non-GET/HEAD without `Sec-Fetch-Site: same-origin` or a matching `Origin`. A game on the sibling games host is `same-site` and is refused, so a game can't POST a rating.
- Routing tests: `every_non_public_route_needs_a_session` POSTs `text=x` to `/play/1/…` and expects 404 with no `Location` for `Open` routes. `valid_game_id("1")` is false (`ID_LEN`), so both new handlers answer 404 before they touch the body. With `Bytes` last, nothing rejects first. `every_post_route_refuses_other_origins` gets 403 from the layer before any handler. `every_signed_in_page_shows_the_name_and_sign_out` GETs `/play/{teacher's game}` as the teacher, who is the owner, so that page renders `RATE_OWN`. That's fine.
- SQL: `INSERT … SELECT … WHERE EXISTS(…) ON CONFLICT … DO UPDATE` is valid. SQLite's parsing ambiguity only arises when the `SELECT` has no `WHERE`. `INSERT OR IGNORE … SELECT ?, ?, ? WHERE EXISTS (…)` is valid too.
- Arithmetic: `average_tenths = (20·total + count) / (2·count)` is `floor(10·total/count + ½)`. (17, 4) → 344/8 = 43, (13, 3) → 263/6 = 43, (14, 3) → 283/6 = 47, (3, 3) → 63/6 = 10, (15, 3) → 303/6 = 50. All match. `365·24·3600 = 31 536 000` matches test 1's `Max-Age`. `rate_wait`: 180 → 3분, 61 → ⌈61/60⌉ = 2분, 60 → 1분, 59 → 59초, 1 → 1초. All match.
- Gate rounding can't open early. `opened_at = ⌊t_open⌋`. The client's deadline is `t_response + wait`, with `wait = opened_at + gate − ⌊t_server⌋`. So a click at or after the deadline has `⌊t⌋ ≥ opened_at + gate`, and the server accepts it.
- Body limit: `DefaultBodyLimit` isn't used in `src/` yet (a new import). axum's `Bytes` honours a route-level `DefaultBodyLimit` and answers 413 above it, so test 4's 413 holds. That 413 comes from the extractor, before the game check. That's fine, because test 4 uses a live game.
- Removal and open/rate race: SQLite serialises writers, and both inserts re-check `removed_at IS NULL`. The "Open risks" claim holds.
- `look.rs` `backdate` opens a raw `SqlitePool` on `config.db_path()`, as Step 9's helpers say.

**Accepted on trust:** that Chrome keeps focus in a cross-origin iframe after `iframe.focus()` from a click handler, and that the play bar stays within the 220 px budget at 1366 × 768 with five ≥ 28 px buttons (global `button` gives them `min-height: 40px`, the same as `#fullscreen`). Steps 10 and 11 check both.

---

## Findings

### F1: A failed open leaves the page stuck in a countdown loop
- **Type:** blocking · **Category:** design · **Severity:** HIGH
- **Location:** Step 7 (script), with Step 5 `POST /rate` (409 path)
- **Defect:** The script sends the open POST only once, on start ("On failure, keep the rendered state"), and on a 409 it only "restart[s] the countdown". If that one open POST fails, no open row ever exists for this page load. That happens on a network blip, or on a 500 or a closed listener during Step 11's own `launchctl kickstart -k` while a class has pages open. It also happens when the browser refuses the cookie: then every open POST mints a new token and the next request carries none. Then:
  1. The page counts down from the rendered `data-wait` (180 for a fresh browser). Or, if the timer only starts on an `ok` open, it shows "3분 후에…" forever. Step 7 doesn't say which.
  2. A star click gets 409 with `wait` = the full gate, because `rating_state` finds no open, or there is no cookie.
  3. The countdown restarts at 180 s. Nothing ever records an open, so step 2 repeats forever.

  The student sees the stars come and go every three minutes, with no message, until they reload. This is the gate failing *closed*, which is the right direction, but with no way out. The operator check (Step 11) wouldn't catch it, because it never fails an open.
- **Fix shape:** on a 409, the script re-sends the open POST (it is idempotent, keeps the earliest time, and is the only thing that records an open), then counts down from *its* `wait`. Say whether the countdown runs from `data-wait` when the start-up open fails. Pick one and state it. Optionally, show `msgFailed` when the open POST itself fails. No server change is needed, and `/rate` must still never record an open.

### F2: The hover-preview selectors as written don't unlight anything
- **Type:** non-blocking · **Category:** accuracy · **Severity:** MED
- **Location:** Step 8, `gallery.css` bullet
- **Defect:** `#rate-stars:hover .star` has specificity (1,2,0) and `.star:hover ~ .star` has (0,3,0). The ID rule wins, so while the pointer is anywhere in `#rate-stars`, all five stars are lit, and the "unlit after the hovered one" preview never shows. Also, Step 6 puts the `RATE_PROMPT` label *inside* `#rate-stars`, so hovering the words "재미있었나요?" lights all five stars too. The look check can't catch either problem, because screenshots have no hover.
- **Fix shape:** scope both rules to a wrapper around the five buttons only, at the same ID weight (e.g. `#rate-buttons:hover .star` lit, `#rate-buttons .star:hover ~ .star` unlit), or put the label outside that wrapper. While here: the existing sheet already needed `progress[hidden] { display: none; }` (gallery.css:146). If any rule gives `#rate-wait`, `#rate-stars` or `#rating-avg` a `display`, it overrides `hidden`. Either give them no `display`, or add the same `[hidden]` guard.

### F3: The 179 s gate test can flake on a second boundary
- **Type:** non-blocking · **Category:** design · **Severity:** MED
- **Location:** Step 9 test 2 ("At 179 s back, still 409")
- **Defect:** `backdate_open(app, id, 179)` reads `unix_now()` and writes `opened_at = T₀ − 179`. The rate handler reads `unix_now()` again a few milliseconds later. If a second boundary falls in between, the handler sees 180 s elapsed and answers 200, so the test fails. That happens on roughly (elapsed ms / 1000) of runs. There is no injected clock (`unix_now` is the only clock read). The "180 s back → 200" half is safe, because time only moves forward.
- **Fix shape:** pin the exact boundary in the Step 2 db unit tests, where `rate(…, opened_by, now)` takes explicit times (`opened_at = 1000`: `opened_by = 1179` refused, `1180` accepted). Keep the HTTP test with a margin (e.g. 170 s back → 409, 180 s → 200).

### F4: The look check's countdown page names a browser but not a game, and lee owns `traps`
- **Type:** non-blocking · **Category:** accuracy · **Severity:** LOW
- **Location:** Step 10, `play-rate-wait (lee, fresh browser: countdown)`
- **Defect:** In `look.rs`, `traps` belongs to lee (look.rs:66), and the existing `play` page is lee on `traps` (look.rs:86). By analogy, an implementer may render `play-rate-wait` as lee on `traps`. That is an owner page: `RATE_OWN`, no `#rate`, no countdown, and Step 10's wire assertion "on every non-owner play page" would silently skip it. The existing `play` page also becomes an owner page after this phase, so the list of "non-owner play pages" whose wires are asserted needs to be explicit.
- **Fix shape:** name the game for each new page (e.g. `play-rate-wait`: lee on `old` or `spikes`). List which pages get the `#rate` wire assertions (`play-public`, `play-rate-wait`, `play-rate-ready`, `play-rated-hint`, `play-rated-avg`) and which are owner pages (`play`, `play-own`).

### F5: "The GET writes nothing" is stated too broadly
- **Type:** non-blocking · **Category:** accuracy · **Severity:** LOW
- **Location:** Completion Criteria 2, Step 14 ("the rule that GET writes nothing"), Step 6
- **Defect:** A signed-in GET of `/play/{id}` already writes. `session::current_user` touches `sessions.last_seen` every 300 s and deletes an expired session (session.rs:62–70). A reviewer or test that takes the criterion literally will flag existing behaviour or try to "fix" it.
- **Fix shape:** say "the GET writes nothing to `game_opens` or `ratings` and sets no rater cookie", which is what test 10 checks.

### F6: Pressed state on every lit star misreports the rating to screen readers
- **Type:** non-blocking · **Category:** design · **Severity:** LOW
- **Location:** Step 6 (`aria-pressed`), Step 7 ("set `aria-pressed`"), the Step 6 test ("`aria-pressed="true"` × its stars")
- **Defect:** With `aria-pressed="true"` on stars 1…k, a 3-star rating is announced as three pressed toggles ("별 1개, pressed; 별 2개, pressed; 별 3개, pressed"), which reads as three choices. The group has one value.
- **Fix shape:** `aria-pressed="true"` on star k only (the others `false`), with `class="on"` still on 1…k for the look. Adjust the test to "exactly one `aria-pressed="true"`, on star `mine`, and `mine` stars with `on`".

### F7: Rollback loses everything since the deploy, not just ratings
- **Type:** non-blocking · **Category:** accuracy · **Severity:** LOW
- **Location:** Step 11, "Rollback: … Ratings made since the deploy are lost."
- **Defect:** Restoring the step-2 database copy (per `backup.rs`'s restore notes: bootout, delete `-wal`/`-shm`, copy back) also drops every upload, update, removal, new account, nickname change and password change made since the deploy. An upload's version folder would then exist with no row, and the startup sweep deletes it. The sentence understates what the operator gives up.
- **Fix shape:** "Everything since the deploy is lost (ratings, uploads, account and password changes), so roll back soon or not at all."

### F8: The Overview says "open for three minutes", but the gate measures time since the first open
- **Type:** non-blocking · **Category:** accuracy · **Severity:** LOW
- **Location:** Overview, bullet 2, against decision 3
- **Defect:** Decision 3 (and the code) counts 180 s from this browser's *first* open, kept forever (or 30 days unrated). A browser that opened the page for five seconds yesterday can rate the moment it returns today, and one that closes the tab after 10 s and comes back 3 minutes later can rate at once. "Once the game has been open for three minutes" reads as three minutes of play. The prompt's own rule is "If anything here contradicts the Overview, fix the Overview."
- **Fix shape:** "The stars unlock three minutes after this browser first opened the game. The page counts down…"

---

## Reviewer's view
I would build this as written except for **F1**. The server side is sound: the gate fails closed, the SQL and arithmetic check out, the removal transaction covers all three paths, and the routing and same-origin tests hold. But the script has no way to recover from a single failed open, and the phase's own deploy step causes one. That is a stuck page for whoever is mid-game during a redeploy. F2 (the hover preview won't work) and F3 (a flaky test) are cheap fixes the Author should fold. F4–F8 are wording and precision.
