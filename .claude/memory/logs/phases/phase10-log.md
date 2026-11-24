# Phase 10 log: `gallery-comments` (v0.35, 2026-11-24)

Prompt: [phase10-gallery-comments.md](../../../../Documents/Phases/archive/phase10-gallery-comments.md) (design panel; handshake approved round 2: [record](../../../../Documents/Review/archive/phase10-gallery-comments/)).

## Decisions on the way (operator, this session)
- Comments open to the public for now, guests included ("only students and their friends use it"); the operator closes them on request if something bad appears, no teacher switch. Guests show as 손님 (no typed name). Replies by the game's owner only. The owner can't delete others' comments.
- After the design panel: a gallery-wide guest cap (20 per 10 min); a writer's delete only hides (teacher sees 지움 for 30 days); a 선생님 badge; the owner's replies skip the 30 s gap.
- After handshake round 1 (R1): real names only on the teacher page's 최근 댓글, never on `/play`, so a game projected in class never shows them.

## What was built
1. `0005_comments.sql`: one table, `user_id` xor `guest_hash` by CHECK, parent and user FKs cascade.
2. `db.rs`: `insert_comment` checks live game, not the owner, the guest gate, the writer's gap and the guest cap in one `INSERT … SELECT … WHERE … RETURNING`; `comment_refusal` reads why afterwards (`None` if a race cleared). Replies likewise. `COMMENT_COLUMNS` computes `by_this_browser` in SQL, so the hash is never selected.
3. `comments.rs`: `Viewer` once per request; one `item` renderer for the play page and 최근 댓글 (`Place`); handlers take the form last as `Result<Form<_>, FormRejection>` so the game/comment lookup runs first (routing test 404s) and a 413 says 300자까지.
4. `gallery.js`: one gate clock shared by the stars and a guest's comment button; the load handler focuses the game only when nothing else has focus; `player.js` focuses the canvas only when its frame has focus. A second `function say` in the upload half would have replaced the comments' helper (same IIFE scope), hence `tell`.

## Verification
- Docker `rust:1` (1.100) from the laptop; the image lacks clippy until `rustup component add clippy`. 218 tests (+27), clippy clean with `-A clippy::result_large_err`.
- Look check: 81 screenshots with measured checks (overflow, font, 16:9, frame in view at 1366 × 768, textarea 16 px, buttons ≥ 44 px, reply indent ≤ 12 px); the comment states looked at. Script check in headless Chrome against a fake server: 22 checks.
- Code review: no HIGH/MED. LOWs fixed: `replies_of` scoped to the game, a 410 delete drops the item, focus back to the box after posting, a 선생님 test. LOWs left as is: the guest gap check scans `comments` (no `guest_hash` index; class scale), blank-looking characters beyond the spec's list (e.g. U+2800) pass, a crafted 64+ byte delete body gets the too-long message.

## Not done here
- Deploy on the Mac Mini with v0.34 (prompt Step 16: copy the pre-deploy backup to `pre-v0.35.db` at once, rehearse the new binary on it) and the operator check on the live gallery; then flip Comment and Reply to Active in the Glossary.
- The Deferred.md `result_large_err` entry's trigger ("the next change to `play.rs`") fired with v0.34's `play.rs` change, not this phase's; left for the operator to schedule.
