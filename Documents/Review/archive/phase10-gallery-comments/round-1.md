---
slug: phase10-gallery-comments
round: 1
verdict: changes-requested
reviewer: claude-cowork
updated: 2026-11-24T15:05:00+09:00
blocking: 2
recommendations: 1
---

# Round 1: Phase 10, gallery comments

## What I could run, what I verified, what I could not

**Ran:** `device_bash` on the laptop's `games\orcarin\daedalus` folder (cat/sed/grep over `Deploy/gallery/` and the docs). No cargo, no Chrome, no Unity.

**Verified against the code (all hold):**
- `ratings.rs` `rater()` is private (line 24) and `gallery.rs` `days_ago` is private (line 31), so both `pub(crate)` changes are needed.
- `db.rs` `open_with` sets `foreign_keys(true)` (line 141); `users` rows are deleted with `DELETE FROM users` (`delete_user`, line 280), so `comments.user_id ON DELETE CASCADE` fires. `teacher::remove` tombstones every live game through `gallery::remove_game` before `delete_user` (teacher.rs 383–398), so "account removal reaches `tombstone_game`" is right.
- `the_ratings_migration_keeps_todays_games` runs `[..3]`; the new test's `[..4]` is the right analogue.
- Routing: `concrete()` maps `{id}` to `1`; `every_non_public_route_needs_a_session` expects 404 on Open for a made-up id, which both new Open POSTs give if the lookup runs before the `Result<Form>` is inspected (it does: the rejection is wrapped, so the handler body runs). `every_signed_in_page_shows_the_name_and_sign_out` only checks `text/html` answers, so the JSON GETs pass. The new Teacher GET with no session gets the layer's 303, as the test expects.
- `gallery_layer` refuses every non-GET without same-origin (guard.rs 54–60), so the two new POSTs are CSRF-guarded without new code, and it sets `Cache-Control: no-store` when a handler sets none, so the teacher's real-name JSON from the Open `GET /play/{id}/comments` is never cached by Cloudflare.
- SQL boundaries: with `created_at > now − gap`, a comment at 1000 refuses 1029 and accepts 1030 (Step 3 test); the gate `opened_at <= now − 180` matches test 2's 170 s / 180 s; NULL handling of `owner_id IS NOT ?user` and `user_id = ?user OR guest_hash = ?hash` is right for both writer kinds.
- 300 four-byte emoji: `as=guest&text=` (14 B) + 300 × 12 B = 3614 B < 4096. 300 Korean syllables: 2700 B.
- `unicode-normalization` is already in `Cargo.lock` (pulled in transitively), so the new dependency adds nothing to the tree.
- Changelog line 29: v0.31 + v0.32 deployed 14:38 KST with 0004 applied, so the "live is at 0004" correction is right.
- `.theater` has no height limit (gallery.css 113), so comments after `#frame` don't squeeze the 16:9 box.

**Could not check:** Chrome's focus behaviour (F1), the look, any test run. All taken from the doc, as the plan already asks for them.

---

## Blocking

### F1 (blocking, design): the game page inside the frame still takes focus when Unity starts
**Location:** Overview "Writing a comment never ... interrupts the game", Step 8 Focus, File Manifest ("`play.rs` does not change"), Step 16 "Must be absent: the game stealing focus while typing".
**Defect:** the plan guards only the parent page's `load` handler. The player page inside the iframe focuses the canvas on its own: `static/player.js` line 17 `started()` calls `canvas.focus()` when `createUnityInstance` resolves, and line 53 calls it at script start. On the slow line (ISS-002: a 15 s school load target, slower on a bad day) a player who opens the page, presses "댓글 ↓" and starts typing while the game loads can have focus pulled into the game when Unity starts, mid-word; the `?local=1` fallback reload runs `player.js` again and does it a second time. If Chrome lets a cross-origin iframe's `element.focus()` take focus from the top page without a user gesture, the keystrokes after that go to the game.
**I could not test Chrome's behaviour.** So either:
- measure it (Step 8's optional headless approach, with the iframe on a second origin: focus the textarea, then have the frame call `canvas.focus()` after 2 s, and check `document.activeElement` in the parent), and quote the result in the plan, or
- add the same rule on the inside: in `player.js`, `started()` focuses the canvas only when `document.hasFocus()` (the frame already has focus because the parent's start-up focus put it there; a player who moved to the textarea has taken it away). Keep the click and `window` focus listeners as they are. Add `player.js` to the File Manifest, keep the `?v=` fingerprint working (it follows the file's bytes), and add the "type while the game loads" case to Step 16 Do/Observe.

Either way, the plan's claim that the parent's load handler "covers the fallback reload too" isn't enough on its own, because the fallback's focus call comes from inside the frame.

### F2 (blocking, accuracy): the next nightly backup overwrites the rollback copy
**Location:** Step 16.3 ("Note the printed copy path") and 16.7 ("restore the step-3 copy").
**Defect:** backup copies are named by UTC date, `<to>/db/gallery-YYYY-MM-DD.db` (backup.rs 47, `utc_date`), and the nightly job runs at 03:30 KST, which is 18:30 UTC the **previous** day. A deploy any time after 09:00 KST shares its UTC date with that night's run. Example: deploy at 16:00 KST on 11-24 → copy `gallery-2026-11-24.db` (pre-0005). At 03:30 KST on 11-25 the job, now on the new binary, writes `gallery-2026-11-24.db` again from the migrated database and renames it over the old one. A rollback the next morning then restores a database at 0005, which the v0.32 `gallery.prev` refuses to open, so the gallery stays down. (Today's deploy will also overwrite the 14:38 copy from the v0.32 deploy, for the same reason.)
**Fix:** in 16.3, copy the printed file out of the rotating folder straight away, e.g. `cp <copy> "$HOME/Library/Application Support/daedalus-gallery-backups/pre-v0.35.db"`, and have 16.4 (rehearsal) and 16.7 (rollback) name that file. Record the path in the Changelog *Deployed* line. Worth one line in `backup.rs`'s restore note or platforms.md too, since every past deploy had the same window.

---

## Non-blocking

### F3 (non-blocking, accuracy): `comment_by_id` must return hidden rows
**Location:** Step 3 Reads, Step 6 delete step 1.
**Defect:** the delete handler's "an existing comment, else 404" uses `comment_by_id`. Test 8 expects "deleting again gets 410" and Step 5 says the teacher may erase hidden rows from 최근 댓글. Both need `comment_by_id` to find hidden rows (and replies of a hidden parent). The page reads are described as visible-only, so an implementer copying them gives 404 in both cases. State that `comment_by_id` includes hidden rows and carries `hidden`.

### F4 (non-blocking, accuracy): deleting a reply under a hidden parent answers 200, not 410
**Location:** Step 6 delete step 4.
**Defect:** `hide_comment` checks only the row's own `hidden_at IS NULL`. A reply whose parent is already hidden still has `hidden_at` NULL, so the UPDATE succeeds and answers 200, though the plan says "its own or its parent's" hidden gives 410. Either check the `hidden` flag from F3's `comment_by_id` before calling `hide_comment`, or drop "or its parent's" from the text. Small, but the test as written ("Deleting again gets 410") won't catch it.

### F5 (non-blocking, accuracy): 최근 댓글 keeps showing replies of an expired hidden parent
**Location:** Step 3 `recent_comments` ("Hidden rows are included only while `hidden_at >= hidden_since`"), Notes "Open risks" ("hidden rows can outlive 30 days ... but they are not listed").
**Defect:** the filter is on the row's own `hidden_at`. A reply under a parent hidden 31+ days ago has its own `hidden_at` NULL, so it is still listed (marked 지움 via the parent) until the next server start purges the parent. Filter on `COALESCE(c.hidden_at, p.hidden_at)` and add the case to test 9.

### F6 (non-blocking, design): the 429 gap answer doesn't count the button down
**Location:** Step 8 click handling.
**Defect:** the gap is per writer across the gallery. A guest who comments on game A and then opens game B within 30 s gets 429 with `wait`, but the script only counts down on 200 and restarts the gate on 409; on 429 it just shows the message, with the button live. Treat a 429 that carries `wait` like the 200 countdown.

### F7 (non-blocking, accuracy): `COMMENT_GATE` is minutes only
**Location:** Step 4 Messages, Step 6.6.
**Defect:** "{n}분 후에 댓글을 남길 수 있어요" is the 409 message for any gate remainder. At 40 s left it says "1분 후" while the button beside it says "40초 후". Fill the message through `comment_wait` (as the button does), or give the gate a seconds form.

### F8 (non-blocking, design): the client counts the raw draft, the server counts the cleaned text
**Location:** Step 8 counter, Step 2.
**Defect:** the counter and the local over-length check use the raw value, but the server collapses whitespace and blank lines first. A 295-character comment with a few extra spaces or blank lines is blocked on the page though the server would accept it. Low impact; either collapse whitespace runs before counting in the script or let the server decide when the raw count is over. Also give the two `더 보기` fetches a `catch` (a teacher whose session ended gets the 303 to `/login`, and `r.json()` throws).

---

## Recommendation

### R1 (recommendation, design): real names on a projected play page
Settled item 8 puts real names on `/play` for the teacher. If the teacher ever shows a game on the classroom projector while signed in, every student's real name appears next to their comment, in front of the class the nicknames were for. If the operator hasn't weighed that, one option is to show real names only in 최근 댓글 (the teacher page) and nicknames plus a 선생님-only delete on `/play`. If it was weighed, close this.

---

## Reviewer's view
The plan is careful and its citations hold: the SQL, the routing claims, the cascades and the cache and same-origin guards all check out against the code. I'd build it once two things are fixed: the focus rule has to cover the player page inside the frame, not only the parent page (F1), and the rollback copy has to be moved somewhere the nightly backup can't overwrite (F2). F3–F8 are small wording or edge fixes.
