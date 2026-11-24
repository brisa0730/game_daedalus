# Phase 9: Gallery ratings

## Overview

**Where things stand:** Classmates can play every game in the class gallery, but they have no way to say whether they liked one. A student can't tell how their game is going over.

**What this phase changes:**
- Anyone playing a game can give it one to five stars and change them later. Nobody has to sign in, and nobody, not even the teacher, can see who gave what.
- The stars unlock three minutes after this browser first opened the game. The page counts down and the stars appear on their own.
- When a game has three ratings, its average and the number of people who rated it show on its card and on its play page.
- Giving stars never interrupts the game. The game keeps the keyboard.
- Students can't rate their own games, and removing a game removes its ratings.

**How you'll know it worked:** Rate a game from three browsers, each after three minutes, and "★ 4.3 (3명)" appears on its card.

**Not in this phase:** sorting games by rating, comments, and any teacher tool to view or reset ratings.

---

## Source
- **GDD sections:** `systems.md` § Share (Play, removal), `platforms.md` (cookies, Access, the data folder and rollback), `architecture.md` (the gallery's responsibilities), `Glossary.md`. **The operator's decisions 1–11 (repeated below and in `Notes to GDD`) are not in the GDD yet. This prompt holds them until Step 12.**
- **GDD version:** v0.30. This phase lands as v0.31.
- **Depends on:** Phase 5 (`gallery-server`), the public gallery (v0.20: `Access::Open`, the route table), the play page layout (v0.19/v0.30: the frame height is `100dvh − 220px`, measured at 1366 × 768).

**Paths:** `G/` means `Deploy/gallery/`. All the work is in `G/`. Nothing changes in the Unity package or the student projects, and there is no package release.

**Settled decisions (operator):**
1. Anyone may rate a game 1–5 stars, signed in or not. Each browser gets one rating, keyed by a new anonymous cookie `__Host-gallery_rater` (HttpOnly, Secure, SameSite=Lax, Path=/, about 1 year). The server stores only its SHA-256, the way it stores sessions. The known-browser cookie is never reused, because `known_browsers` links it to user ids. There are no account-based ratings and no IP limits. No rating row and no log line carries a user id, IP or login. Duplicates from incognito windows or cleared cookies are accepted. A shared laptop shares one rating, and the cookie is never rotated at sign-in or sign-out.
2. A new rating from the same browser replaces the old one, at any time.
3. The server enforces a 3-minute gate: a rating counts only ≥ 180 s after this browser first opened this game's play page. The open is recorded by a same-origin POST that the page's script sends, never by the GET. It keeps the earliest open, so a reload never restarts the clock. The page counts down ("3분 후에 별점을 줄 수 있어요") and turns into stars without a reload. The gate length is a `Config` value.
4. Open records older than 30 days are pruned when that browser never rated that game.
5. The average shows to one decimal, with the count ("★ 4.3 (12명)"), on every card (내 게임 included) and in the play page's title row. It stays hidden below 3 ratings. Rounding is half-up and pinned by a test: 4.25 → 4.3. Cards are the same height with or without an average.
6. Saving a rating doesn't disturb the game. It uses fetch with no navigation, focus goes back to the frame, and the page has no control whose value changes with the arrow keys or Space. After a click the chosen stars stay lit and the page shows "저장됐어요". While the average is hidden, it also shows "별점이 3개 모이면 평균이 보여요". The prompt reads "재미있었나요?". A returning browser sees its own current rating.
7. A signed-in owner can't rate their own game. Their page shows the average (if any) and "내 게임에는 별점을 줄 수 없어요", and the server refuses their POSTs. As a guest in another browser they can rate it (accepted).
8. Ratings survive game updates. There is no reset.
9. Removing a game deletes its ratings and open records in the same transaction as the tombstone. That one path covers owner removal, teacher removal and account removal.
10. Out of scope: teacher or operator rating tools, sorting or ranking by rating, comments, reset.
11. New migration `0004_ratings.sql`.

## File Manifest

| Action | File | Purpose |
|--------|------|---------|
| NEW | `G/migrations/0004_ratings.sql` | `game_opens` and `ratings` tables |
| MODIFY | `G/src/config.rs` | `rating_gate_secs`, `ratings_shown_at`, `open_keep_secs`, `rater_cookie_secs` |
| MODIFY | `G/src/db.rs` | Rating queries, the rating totals on `Game`, the removal transaction, prune, migration test |
| NEW | `G/src/ratings.rs` | The rater cookie, the open and rate handlers, the average, the play page's rating box |
| MODIFY | `G/src/lib.rs` | `pub mod ratings` |
| MODIFY | `G/src/session.rs` | `is_token` becomes `pub(crate)` |
| MODIFY | `G/src/app.rs` | Two Open POST routes |
| MODIFY | `G/src/gallery.rs` | The average on cards, the rating box and average in the play page's title row |
| MODIFY | `G/src/strings.rs` | `// Ratings` section |
| MODIFY | `G/src/main.rs` | Prune old open records at start |
| MODIFY | `G/static/gallery.js` | Record the open, the countdown, the stars, focus back to the game |
| MODIFY | `G/static/gallery.css` | The rating line on cards, the stars in the play bar |
| NEW | `G/tests/ratings.rs` | Gate, cookie, anonymity, owner, average, removal, update |
| MODIFY | `G/tests/support/mod.rs` | `open_rating`, `rate`, `backdate_open`, `count_rows` helpers |
| MODIFY | `G/tests/look.rs` | Rating states for the screenshots, the script's new wires |
| MODIFY | `Documents/GDD/systems.md`, `platforms.md`, `architecture.md`, `Glossary.md`, `GDD.md`; `Documents/Operations/Changelog.md` | Step 12 |

---

## Steps

### Step 1: Settings and the migration
- **MODIFY `G/src/config.rs`:** add, with doc comments, `rating_gate_secs: u64 = 180`, `ratings_shown_at: i64 = 3`, `open_keep_secs: u64 = 30 * 24 * 3600` and `rater_cookie_secs: u64 = 365 * 24 * 3600`. `unix_now()` stays the only clock read.
- **NEW `G/migrations/0004_ratings.sql`** (comment each table in the style of 0001):
  ```sql
  -- When a browser (the rater cookie's SHA-256; nothing else about it) first opened a game's play page (v0.31).
  CREATE TABLE game_opens (
      game_id TEXT NOT NULL REFERENCES games(id),
      rater_hash BLOB NOT NULL,
      opened_at INTEGER NOT NULL,
      PRIMARY KEY (game_id, rater_hash)
  );
  -- One rating per browser per game; a new one replaces it. No user id, ever.
  CREATE TABLE ratings (
      game_id TEXT NOT NULL REFERENCES games(id),
      rater_hash BLOB NOT NULL,
      stars INTEGER NOT NULL CHECK (stars BETWEEN 1 AND 5),
      rated_at INTEGER NOT NULL,
      PRIMARY KEY (game_id, rater_hash)
  );
  ```
  Games are tombstoned, never deleted, so no `ON DELETE` clause is needed (Step 3 deletes the rows explicitly).
- **Tests:** a migration test in `db.rs` following `the_real_name_migration_keeps_todays_names`: build a DB with migrations 0001–0003 and one game, then open it with `Db::open_existing`. The game is still live with 0 ratings.
- **Verify:** `[automated]` `cargo test db::`.

### Step 2: Rating queries (`G/src/db.rs`)
- **MODIFY `Game`:** add `ratings: i64` and `stars_total: i64`. `GAME_COLUMNS` gets two correlated subqueries before `FROM`: `(SELECT COUNT(*) FROM ratings r WHERE r.game_id = g.id) AS ratings` and `(SELECT COALESCE(SUM(r.stars), 0) FROM ratings r WHERE r.game_id = g.id) AS stars_total`. Every card and the play page then get them with no second query. Update `from_row`.
- **Add** (all SQL stays in this file):
  - `record_open(game_id, rater_hash, now) -> Result<Option<i64>>`: `INSERT OR IGNORE INTO game_opens SELECT ?, ?, ? WHERE EXISTS (SELECT 1 FROM games WHERE id = ? AND removed_at IS NULL)`, then return this browser's `opened_at` (the earliest), or `None` if the game isn't live. Because the insert is conditional, an open that races a removal can't leave a row behind.
  - `rating_state(game_id, rater_hash) -> Result<(Option<i64>, Option<i64>)>`: this browser's `opened_at` and `stars`. Read-only, for the GET.
  - `rate(game_id, rater_hash, stars, opened_by, now) -> Result<bool>`: one statement, `INSERT INTO ratings (game_id, rater_hash, stars, rated_at) SELECT ?, ?, ?, ? WHERE EXISTS (game_opens row with opened_at <= opened_by) AND EXISTS (live game) ON CONFLICT (game_id, rater_hash) DO UPDATE SET stars = excluded.stars, rated_at = excluded.rated_at`. SQLite needs the `WHERE` on an `INSERT … SELECT` before `ON CONFLICT`, and here it already has one. Returns whether a row was written.
  - `prune_opens(before) -> Result<u64>`: `DELETE FROM game_opens WHERE opened_at < ? AND NOT EXISTS (SELECT 1 FROM ratings r WHERE r.game_id = game_opens.game_id AND r.rater_hash = game_opens.rater_hash)`. Returns the number of rows deleted.
- **Tests (db unit):** `record_open` keeps the earliest time and refuses a removed game. `rate` refuses with no open. The exact gate boundary is pinned here with explicit times: with `opened_at = 1000`, `opened_by = 999` is refused and `opened_by = 1000` (i.e. `now = 1180` for a 180 s gate) is accepted. A second `rate` replaces the stars (count stays 1). `prune_opens` deletes an old unrated open and keeps an old rated open and a recent open.
- **Verify:** `[automated]` `cargo test db::`.

### Step 3: Removal deletes ratings, in one transaction
- **MODIFY `db.tombstone_game`:** run in a transaction: the existing `UPDATE games SET removed_at …`, then `DELETE FROM ratings WHERE game_id = ?` and `DELETE FROM game_opens WHERE game_id = ?`, then commit. Update its doc comment and the `db.rs` module line ("users, sessions, known browsers, games and ratings"). Owner removal (`gallery::remove`), teacher removal and account removal (`teacher::remove`) all go through `gallery::remove_game`, which calls `tombstone_game`, so they need no change.
- **Tests:** in `tests/ratings.rs` (Step 9), one test per path: owner, teacher, and the teacher removing the account. Each leaves `count_rows(ratings | game_opens WHERE game_id = id) == 0`.
- **Verify:** `[automated]`.

### Step 4: Words (`G/src/strings.rs`)
- Add a `// Ratings` section:
  - `RATE_PROMPT = "재미있었나요?"`, `RATE_SAVED = "저장됐어요"`, `RATE_OWN = "내 게임에는 별점을 줄 수 없어요"`, `RATE_FAILED = "저장하지 못했어요. 다시 눌러 보세요."` (my wording; see Notes).
  - `RATE_WAIT_MINUTES = "{n}분 후에 별점을 줄 수 있어요"` and `RATE_WAIT_SECONDS = "{n}초 후에 별점을 줄 수 있어요"`. The script fills `{n}` from these same strings (data attributes). `pub fn rate_wait(secs: u64) -> String` uses minutes, rounded up, while ≥ 60 s remain, and seconds below that: 180 → 3분, 61 → 2분, 60 → 1분, 59 → 59초.
  - `pub fn average_after(n: i64)` returns `"별점이 {n}개 모이면 평균이 보여요"`, `pub fn rating_summary(tenths: i64, count: i64)` returns `"★ {}.{} ({}명)"`, and `pub fn star_label(k)` returns `"별 {k}개"` (the buttons' accessible names).
- **Tests:** unit test of `rate_wait` at 180/61/60/59/1.
- **Verify:** `[automated]`.

### Step 5: The rater cookie and the two POSTs (`G/src/ratings.rs`, `app.rs`)
- **MODIFY `session.rs`:** `is_token` becomes `pub(crate)`. Nothing else changes.
- **NEW `ratings.rs`:**
  - `pub const RATER_COOKIE = "__Host-gallery_rater"` and `fn rater(headers) -> Option<String>` (well-formed tokens only, via `session::cookie` and `is_token`).
  - `pub fn average_tenths(total, count) -> i64 = (total * 20 + count) / (2 * count)`. This is integer half-up, with no float. `pub fn summary(game, config) -> Option<String>` returns `Some` only when `game.ratings >= config.ratings_shown_at`.
  - `pub fn wait_secs(opened_at: Option<i64>, now, config) -> u64`: the full gate if there is no open, otherwise `max(0, opened_at + gate − now)`.
  - **`POST /play/{id}/open`** (no body; handler args `State`, `Option<Extension<CurrentUser>>`, `Path`, `HeaderMap`). The order is: valid id and live game, otherwise JSON 404. A signed-in owner gets 403 and nothing is recorded. Then take the rater token, or `session::new_token()` if it is missing or malformed. Then `record_open` (`None`: 404). Answer `200 {"ok":true,"wait":N,"mine":null|1..5}`. Send `Set-Cookie: __Host-gallery_rater=<t>; HttpOnly; Secure; SameSite=Lax; Path=/; Max-Age=<rater_cookie_secs>` only when the token was new.
  - **`POST /play/{id}/rate`** (body `stars=N`, form-encoded). The extractors are `State`, `Option<Extension<CurrentUser>>`, `Path`, `HeaderMap`, and **`Bytes` last**. `Bytes` never rejects a small body, so the game check runs first and the routing test's `text=x` on game `1` gets 404, not 422. The order is: game live, else 404. Owner gets 403 `{"ok":false,"message":RATE_OWN}`. Then parse the body exactly (`stars=1` … `stars=5`, anything else 400). No rater cookie, or `db.rate(…, opened_by = now − gate)` returns false: 409 `{"ok":false,"wait":N}`, with N from `rating_state`. Otherwise re-read `live_game` and answer `200 {"ok":true,"mine":k,"summary":"★ 4.3 (3명)"|null,"hint":average_after(n)|null}` (`hint` only while the summary is null). DB errors give 500 `{"ok":false,"message":RATE_FAILED}`. Use `upload::json_answer` for every answer.
  - Logging: no line on success. Failures log only the game id and the error. Never the rater hash, a login or an IP.
- **MODIFY `app.rs`:** `route(POST, "/play/{id}/open", Open, post(ratings::open))` and `route(POST, "/play/{id}/rate", Open, post(ratings::rate).layer(DefaultBodyLimit::max(64)))`. The same-origin guard already covers both, and the routing tests iterate the table.
- **Tests:** Step 9. Also `average_tenths` unit tests: (17, 4) → 43 (4.25 → **4.3**), (13, 3) → 43, (14, 3) → 47, (3, 3) → 10, (15, 3) → 50. `rating_summary(43, 12) == "★ 4.3 (12명)"`, `(10, 3)` gives `"★ 1.0 (3명)"`.
- **Verify:** `[automated]` `cargo test` (the existing `tests/routing.rs` passes unchanged).

### Step 6: The play page (`G/src/gallery.rs` → `ratings::rate_box`)
- **Title row** (`.play-title`): after the owner, `span#rating-avg.avg` holds the summary. It is always rendered, with `hidden` when there is no summary, so the script can fill it. When the summary is hidden and this browser has already rated, the same span shows `average_after(n)` in the `.hint` style instead. That puts the second note in the title row, not in the bar, to keep the bar on one line at 1366 × 768.
- **Rating box** (`ratings::rate_box(&state, &game, user, &headers, now) -> Markup`), between `.play-title` and `#fullscreen`. The GET only reads, via `rating_state`, and never sets a cookie:
  - Signed-in owner: `p.rate-own { RATE_OWN }`, with no `#rate` id, so the script does nothing.
  - Everyone else: `div#rate.rate role="group" aria-label=RATE_PROMPT` with data attributes `data-open`, `data-rate` (the two paths), `data-wait` (from `wait_secs`), `data-mine` (0 when none), `data-msg-wait-min`, `data-msg-wait-sec`, `data-msg-saved`, `data-msg-failed`. It contains `span#rate-wait` (the `rate_wait` text, `hidden` when the wait is 0), `span#rate-stars` (`hidden` while waiting; the label `RATE_PROMPT`, then `span#rate-buttons` holding only the five `button.star type="button" data-stars=k aria-label=star_label(k) aria-pressed`, each a 7×7 `crispEdges` SVG star in `currentColor` like the existing icons; class `on` for k ≤ mine; `aria-pressed="true"` on star `mine` only, `false` on the others, so a screen reader hears one choice) and `span#rate-note role="status"` (empty).
  - Buttons only: no radio inputs or ranges, because those change value with the arrow keys.
- `play_view` takes `HeaderMap` for the rater cookie.
- **Tests:** Step 9 (returning browser: exactly one `aria-pressed="true"`, on star `mine`, `mine` stars with `on`, and no `#rate-wait` text; owner page has `RATE_OWN` and no `id="rate"`).
- **Verify:** `[automated]`.

### Step 7: The script (`G/static/gallery.js`)
- In the existing `if (game)` block, if `#rate` exists:
  - On start, the countdown begins at once from the rendered `data-wait` (deadline `Date.now() + data-wait * 1000`), and the script sends `fetch(d.open, {method: "POST"})`. On `ok`, it resets the deadline from the answer's `wait` and lights `mine`. On failure, the countdown keeps running from `data-wait`, so the stars still appear and the first click recovers (next bullet).
  - Every second: `remaining = ceil((deadline − now) / 1000)`. While it is > 0, `#rate-wait` shows the template with `{n}`, in minutes rounded up while ≥ 60, else seconds (the same rule as `rate_wait`). At 0, hide `#rate-wait`, show `#rate-stars` and stop the timer.
  - Star click: call `game.focus()` **first, synchronously**, so the button never keeps focus. Then `fetch(d.rate, {method: "POST", headers: {"Content-Type": "application/x-www-form-urlencoded"}, body: "stars=" + k})`. On 200: light 1…k, set `aria-pressed="true"` on star k only, put `msgSaved` in the note, and set `#rating-avg` to `summary` or `hint` (unhide it, or set `hidden` when both are null). On 409: **re-send the open POST** (idempotent; it keeps the earliest time and is the only thing that records an open), then restart the countdown from *its* `wait`. If that open fails too, show `msgFailed` and keep the stars showing, so the next click tries again. Any other answer, or a network error: `msgFailed`, with the previous stars kept. `/rate` never records an open. (A browser that refuses cookies gets a fresh token on every open, so it keeps counting down and can never rate. That is accepted: it fails closed.)
  - The script never reloads or navigates, and it reads no message from the game.
- Update the file's header comment (it lists what the script does).
- **Tests:** `look.rs` asserts the new ids and data attributes (Step 10). Behaviour is checked in the operator check (Step 11).
- **Verify:** `[manual]` Step 11.

### Step 8: Cards and styles
- **MODIFY `card()`:** inside the card link, after the title and owner, **always** render `span.rating`: `summary` or empty. Mine and other people's cards both get it, so every card has the same lines and the same height.
- **MODIFY `gallery.css`:**
  - `.card .rating` gets a fixed `min-height` of one line and the muted colour. Its padding takes over the bottom padding of the line above. Remove `.card .title:last-child` if this leaves it unused.
  - `.rate` is an inline-flex row with `gap`. `.star` is a ≥ 28 px button, unlit `var(--muted)`, `.on` `var(--accent)`, with visible `:focus-visible`. The hover preview is scoped to the buttons' wrapper, so hovering the label lights nothing: `#rate-buttons:hover .star` lit (specificity 1,2,0), `#rate-buttons .star:hover ~ .star` unlit (1,3,0, so it wins over the lit rule and over `.star.on`).
  - `.avg` and `.hint` use the owner line's style.
  - No inline styles (CSP). The `hidden` attribute does the showing and hiding. Any rule that gives `#rate-wait`, `#rate-stars`, `#rate-buttons` or `#rating-avg` a `display` also gets a `[hidden] { display: none; }` guard, as `progress[hidden]` already has (gallery.css:146).
- **Verify:** `[automated]` look test (no inline style) and `[manual]` screenshots (Step 10).

### Step 9: Behaviour tests (`G/tests/ratings.rs`, `support/mod.rs`)
- **Support helpers:** `open_rating(app, id, cookie) -> (Answer, rater)` (POST open, returns the `name=value` rater cookie from the answer or the input), `backdate_open(app, id, secs)` and `count_rows(app, sql)` (raw sqlx pool on the DB file, as in `look.rs` `backdate`), and `rate(app, id, cookie, stars) -> Answer`.
- **Tests (each a `#[tokio::test]`):**
  1. The first open sets the cookie with every attribute and `Max-Age=31536000`, and `wait` is in 179..=180. Opening again with the cookie sends no `Set-Cookie`. After `backdate_open(100)`, a reopen gives `wait` ≤ 80 (the clock wasn't reset).
  2. Gate: rating right away gives 409 with `wait`. At 170 s back, still 409 (a margin: the exact boundary is pinned in Step 2's db tests, because the clock can tick a second between the backdate and the handler). At 180 s back, 200. With no rater cookie, 409.
  3. With `TestApp::with(|c| c.rating_gate_secs = 0)`, re-rating replaces the stars: one row, and `mine` follows.
  4. Bad bodies `stars=0`, `stars=6`, `stars=x`, empty and `stars=4&x` each give 400, and a body over 64 bytes gives 413.
  5. Owner: open and rate both give 403, and nothing is written. The owner's page shows `RATE_OWN` and no `id="rate"`. The same browser's rater cookie with no session can rate (200).
  6. Average: 2 ratings show no summary on the play page or either list section. A 3rd shows `rating_summary` on the play page, the card in 모든 게임 and the owner's 내 게임 card. Totals 17/4 read `★ 4.3 (4명)`.
  7. Anonymity: `PRAGMA table_info(ratings)` and `table_info(game_opens)` columns are exactly `game_id, rater_hash, stars, rated_at` and `game_id, rater_hash, opened_at`. The stored hash is not the cookie value. Sign-in and sign-out answers set no rater cookie.
  8. Removal ×3 (Step 3). A removed game's open and rate give 404.
  9. Ratings survive an update (upload to the same game: count and average unchanged).
  10. A returning browser's GET renders its stars lit (one `aria-pressed="true"`, on star `mine`) and no countdown. The GET writes nothing to `game_opens` or `ratings` and sets no rater cookie.
- **Verify:** `[automated]` `cargo test --test ratings`.

### Step 10: Look check
- **MODIFY `tests/look.rs`:** give `old` 3 ratings (average shown) and `traps` 1. Keep the default gate and use `backdate_open`. Add pages:
  - `play-rate-wait` (lee on `old`, fresh browser: countdown)
  - `play-rate-ready` (public on `traps`, open backdated)
  - `play-rated-hint` (public, has rated `traps`: stars lit and the hint in the title row)
  - `play-rated-avg` (public, has rated `old`: average shown)
  - `play-own` (kim on `old`: `RATE_OWN` and the average)

  The existing `play` page (lee on `traps`) becomes an owner page. `list`, `list-mine` and `list-public` now carry cards with and without an average.
- Assert the wires on the non-owner play pages `play-public`, `play-rate-wait`, `play-rate-ready`, `play-rated-hint` and `play-rated-avg`; on the owner pages `play` and `play-own`, assert `RATE_OWN` and no `id="rate"`. The non-owner wires are the ids `rate`, `rate-wait`, `rate-stars`, `rate-buttons`, `rate-note`, `rating-avg`, and the data attributes `open`, `rate`, `wait`, `mine`, `msg-wait-min`, `msg-wait-sec`, `msg-saved`, `msg-failed`. Five `class="star` buttons. No inline style.
- **Run the look check** per CLAUDE.md: `cargo test --test look`, serve `TestResults/gallery-look/` on localhost, and screenshot every list and play page at 1280 × 900 and 390 × 844, plus the play pages at 1366 × 768. Check:
  - no horizontal overflow at 390 px
  - Galmuri loaded
  - the frame is 16:9
  - **at 1366 × 768 the whole frame is in view for every rating state** (the play bar stays one row, inside the 220 px budget)
  - cards in a row are the same height with and without an average
  - the ★ glyph renders (Galmuri or a fallback that looks right)

  Look at every screenshot. The script's fetch fails on the static server, so each page shows its server-rendered state. That is intended.
- **Verify:** `[manual]` screenshots inspected.

### Step 11: Pruning at start, deploy, operator check
- **MODIFY `main.rs` `serve`:** after `prune_sessions`, `db.prune_opens(now − open_keep_secs)`. Log the count (`tracing::info!(pruned, "old open records pruned")`). A failure is logged and serving continues: unlike the session prune, it guards nothing. Why at start: every deploy and every Mac restart runs it. The rows are tiny, so growth between restarts is harmless. The backup job is a separate read-and-copy process, and a backup that deletes rows would surprise anyone restoring from it.
- **Deploy** (CLAUDE.md redeploy; migration 0004 makes the old binary refuse the DB afterwards):
  1. On the Mac Mini: `git pull`.
  2. With the **old** binary, `gallery backup --data "$HOME/Library/Application Support/daedalus-gallery" --to "$HOME/Library/Application Support/daedalus-gallery-backups"`, and note the printed copy.
  3. Copy the old `bin/gallery` to `bin/gallery.prev`.
  4. `cargo build --release`, `install -m 0755`, `launchctl kickstart -k …gallery`.
  5. Check `gallery.log` for `gallery listening` and the prune line.

  Rollback: stop the job, put `gallery.prev` back, and restore the step-2 copy per `backup.rs`. Everything since the deploy is lost (ratings, uploads and updates, removals, account, nickname and password changes; an upload's version folder left without a row is swept at start), so roll back soon or not at all.
- **Operator check (live gallery, documented; report it, don't block review):**
  - **Do:** In Chrome A (signed out), open a game, play with the keyboard and watch the countdown. Reload once at about 1 minute. In Chrome B (incognito), open the same game. In A after 3 minutes, click 4 stars while the game is running, then press the game's keys. In B, click 5. Sign in as the owner in a third browser and open the game.
  - **Observe:**
    - The countdown keeps going after the reload, and the stars appear without a reload.
    - A shows "저장됐어요" and the hint, and the game keeps responding to the arrow keys and Space with no page jump.
    - After a third rating (another incognito window), "★ x.x (3명)" appears on the card and the title row.
    - The owner sees "내 게임에는 별점을 줄 수 없어요".
  - **Must be absent:** a page reload or game restart on clicking, a star moving with the arrow keys, an average below 3 ratings.

### Step 12: Docs
- `systems.md` § Share: Play (stars, gate, average on cards and the title row, owner), removal deletes ratings. `platforms.md`: the rater cookie in the cookie list, Open now includes two POSTs, migration 0004 and the rollback note, pruning at start. `architecture.md`: the gallery also keeps anonymous ratings. `Glossary.md` (Draft): **Rating**, **Rater cookie**, **Rating gate**. `GDD.md` version line to v0.31. Changelog entry v0.31. Commit `v0.31: Phase 9 — gallery ratings` (attribution per the harness).
- **Verify:** `[grep]` `__Host-gallery_rater` appears in `platforms.md` and `ratings.rs`.

### Step 13: Run Tests
- In `G/`: `cargo test` and `cargo clippy --all-targets -- -D warnings`. Fix until both are green. Do not proceed to review until they pass.

### Step 14 (Final): Code Review
- Run `.claude/agents/workflow/code-review.md` on all changed files. Pay special attention to: the gate failing closed (no cookie, no open, an open that is too recent are all refused), nothing personal in rows or logs, the removal transaction, the rule that the GET writes nothing to `game_opens` or `ratings` and sets no rater cookie, and the script's recovery from a failed open. Fix HIGH/MED findings and report the result.

---

## Completion Criteria
- [ ] `cargo test` (routing tests unchanged and green) and `cargo clippy --all-targets -- -D warnings` pass.
- [ ] A rating is refused before 180 s after this browser's first open and accepted at 180 s. Reopening never resets the clock. Only the open POST records an open; the GET writes nothing to `game_opens` or `ratings` and sets no rater cookie. A failed open recovers on the next star click.
- [ ] The rater cookie is `__Host-`, HttpOnly, Secure, SameSite=Lax, 1 year, and only its hash is stored. The rating tables have no user column, and no log line names a rater.
- [ ] Re-rating replaces the old rating. The owner is refused on the page and on the server.
- [ ] The average shows from 3 ratings, half-up to one decimal (4.25 → 4.3), on cards (both sections) and in the title row. Cards keep equal heights.
- [ ] Owner, teacher and account removal each delete the game's ratings and opens in the tombstone's transaction. Ratings survive updates.
- [ ] Old unrated opens are pruned at start (logged count).
- [ ] Look check done: every rating state screenshotted at 1280 × 900, 390 × 844 and 1366 × 768. No overflow at 390 px, the font loaded, the frame is 16:9 and fully in view at 1366 × 768.
- [ ] Deployed after a pre-deploy backup, and the operator check is reported.

---

## Notes to GDD
- Decisions 1–11 above go into `systems.md` § Share and `platforms.md` as written. Rounding is half-up in integer arithmetic.
- **Implementation choices not in the decisions:**
  - The two routes are `POST /play/{id}/open` and `POST /play/{id}/rate` (body `stars=N`, at most 64 bytes).
  - The cookie name is `__Host-gallery_rater`. It is set only when missing and never refreshed, so it lasts one year from the first visit.
  - The "3개 모이면" hint sits in the title row's average slot, so the play bar fits the 220 px budget.
  - The teacher can rate like anyone (decision 7 names only owners).
  - Pruning runs at server start only.
  - The failure message "저장하지 못했어요. 다시 눌러 보세요." is new wording.
- **Open risks (accepted):**
  - With 3–5 ratings, a classmate watching the average change can work out a friend's rating. This comes with any public average.
  - A script can mint many cookies, wait 3 minutes and stuff ratings. There is no IP limit, by decision.
  - Open records grow between restarts (tiny).
  - Removing a game during an open/rate race leaves nothing, because both inserts check the game is live.

**If anything here contradicts the Overview, fix the Overview.**

---

## Post-Phase Cleanup
After code review passes:
1. Move this prompt to `Documents/Phases/archive/`.
2. Update the phase status in CLAUDE.md if it lists one.
3. Write `.claude/memory/logs/phases/phase9-log.md`.
4. Update `MEMORY.md` current state.
5. `decisions.md`: ADR row for the anonymous rater cookie (not the known-browser cookie) and the server-side gate.
6. Deferred items: file any leftover in `Documents/Operations/Deferred.md` (Source = Phase 9) and delete any entry whose trigger fired.
7. Commit all phase work.

## Workflow
Execute steps sequentially. Proceed to the next step without waiting for user approval. Only stop if blocked. If blocked, stop and report: what step, what was attempted, what failed.

---

## Review round 1

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

---

## Review round 2

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
