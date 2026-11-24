# Handshake log: phase10-gallery-comments

## Opening (2026-11-24T14:55:50+09:00)

Doc: `Documents/Phases/phase10-gallery-comments.md`. Phase 10 adds comments to the class gallery (`Deploy/gallery/`, Rust Axum + SQLite): anyone (guests as 손님 via the rater cookie hash) comments under a game on its play page; only the game's owner replies, one level; guests wait the 3-minute rating gate; everyone else 30 s between comments (owner replies exempt); at most 20 guest comments per 10 minutes gallery-wide; a writer's delete hides (teacher sees 지움 for 30 days), a teacher's erases; the teacher sees real names and a 최근 댓글 list. Migration 0005, fetch-only UI so the game never reloads, a focus rule so the game never steals the comment box. Ends with the Mac Mini deploy (with the undeployed v0.34) and an operator check. The design is already in GDD v0.34 (systems.md § Comments, decisions.md 2026-11-24 rows). Nothing is built yet.

## Round 1 dispositions (2026-11-24T15:03:43+09:00)

- **F1 accepted.** Verified: `G/static/player.js` calls `focus()` in `started()` (line 17) and at script start (line 53); the listener on `window` focus also calls it. Fix chosen: the inside rule (focus the canvas only when `document.hasFocus()`), not a Chrome measurement, because it holds whatever Chrome does and costs two guards. Step 8 gains the rule, the manifest gains `player.js`, Step 16 Do/Observe gains "type while the game loads".
- **F2 accepted.** Verified: `backup.rs:46` names copies `gallery-{utc_date}.db`; the backup plist runs at 03:30 (local). Step 16.3 now copies the printed file to `daedalus-gallery-backups/pre-v0.35.db`; 16.4 and 16.7 name it; the Deployed line records it; Step 13 adds the rule to platforms.md and `backup.rs`'s restore note (manifest row added). Checked: the rotation in `backup.rs:57` only touches `gallery-*.db`, so `pre-v0.35.db` is never pruned.
- **F3 accepted.** `comment_by_id` includes hidden rows and carries `hidden` and `parent_hidden`.
- **F4 accepted.** Delete step 4 checks `hidden`/`parent_hidden` first (410); test 8 adds the reply-under-hidden-parent case.
- **F5 accepted.** `recent_comments` filters on `COALESCE(c.hidden_at, p.hidden_at)`; test 9 adds the reply case.
- **F6 accepted.** A 429 with `wait` counts the button down; `COMMENT_BUSY` (no `wait`) only shows the message.
- **F7 accepted.** `COMMENT_GATE` split into `COMMENT_GATE_MINUTES` / `COMMENT_GATE_SECONDS`, picked by `comment_wait`'s rule; both references updated.
- **F8 accepted.** Remedy chosen: the server decides length (the counter is a guide; only an empty draft is refused locally), since collapsing whitespace in the script would duplicate the server's cleaner. Both 더 보기 fetches catch a failed or non-JSON answer.
- **R1 recorded** beside settled item 8 (Overview decisions), with the Author's agreeing view; the plan builds the settled version until the operator decides.

## Round 2 / close dispositions (2026-11-24T15:08:42+09:00)

Round 2: `verdict: approved`, 0 blocking.

- **F9 accepted.** Verified: with the F8 remedy the page sends any length, and a body over the route's 4096-byte limit is a 413 `FormRejection` that Step 6 answered with `COMMENT_FAILED`. Fold: Step 6 maps a 413 rejection to `COMMENT_TOO_LONG`; test 6's 5000-byte case expects 413 with `COMMENT_TOO_LONG`.
- **R1 decided by the operator** (2026-11-24, in session): go with the Reviewer's recommendation. Settled item 8 rewritten: real names only in 최근 댓글 on the teacher page; on `/play` the teacher sees nicknames and 손님 plus a delete button on every item. Folded into Overview item 8, Names, Step 5 writer name, the tests/teacher.rs row and test, the look state `play-comments-teacher`, Step 16 Observe and Must be absent, the privacy bullet and the checklist. GDD systems.md § Teacher and a decisions.md row updated in the same session.
- Close fold re-verified: every "real name" line above `## Review round 1` now says teacher page only; `teacher_view_name` is used only by 최근 댓글.

Nothing was built. The approved document is a proposal; implementation is separate work.
