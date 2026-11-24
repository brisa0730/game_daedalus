# Phase 10: Gallery comments

## Overview

**Where things stand:** Players can give a gallery game stars, but they can't tell the student who made it what they liked, and the student can't answer.

**What this phase changes:**
- Anyone playing a game can leave a short comment under it. Signed-in students show their nickname; everyone else shows as 손님 (guest).
- Only the game's maker can reply, one level deep, and their replies are marked.
- Guests can write once the game has been open three minutes, everyone waits half a minute between comments, and a flood of guest comments pauses guests for a while.
- Writers can delete their own comments. The teacher sees who wrote each one and can delete any, from the game or from a list of recent comments.
- Writing a comment never restarts or interrupts the game.
- It goes live on the class gallery after a backup.

**How you'll know it worked:** On the live gallery, a guest's comment appears as 손님, the maker answers it, and the teacher sees who wrote it and deletes it, while the game keeps running.

**Not in this phase:** notifications, comment counts, a word filter, a teacher on/off switch, sorting and editing.

---

## Source
- **GDD sections:** `systems.md` § Share → Comments (v0.33) and → Teacher; `platforms.md` (rater cookie note, Access, migrations and rollback, pruning at start); `architecture.md` (gallery server row); `Glossary.md` (Comment, Reply: Draft); `.claude/memory/decisions.md` (the two 2026-11-24 comment rows). **The design is already in the GDD (v0.33).** This prompt adds the implementation choices and the design panel's adopted recommendations, and Step 13 brings the GDD text to as-built.
- **GDD version:** v0.34. This phase lands as v0.35. v0.34 (Mac, 2026-11-24: `pages.rs` links `gallery.css`, `gallery.js` and `player.js` by a fingerprint of their bytes, because Cloudflare caches them for 4 hours; `play.rs` touched too) is **not yet deployed** and goes out in this phase's deploy. Build on it: the script and stylesheet changes here reach browsers through those fingerprinted addresses, and the look check must load them the same way.
- **Depends on:** Phase 9 (`gallery-ratings`: the rater cookie, `game_opens` and the rating gate, `play_parts`, the `stars()` script), v0.32 (time labels, one-rating average), the public gallery (v0.20: `Access::Open`, the one route table).

**Paths:** `G/` means `Deploy/gallery/`. All code is in `G/`. Nothing changes in the Unity package, so there is no package release. `G/src/play.rs` (the games host) does not change, so the Deferred.md `result_large_err` entry is not triggered. Clippy on the laptop's `rust:1` image still needs that one lint allowed, as in Phase 9.

**Live state (checked 2026-11-24):** Changelog v0.32 records the v0.31 + v0.32 deploy at 14:38 KST, with migration 0004 applied. The live database is therefore expected at **0004**, and this deploy adds **0005**. Step 16 checks the level first. If it reads 3, the same steps apply 0004 and 0005.

**Settled (operator; canonical text in systems.md § Comments), summarised:**
1. Anyone, guests included, writes a top-level comment on a game's play page. Everyone reads them.
2. A signed-in writer shows their nickname. A guest shows 손님, and guests type no name.
3. Plain text, at most 300 characters after NFC (one Korean syllable is one character). No links, no formatting, no editing.
4. Only the game's owner replies: one level deep, under any comment on their game. The owner writes no top-level comment on their own game. Owner replies carry 만든이.
5. Who may write, and how often:
   - A guest is one browser, known by the rater cookie's hash.
   - A guest may comment only after the 3-minute rating gate.
   - Every writer waits 30 s between top-level comments. The owner's replies don't wait.
   - At most 20 guest comments in 10 minutes, gallery-wide. Signed-in writers never hit that cap.
6. A POST carries the identity the page showed. On a mismatch the server refuses; the draft stays and the game keeps running.
7. Deleting:
   - A writer deletes their own comment (a guest from the same browser). This only hides it, and its replies are hidden with it.
   - The teacher's 최근 댓글 shows a hidden comment marked 지움 for 30 days. Then it is erased at server start.
   - The owner can't delete others' comments.
   - The teacher deletes any comment, for good, at once.
8. **Real names only on the teacher page** (operator, 2026-11-24, after round 1 R1): 최근 댓글 at the top of the teacher page (newest 50, replies included, with 더 보기) shows each writer's real name (`teacher_view_name`: 손님 for guests, the nickname for the teacher). On `/play` the teacher sees nicknames and 손님 like everyone else, plus a delete button on every comment, so a game projected in class never shows a real name. The teacher deletes from either place.
9. The teacher's comments and replies carry 선생님.
10. Comments survive game updates. They go with the game (`tombstone_game`) or with the writer's account (FK cascade). No one is ever shown the guest hash.
11. No notifications, counts, word filter, switch, sorting or editing. Closing comments is a one-constant code change plus a redeploy.

**Design panel (adopted):**
- **Rows and privacy:** each row holds exactly one of `user_id` / `guest_hash` (CHECK). A signed-in comment never stores the rater hash, and stars are never shown beside a comment.
- **Focus:** the iframe's load handler focuses the game only when nothing else has focus, using `focus({preventScroll:true})`. The comment box is never focused automatically, and Enter never submits.
- **Fetch only:** every comment action goes through fetch, deletes use an inline confirm, and the DOM is built only from server-rendered maud fragments or `textContent`.
- **Layout and paging:** a "댓글 ↓" link in the play bar. The box sits on top, comments newest first, replies oldest first, 20 top-level comments per page with 댓글 더 보기.
- **Text cleaning:**
  - NFC; control and bidi characters stripped.
  - Zero-width characters and Hangul fillers count as blank.
  - Line breaks are kept, runs of blank lines collapse to one, and there are at most 5 lines.
  - Over 300 characters is refused, never cut. The page counts with `Array.from`.
- **Race safety:** the reply check is one SQL statement. The gap and the cap are checked inside the INSERT's WHERE.
- **Names:** nicknames are joined in at render time. Real names show only on the teacher page (최근 댓글), never on `/play`.
- **390 px:** one column, a 16 px textarea, `overflow-wrap:anywhere`, replies indented ≤ 12 px, buttons ≥ 44 px.
- **Copy:** the Korean copy in Step 4.

## File Manifest

| Action | File | Purpose |
|--------|------|---------|
| NEW | `G/migrations/0005_comments.sql` | `comments` table, CHECK, indexes |
| MODIFY | `G/Cargo.toml` | `unicode-normalization = "0.1"` (NFC) |
| MODIFY | `G/src/config.rs` | `comments_open`, `comment_gap_secs`, `guest_comment_cap`, `guest_cap_window_secs`, `hidden_comment_keep_secs`, `comments_page`, `recent_comments_page` |
| MODIFY | `G/src/text.rs` | `clean_comment`, `CommentProblem`, `COMMENT_MAX_CHARS`, `COMMENT_MAX_LINES` |
| MODIFY | `G/src/db.rs` | Comment queries, refusal reasons, hide / erase / purge, `tombstone_game` deletes comments, migration test |
| MODIFY | `G/src/strings.rs` | `// Comments` section |
| NEW | `G/src/comments.rs` | Viewer rules, item / section / recent-list markup, the four handlers |
| MODIFY | `G/src/lib.rs` | `pub mod comments` |
| MODIFY | `G/src/ratings.rs` | `rater()` becomes `pub(crate)` |
| MODIFY | `G/src/app.rs` | Four routes |
| MODIFY | `G/src/gallery.rs` | `days_ago` `pub(crate)`; play view: "댓글 ↓" link and the comments section |
| MODIFY | `G/src/teacher.rs` | 최근 댓글 section at the top of the teacher page |
| MODIFY | `G/src/main.rs` | Erase old hidden comments at start |
| MODIFY | `G/src/backup.rs` | Module doc's restore note: copy a pre-deploy backup to a fixed `pre-v<version>.db` name (round 1, F2); doc comment only |
| MODIFY | `G/static/gallery.js` | Focus rule, shared post / gate clock, comment box, replies, inline delete, more |
| MODIFY | `G/static/gallery.css` | Comments and 최근 댓글 styles |
| MODIFY | `G/static/player.js` | The games host's player page focuses the canvas only while its frame has focus (round 1, F1) |
| NEW | `G/tests/comments.rs` | Behaviour tests |
| MODIFY | `G/tests/support/mod.rs` | `comment`, `reply`, `delete_comment`, `user_id` helpers |
| MODIFY | `G/tests/teacher.rs` | Real-name invariant extended to comments and to the teacher's own `/play` view |
| MODIFY | `G/tests/look.rs` | Comment states and wires |
| MODIFY | `Documents/GDD/systems.md`, `platforms.md`, `architecture.md`, `Glossary.md`, `GDD.md`; `Documents/Operations/Changelog.md` | Step 13 |

---

## Steps

### Step 1: Settings and the migration
- **MODIFY `G/src/config.rs`:** add these fields with doc comments, defaults in `Config::new`:
  - `comments_open: bool = true`. **The one constant to change to close comments.** When false, nothing new can be written, and existing comments stay readable and deletable.
  - `comment_gap_secs: u64 = 30`.
  - `guest_comment_cap: i64 = 20` and `guest_cap_window_secs: u64 = 600`.
  - `hidden_comment_keep_secs: u64 = 30 * 24 * 3600`.
  - `comments_page: i64 = 20` and `recent_comments_page: i64 = 50`.

  The guest gate reuses `rating_gate_secs`, and `unix_now()` stays the only clock read.
- **NEW `G/migrations/0005_comments.sql`** (commented in the style of 0004; never edit 0004):
  ```sql
  -- Comments under a game (v0.35). Exactly one writer: a user (nickname joined at render time) or a guest browser
  -- (the rater cookie's SHA-256, never shown to anyone). A signed-in comment never stores the hash.
  -- Games are tombstoned, never deleted: removing a game deletes its comments explicitly (db.rs `tombstone_game`).
  CREATE TABLE comments (
      id INTEGER PRIMARY KEY,
      game_id TEXT NOT NULL REFERENCES games(id),
      parent_id INTEGER REFERENCES comments(id) ON DELETE CASCADE,
      user_id INTEGER REFERENCES users(id) ON DELETE CASCADE,
      guest_hash BLOB,
      body TEXT NOT NULL CHECK (length(body) BETWEEN 1 AND 300),
      created_at INTEGER NOT NULL,
      hidden_at INTEGER,
      CHECK ((user_id IS NULL) <> (guest_hash IS NULL))
  );
  CREATE INDEX comments_game ON comments(game_id, parent_id, id);
  CREATE INDEX comments_parent ON comments(parent_id);
  CREATE INDEX comments_user ON comments(user_id);
  CREATE INDEX comments_guest_recent ON comments(created_at) WHERE guest_hash IS NOT NULL;
  ```
  `foreign_keys(true)` is already on (db.rs `open_with`), so the cascades from `users` and from a parent work.
- **Tests:** `db.rs` `the_comments_migration_keeps_todays_games`, the same shape as `the_ratings_migration_keeps_todays_games`. Run migrations `[..4]`, insert one user, one game and one rating, then `Db::open_existing`. The game is live, its rating is still there, and `comments` is empty.
- **Verify:** `[automated]` `cargo test db::`.

### Step 2: Text cleaning (`G/src/text.rs`, `Cargo.toml`)
- Add `unicode-normalization = "0.1"` to `[dependencies]`. It is small and has no transitive surprises; record it in Notes to GDD.
- `pub const COMMENT_MAX_CHARS: usize = 300`, `pub const COMMENT_MAX_LINES: usize = 5`, `pub enum CommentProblem { Empty, TooLong }`.
- `pub fn clean_comment(raw: &str) -> Result<String, CommentProblem>`, in this order:
  1. NFC.
  2. `\r\n` and `\r` become `\n`.
  3. Tab becomes a space. Every other control character except `\n` is dropped, and so are the `is_bidi_control` characters.
  4. Per line: collapse whitespace runs to one space and trim. A line is **blank** when it holds only whitespace, zero-width characters (U+200B, U+200C, U+200D, U+2060, U+FEFF) or Hangul fillers (U+3164, U+115F, U+1160, U+FFA0); a blank line becomes `""`.
  5. Drop leading and trailing blank lines, and collapse each run of blank lines to one.
  6. Join lines 6 and later onto line 5 with a space.
  7. No non-blank line left gives `Empty`. More than `COMMENT_MAX_CHARS` `chars()` (newlines count one each) gives `TooLong`. Never truncate.
- **Tests (unit):**
  - `"  안녕  \n\n\n 하세요 "` → `"안녕\n\n하세요"`.
  - Bidi and control characters are stripped.
  - `"\u{200B}\u{3164} \n\u{FEFF}"` → `Empty`.
  - 300 Korean syllables → Ok; 301 → `TooLong`. 300 syllables typed as NFD jamo → Ok (NFC first).
  - 8 lines → 5 lines.
  - `"<b>x</b>"` is kept as typed (escaping is maud's job).
- **Verify:** `[automated]` `cargo test text::`.

### Step 3: Comment queries (`G/src/db.rs`)
All SQL stays in this file. Bind `guest_hash` / `user_id` as `Option`, and never SELECT `guest_hash` into a struct.
- **`pub struct CommentRow`:**
  - `id`, `game_id`, `parent_id: Option<i64>`, `user_id: Option<i64>`, `body`, `created_at`, `hidden: bool` (its own or its parent's `hidden_at`).
  - The writer, joined at read time: `nickname: Option<String>`, `real_name: Option<String>`, `teacher: bool`.
  - `by_this_browser: bool`, computed in SQL as `c.guest_hash IS NOT NULL AND c.guest_hash = ?viewer_hash`.
  - For the recent list: `game_title: Option<String>`.
- **`insert_comment(game_id, writer_user: Option<i64>, guest_hash: Option<&[u8]>, body, now, opened_by, gap_secs, cap, cap_window) -> Result<Option<i64>>`:** one `INSERT … SELECT ?… WHERE`, with all of these conditions:
  - the game is live and the writer isn't its owner: `(?user IS NULL OR owner_id IS NOT ?user)`;
  - guests only: an open row with `opened_at <= opened_by`;
  - `NOT EXISTS` a top-level comment by the same writer (`user_id = ?user OR guest_hash = ?hash`, hidden ones included) with `created_at > now − gap`;
  - guests only: `(SELECT COUNT(*) FROM comments WHERE guest_hash IS NOT NULL AND created_at > now − window) < cap`.

  Returns the new id, or `None` when nothing was written.
- **`comment_refusal(…same inputs…) -> Result<Refusal>`** (read only; used after a `None`, the way `rate` reads `rating_state`). `enum Refusal { GameGone, Owner, Gate(u64), Gap(u64), Busy }`, checked in that order. `Gap` carries the seconds left from the writer's last top-level comment.
- **`insert_reply(game_id, parent_id, owner_id, body, now) -> Result<Option<i64>>`:** one statement. `WHERE EXISTS` a parent with `id = ?`, `game_id = ?`, `parent_id IS NULL` and `hidden_at IS NULL`, `AND EXISTS` a live game with `owner_id = ?`. Replies skip the gap and don't start its clock. `reply_refusal` returns `GameGone | NotOwner | ParentGone`.
- **Reads:**
  - `comments_page(game_id, before: Option<i64>, limit, viewer_hash) -> Result<(Vec<CommentRow>, Option<i64>)>`: up to `limit` visible top-level comments `ORDER BY id DESC`, and the next cursor when there are more (fetch `limit + 1`).
  - `replies_of(parent_ids, viewer_hash)`: visible replies `ORDER BY id`. Replies of a hidden parent are never returned.
  - `comment_by_id(id, viewer_hash)`: **includes hidden rows** (and replies under a hidden parent) and carries `hidden` (its own `hidden_at` set) and `parent_hidden`, so the delete handler can answer 410 and the teacher can erase a hidden row from 최근 댓글 (round 1, F3). The page reads above stay visible-only.
  - `recent_comments(before, limit, hidden_since) -> (Vec<CommentRow>, Option<i64>)`: every comment and reply on live games `ORDER BY c.id DESC`, with the game title. Hidden rows are included only while `COALESCE(c.hidden_at, p.hidden_at) >= hidden_since` (`p` the parent, `LEFT JOIN`), so a reply under a parent hidden too long ago drops out with its parent (round 1, F5).
- **Writes:**
  - `hide_comment(id, user: Option<i64>, hash: Option<&[u8]>, now) -> Result<bool>`: `UPDATE … SET hidden_at = ? WHERE id = ? AND hidden_at IS NULL AND (user_id = ?user OR guest_hash = ?hash)`.
  - `erase_comment(id) -> Result<bool>`: `DELETE`; replies cascade.
  - `purge_hidden_comments(before) -> Result<u64>`: `DELETE … WHERE hidden_at < ?`; replies cascade.
- **MODIFY `tombstone_game`:** add `DELETE FROM comments WHERE game_id = ?` inside the existing transaction. Update its doc comment and the module line. Owner, teacher and account removal all reach it through `gallery::remove_game`.
- **Tests (db unit; explicit times, no clock):**
  - Gap boundary: comment at 1000; with a gap of 30, 1029 is refused and 1030 is accepted.
  - Cap: with cap 2, the third guest comment in the window is refused, and a user comment still passes.
  - Gate: refused when `opened_at > opened_by`.
  - Owner: a top-level comment is refused.
  - Reply: refused to a reply, to a hidden parent, to another game's comment, and from a non-owner.
  - Hide, then the page leaves out the comment and its replies. Purge removes old hidden rows and their replies, and keeps recent hidden and visible ones.
  - `tombstone_game` leaves 0 comment rows.
  - A raw insert with both or neither of `user_id` / `guest_hash` fails the CHECK.
- **Verify:** `[automated]` `cargo test db::`.

### Step 4: Words (`G/src/strings.rs`)
- Add a `// Comments` section with these constants. Data attributes copy the `{n}` templates, as with the ratings strings.
  - **Labels:**
    - `COMMENTS_HEADING` "댓글", `COMMENTS_JUMP` "댓글 ↓", `RECENT_COMMENTS` "최근 댓글"
    - `GUEST_NAME` "손님", `TEACHER_BADGE` "선생님", `MAKER_BADGE` "만든이", `HIDDEN_MARK` "지움"
  - **Box text:**
    - `COMMENT_PLACEHOLDER` "만든 친구가 읽어요. 재미있었던 점, 아쉬운 점을 남겨 주세요."
    - `COMMENT_AS_GUEST` "'손님'으로 남겨져요. 이 브라우저에서만 지울 수 있어요."
    - `fn comment_as(nick)` "'{별명}' 이름으로 남겨져요"
    - `COMMENT_PRIVACY` "이름·전화번호 같은 개인 정보는 쓰지 마세요."
  - **Buttons:** `COMMENT_SEND` "남기기", `REPLY` "답글", `REPLY_SEND` "답글 남기기", `COMMENT_DELETE` "삭제", `COMMENTS_MORE` "댓글 더 보기", and `CANCEL` (existing).
  - **Messages:**
    - `COMMENT_EMPTY` "댓글을 써 주세요.", `COMMENT_TOO_LONG` "300자까지 쓸 수 있어요."
    - `COMMENT_GAP` "{n}초 후에 다시 남길 수 있어요.", `COMMENT_GATE_MINUTES` "{n}분 후에 댓글을 남길 수 있어요" / `COMMENT_GATE_SECONDS` "{n}초 후에 댓글을 남길 수 있어요", picked by `comment_wait`'s rule (minutes rounded up while ≥ 60, then seconds), so the message and the button agree (round 1, F7)
    - `COMMENT_FAILED` "남기지 못했어요. 다시 눌러 보세요.", `COMMENT_GONE` "이미 지워진 댓글이에요."
    - `COMMENT_OWN` "내 게임에는 답글만 달 수 있어요.", `COMMENT_BUSY` "지금은 손님 댓글이 많아요. 잠시 후에 다시 써 주세요."
    - `COMMENT_SIGNED_OUT` "로그인이 끝났어요. 새 탭에서 로그인한 뒤 다시 눌러 주세요."
    - `COMMENT_POSTED` "댓글을 남겼어요"
    - `COMMENT_CONFIRM` "이 댓글을 지울까요? 답글도 함께 지워져요. 되돌릴 수 없어요."
    - `COMMENTS_EMPTY` "아직 댓글이 없어요.", `COMMENT_OWNER_NOTE` "불편한 댓글은 선생님께 알려 주세요."
  - **Button countdown:** `COMMENT_WAIT_MINUTES` "{n}분 후에 남길 수 있어요", `COMMENT_WAIT_SECONDS` "{n}초 후에 남길 수 있어요".
  - **New wording (see Notes):** `COMMENT_WHO_CHANGED` "다른 이름으로 로그인되어 있어요. 새로 고친 뒤 다시 남겨 주세요.", `COMMENTS_CLOSED` "지금은 댓글을 남길 수 없어요."
- `fn comment_wait(secs)`: whole minutes rounded up while ≥ 60, then seconds, like `rate_wait`. `fn comment_ago(days)`: "오늘" / "어제" / "{n}일 전". `fn teacher_view_name(real, nick)`: "{real} ({nick})", or just `nick` when `real` is empty.
- **Tests:** units for `comment_wait` (180/61/60/59/1), `comment_ago` (0/1/5) and `teacher_view_name` (real name empty and not).
- **Verify:** `[automated]`.

### Step 5: Rendering (`G/src/comments.rs`, part 1)
- `pub(crate)` in `ratings.rs`: `rater()`. `gallery.rs`: `days_ago` becomes `pub(crate)`.
- **`struct Viewer`:** `user: Option<User>`, `hash: Option<Vec<u8>>` (the rater cookie's hash, if any), `teacher` (`role == Role::Teacher`), `owner` (signed in and `game.owner_id == user.id`). Built once per request.
- **Writer name:**
  - A guest is `GUEST_NAME`.
  - On `/play` a user is always shown as the nickname, whoever views. Only the 최근 댓글 list uses `teacher_view_name(real, nick)`.
  - Badges: `TEACHER_BADGE` when the writer is the teacher. `MAKER_BADGE` on a reply whose writer is the game's owner; a teacher who owns the game gets both.
- **`fn item(row, replies, game, viewer) -> Markup`:**
  - `li.comment data-id=N`, containing `p.by` with `span.name`, the badges and `span.ago` (`comment_ago(days_ago(created_at, now))`), and `p.text` (the body; maud escapes it).
  - An actions row:
    - `button.comment-del type=button data-delete="/comments/N/delete"`, only when the viewer is the teacher, the writer (`user_id == viewer.id`) or `by_this_browser`.
    - `button.comment-reply type=button data-parent=N`, only when the viewer is the owner, the comment is top-level and `comments_open`.
  - Then `ul.replies` of reply items. **No rating, no star and no hash in any of it.**
- **`pub async fn section(state, game, viewer) -> Markup`:** `section#comments.comments`, with h2 `COMMENTS_HEADING` and these `data-*` attributes:
  - `post` (`/play/<id>/comments`) and `as` (`guest` or the user id).
  - `guest` (`1` / `0`) and `max` (300).
  - `msg-empty`, `msg-too-long`, `msg-gap`, `msg-gate`, `msg-wait-min`, `msg-wait-sec`, `msg-posted`, `msg-failed`, `msg-confirm`, `msg-gone`.
  - `label-delete`, `label-cancel`, `label-send`.

  Then, depending on who is looking:
  - **Non-owner and `comments_open`:** `div#comment-box` holding:
    - `textarea#comment-text rows=3 aria-label=COMMENTS_HEADING placeholder=…`, with no `maxlength` and no `autofocus`;
    - `span#comment-count` "0/300" and `button#comment-send type=button`;
    - `p.comment-who` (`COMMENT_AS_GUEST` or `comment_as(nick)`), `p.comment-privacy` and `p#comment-msg role=status`.
  - **Owner:** `p.comment-owner-note` and `template#reply-form`. The template holds `textarea.reply-text rows=2`, `span.reply-count`, `button.reply-send` (`REPLY_SEND`) and `button.reply-cancel` (`CANCEL`).
  - **Closed:** `p.comment-closed` `COMMENTS_CLOSED` instead of the box.
  - **Then:**
    - `ul#comment-list` holding the first page;
    - `p#comments-empty` (`hidden` when there are comments);
    - `button#comments-more type=button data-more="/play/<id>/comments?before=N"`, only when there are more.
- **`pub async fn recent_section(state, teacher) -> Markup`:** `section#recent` holding:
  - h2 `RECENT_COMMENTS`;
  - `ul#recent-comments data-as=<teacher id>`, with the same `msg-*` and `label-*` attributes;
  - each `li.comment` shows the game title (`a href="/play/<id>#comments"`), a `REPLY` marker on replies, real-name display, `span.gone HIDDEN_MARK` on hidden rows, and the delete button (the teacher may erase hidden rows too);
  - `button#recent-more data-more="/teacher/comments?before=N"` when there are more; `COMMENTS_EMPTY` when there are none.
- **Tests:** Step 10.
- **Verify:** `[automated]` `cargo test`.

### Step 6: Handlers and routes (`G/src/comments.rs`, part 2; `app.rs`; `lib.rs`)
Every answer goes through `upload::json_answer`. Each handler takes `State`, `Option<Extension<CurrentUser>>`, `Path` and `HeaderMap`, with the form **last** as `Result<Form<T>, FormRejection>`. That way the game or comment lookup runs first, so the routing test's `text=x` on game `1` gets a 404. A rejection answers `json_answer(rejection.status(), {"ok":false,"message":COMMENT_FAILED})`, except a 413 (the body over the route's limit, e.g. a pasted ~1,400 Korean characters), which answers `COMMENT_TOO_LONG`, since pressing again can never work (round 2, F9).
- **Identity check** (`fn check_as(as_field, user) -> Result<(), &'static str>`). Its errors are answered with 409 `{"ok":false,"message":…}`, and nothing is written.
  - `as=guest` with no session is ok; with a session, it is `COMMENT_WHO_CHANGED`.
  - `as=<id>` with that user is ok. With no session it is `COMMENT_SIGNED_OUT`; with another user it is `COMMENT_WHO_CHANGED`.
- **`POST /play/{id}/comments`** (form `as`, `text`, optional `parent`; `DefaultBodyLimit::max(4096)`). Checks in order:
  1. Live game, else 404.
  2. `!comments_open`: 403 `COMMENTS_CLOSED`.
  3. Form, then `check_as`.
  4. `clean_comment`: 400 `COMMENT_EMPTY` / `COMMENT_TOO_LONG`.
  5. **With `parent`:** a guest gets 403. Otherwise `insert_reply`, with refusals mapped as `ParentGone` → 410 `COMMENT_GONE`, `NotOwner` → 403, `GameGone` → 404.
  6. **Without `parent`:** the owner gets 403 `COMMENT_OWN`. A guest with no rater cookie gets 409 `{"wait": gate, "message": the COMMENT_GATE_* form for the full gate}`. Then `insert_comment` with `opened_by = now − rating_gate_secs`. On `None`, map `comment_refusal`: `Gate(w)` → 409 with `wait` and the `COMMENT_GATE_*` form for `w`; `Gap(w)` → 429 with `wait` and `COMMENT_GAP`; `Busy` → 429 `COMMENT_BUSY`; `Owner` → 403; `GameGone` → 404.
  7. On success: 200 `{"ok":true,"html": item(new row, viewer).into_string(), "wait": gap or 0 for a reply}`.
  8. DB errors: 500 `COMMENT_FAILED`.
- **`GET /play/{id}/comments?before=N`:** live game, else 404. Answers `{"ok":true,"html": the next page of items, "more": URL or null}`. It is JSON, so the routing test's HTML-header check skips it. It writes nothing.
- **`POST /comments/{id}/delete`** (form `as`; limit 64). Checks in order:
  1. A numeric id and an existing comment, else 404.
  2. Form, then `check_as`.
  3. **Teacher:** `erase_comment`, then `tracing::info!(comment = id, game = %g, by = %user.login, "comment erased")`.
  4. **Otherwise:** when `comment_by_id` says `hidden` or `parent_hidden` → 410 `COMMENT_GONE` (round 1, F4). Else `hide_comment(id, user id, browser hash)` is true → 200; false → 403, the game's owner included.
  5. No log line on a writer's delete, and never a hash or an IP.
- **`GET /teacher/comments?before=N`** (Teacher): `{"ok":true,"html":…,"more":…}` from `recent_comments(before, recent_comments_page, now − hidden_comment_keep_secs)`.
- **Logging:** a post logs nothing on success. Failures log only the game id and the error, never the text, a hash, a login or an IP.
- **MODIFY `app.rs`:**
  - `route(GET, "/play/{id}/comments", Open, get(comments::more))`
  - `route(POST, "/play/{id}/comments", Open, post(comments::post).layer(DefaultBodyLimit::max(4096)))`
  - `route(POST, "/comments/{id}/delete", Open, post(comments::delete).layer(DefaultBodyLimit::max(64)))`
  - `route(GET, "/teacher/comments", Teacher, get(comments::recent_more))`

  The paths use only `{id}`, as `routing.rs` `concrete()` needs. No GET under `/teacher/` takes an id, because `every_signed_in_page_shows_the_name_and_sign_out` substitutes a student id there.
- **MODIFY `lib.rs`:** `pub mod comments`.
- **Verify:** `[automated]` `cargo test --test routing` passes **unchanged**.

### Step 7: Play page and teacher page
- **MODIFY `gallery::play_view`:**
  - Build the `Viewer`.
  - Add `a.to-comments href="#comments"` (`COMMENTS_JUMP`) to the play bar, before `#fullscreen`. If the look check shows the bar wrapping at 1366 × 768, move it to the end of `.play-title` instead (as the Phase 9 hint did).
  - Render `(comments::section(…))` after `div#frame`, inside `.theater`.
- **MODIFY `teacher::page`:** `(comments::recent_section(&state, &user))` as the first section, under the h1.
- **Verify:** `[automated]` Step 10 and look tests.

### Step 8: The script (`G/static/gallery.js`)
- **Focus:**
  - Replace the load handler with one that calls `game.focus({preventScroll:true})` only when `document.activeElement` is `document.body`, `null` or the game. This covers the fallback reload too.
  - The start-up focus and the star click use `{preventScroll:true}`, so focusing the game never scrolls the page away from the comments.
  - No comment action focuses the game.
  - **Inside the frame (`G/static/player.js`, served by the games host):** its start-up `focus()` (script start) and `started()` (when `createUnityInstance` resolves, which on the slow line can be many seconds in, and again after the `?local=1` fallback reload) call `canvas.focus()` only when `document.hasFocus()`. The frame already has focus when the parent's start-up focus put it there; a player who moved to the textarea took it away. The `click` and `window` `focus` listeners stay as they are, so clicking the game still hands it the keyboard. `player.js` reaches browsers through its v0.34 fingerprinted address, so the change needs no cache step. `play.rs` still doesn't change.
- **Shared code:**
  - Move `post(url, body)` out of `stars()` to the IIFE's top level.
  - Add a small gate clock: a deadline, a 1 s tick and a list of listeners.
  - `stars()` sets the deadline from `data-wait` and from every `open()` answer, and exposes `open()` for re-use. The stars' behaviour is otherwise unchanged.
- **The box (`#comments` with `#comment-box`):**
  - On `input`, the counter shows `Array.from(v.normalize("NFC")).length + "/" + max`, and turns red over the max. It is a guide only: the server counts after cleaning (whitespace and blank lines collapse), so the click never refuses a draft locally for length; only an empty-after-trim draft is refused locally, and the server's 400 `COMMENT_TOO_LONG` decides the rest (round 1, F8).
  - The send button is a `type=button` with no form, and Enter is never caught.
  - Guest (`data-guest=1`): while the gate deadline is in the future, the button is disabled and its label is the `msg-wait-*` countdown on the same clock as the stars. The textarea stays typeable.
  - Click: an empty draft shows the message locally. Otherwise `post(data-post, URLSearchParams{as, text: value.normalize("NFC")})`.
    - **200:** insert `answer.html` at the top of `#comment-list` with `insertAdjacentHTML`. This is server-escaped maud, never user text. Hide `#comments-empty`, clear the box, show `msg-posted`, and count the button down `answer.wait` seconds. Focus stays where it was.
    - **409 with `wait`:** re-send `open()` and restart the gate from its answer, then show the message.
    - **429 with `wait`** (the gap; it counts across the gallery, so it can hit on a game where this page never posted): count the button down `answer.wait` seconds as after a 200, keep the draft and show the message (round 1, F6). A 429 without `wait` (`COMMENT_BUSY`) only shows the message. Any other non-200: show `answer.message` or `msg-failed`, and keep the draft.
- **Replies (owner):** `.comment-reply` clones `template#reply-form` under its comment, at most one open at a time. `reply-send` posts with `parent`. On 200 the reply is appended to that comment's `ul.replies`. `reply-cancel` removes the form.
- **Delete (both pages):**
  - `.comment-del` swaps the actions for an inline confirm built with `textContent`: `msg-confirm`, a 삭제 button and a 취소 button.
  - Confirm posts `as=…`. On 200 the `li` is removed, and on 410 `msg-gone` is shown and the `li` removed. Cancel restores the actions.
- **More (both pages):** `#comments-more` / `#recent-more` GET `data-more`, append `answer.html`, then update `data-more` or remove the button. A failed fetch or an answer that isn't JSON (a teacher whose session ended gets the 303 to `/login`) is caught and shows `msg-failed`; the button stays (round 1, F8).
- **Never:** navigate, reload, read `innerHTML` of user text, or listen for messages. Update the header comment.
- **Verify:** `[automated]` the look test wires (Step 12). `[manual]` the operator check (Step 16). Optionally the Phase 9 approach: drive the script in headless Chrome against a fake server for focus, the countdown and insert/delete.

### Step 9: Styles (`G/static/gallery.css`)
- `.comments`: one column, `max-width: 760px`, centred under the frame. `#comment-text` and `.reply-text` get `font-size: 16px` (iOS zoom) and `width: 100%`.
- `.comment .text { white-space: pre-line; }`. `.comment .text, .comment .name { overflow-wrap: anywhere; }`. `.replies` indent ≤ 12 px with a left rule.
- Comment buttons are at least 44 px high. The badges get small pill styles, and `.gone` is muted.
- Any rule that sets `display` on `#comments-empty`, `#comment-box` or `#recent-more` also gets its `[hidden] { display: none; }` guard. No inline styles (CSP).
- **Verify:** `[manual]` screenshots (Step 12).

### Step 10: Behaviour tests (`G/tests/comments.rs`, `support/mod.rs`, `teacher.rs`)
- **Helpers:** `user_id(app, login)`, `comment(app, game, cookie, as_, text) -> Answer`, `reply(app, game, cookie, as_, parent, text)`, `delete_comment(app, id, cookie, as_)`. Guests use `open_rating` and `backdate_open`.
- **Tests:**
  1. **Who it shows:** a signed-in comment shows the nickname; a guest comment (after `backdate_open(200)`) shows 손님. The play page lists newest first, with replies oldest first under their parent. `<b>x</b>` arrives escaped in both the page and the JSON `html`.
  2. **Gate:**
     - A guest with no cookie gets 409 with `wait` 180. A fresh open gives 409; 170 s back is still 409; 180 s back gives 200.
     - Signed-in writers need no open.
     - The play GET writes no comment row and sets no rater cookie.
  3. **Gap:** a second top-level comment right away gets 429 with `wait` in 1..=30 and the `COMMENT_GAP` text. With `comment_gap_secs = 0` it passes. The owner's back-to-back replies both get 200.
  4. **Guest cap:** with `guest_comment_cap = 2`, gap 0 and gate 0, the third guest comment, from a third browser, gets 429 `COMMENT_BUSY`. A signed-in comment right after still gets 200.
  5. **Identity:** each mismatch gets 409 with the right message and writes no row.
     - `as=<kim>` with no session gives `COMMENT_SIGNED_OUT`.
     - `as=guest` with kim's session, and `as=<lee>` with kim's session, give `COMMENT_WHO_CHANGED`.
  6. **Text:**
     - Whitespace only, or zero-width / filler only: 400 `COMMENT_EMPTY`.
     - 301 syllables: 400 `COMMENT_TOO_LONG`. 300 syllables: 200.
     - 300 four-byte emoji, URL-encoded, fit the 4096 limit (200). A 5000-byte body gets 413 with `COMMENT_TOO_LONG` and writes no row.
  7. **Owner:**
     - A top-level comment on their own game gets 403 `COMMENT_OWN`.
     - Their reply shows `MAKER_BADGE`.
     - Refused replies: a reply to a reply, to a hidden comment (410), from a non-owner (403) and from a guest (403).
     - The owner deleting someone else's comment gets 403, and the row is unchanged.
  8. **Delete:**
     - A writer's delete sets `hidden_at`. The comment and its replies leave the page for everyone, the teacher's play view included.
     - A guest deletes from the same browser; another browser gets 403. Deleting again gets 410, and so does deleting the owner's reply under an already hidden comment.
     - A teacher delete erases at once, with its replies (0 rows).
  9. **최근 댓글:**
     - `/teacher` shows newest first, replies included, with real names (`teacher_view_name`) and 손님, and hidden rows marked 지움.
     - With `recent_comments_page = 2`, `GET /teacher/comments?before=` returns the next page and `more`.
     - A hidden row older than `hidden_comment_keep_secs` is not listed, and neither is a reply (itself not hidden) under such a parent.
  10. **Paging:** 25 top-level comments (gap 0) show 20 and `#comments-more`. Its GET returns 5 and `more: null`.
  11. **Removal ×3:** owner, teacher and account removal each leave 0 rows for the game. Account removal also deletes that user's comments on other games, and the replies under them.
  12. **Update:** an upload to the same game keeps the comments.
  13. **Privacy:**
      - `PRAGMA table_info(comments)` is exactly `id, game_id, parent_id, user_id, guest_hash, body, created_at, hidden_at`.
      - A signed-in writer whose browser has a rater cookie stores `guest_hash IS NULL`.
      - The browser hash's hex never appears in any page or JSON.
      - A guest who rated 5 and then commented has no ★ in their comment item.
  14. **Closed:** with `comments_open = false`, there is no `#comment-box` and no `.comment-reply`. POST gets 403 `COMMENTS_CLOSED`. Existing comments show, and delete still works.
- **MODIFY `tests/teacher.rs`:**
  - `real_names_never_leave_the_teacher_pages`: add a comment by kim (real 김실명) on a game kim doesn't own, and kim's reply on her own game. Every listed play page, and `GET /play/{id}/comments`, still never shows 김실명 to non-teachers.
  - New `the_teacher_sees_real_names_only_on_the_teacher_page`: `/teacher` shows "김실명 (별빛)" in 최근 댓글; the teacher's own `/play/{id}` and `GET /play/{id}/comments` show 별빛, never 김실명, with a delete button on every item.
- **Verify:** `[automated]` `cargo test --test comments --test teacher`.

### Step 11: Erase old hidden comments at start (`G/src/main.rs`)
- After `prune_opens`, call `db.purge_hidden_comments(now − hidden_comment_keep_secs)` and log `tracing::info!(erased, "old hidden comments erased")`. A failure only warns, as for opens.
- **Verify:** `[automated]` the db unit test from Step 3. `[manual]` the log line in Step 16.

### Step 12: Look check
- **MODIFY `tests/look.rs`:** add comments on `old` (kim's game). Use `comment_gap_secs = 0` in this test's `TestApp::with`, and backdate opens for the guests.
  - Samples: 300 unbroken Latin letters (lee), 300 Korean syllables (park), 5 lines (a guest), a commenter with a maximum-length nickname (add a student whose nickname is at the configured maximum), a teacher comment, and kim's 만든이 reply.
  - One comment hidden by its writer, so it shows as 지움 on the teacher page.
- **New pages:**
  - `play-comments`: lee on `old`; box with nickname note, the list, 더 보기 (add enough comments, or set `comments_page = 5` for this test).
  - `play-comments-guest-wait`: a fresh guest on `old`; button countdown on the gate.
  - `play-comments-owner`: kim on `old`; no box, owner note, 답글 buttons, `template#reply-form`.
  - `play-comments-teacher`: the teacher on `old`; nicknames only, delete on every item.
  - The existing `teacher` page now carries 최근 댓글 with a 지움 row.
- **Wire assertions:**
  - Non-owner pages: ids `comments`, `comment-box`, `comment-text`, `comment-count`, `comment-send`, `comment-msg`, `comment-list`, `comments-empty`; data `post`, `as`, `guest`, `max`, every `msg-*` and `label-*`.
  - Owner page: `reply-form` present, no `comment-text`.
  - Teacher page: `recent`, `recent-comments`, data `as`.
  - Everywhere: no inline style, and no `autofocus`.
- **Run the look check** per CLAUDE.md: `cargo test --test look`, serve `TestResults/gallery-look/` on localhost, and screenshot every page at 1280 × 900 and 390 × 844, plus the play pages at 1366 × 768. Check:
  - no horizontal overflow at 390 px, with the 300 Latin letters and the long nickname;
  - the font loaded and the frame at 16:9;
  - at 1366 × 768 the whole frame is still in view, with the play bar on one row including "댓글 ↓" (otherwise move it, per Step 7);
  - replies are indented ≤ 12 px, buttons are ≥ 44 px, and the textarea is 16 px.

  Look at every screenshot.
- **Verify:** `[manual]` screenshots inspected.

### Step 13: Docs (bring v0.33 to as-built)
- **`systems.md` § Comments and § Teacher:** drop "(design)". Record the as-built details: the four routes, 20 per page with 댓글 더 보기, 최근 댓글 50 per page, the gap counting top-level comments only, replies neither waiting nor starting the clock, "closed" meaning no new writing (existing still shown), and the hidden-row purge at server start.
- **`platforms.md`:**
  - The Access table: Open adds `GET`/`POST /play/<id>/comments` (≤ 4 KB) and `POST /comments/<id>/delete` (≤ 64 B); Teacher adds `GET /teacher/comments`.
  - The rater cookie note: a guest comment keeps the hash, which is never shown; a signed-in comment never stores it.
  - Migration `0005_comments`: the v0.32 binary refuses the database afterwards, and a rollback loses everything since the deploy.
  - Hidden comments are erased at start after 30 days.
  - **Rollback copy (every deploy):** a backup taken just before a deploy is named by UTC date and the next nightly run can overwrite it; copy it to a fixed `pre-v<version>.db` name first. Add the same line to `backup.rs`'s restore note.
  - **Closing comments:** set `comments_open: false` in `config.rs`, commit, then on the Mac `git pull`, `cargo build --release`, `install -m 0755`, `launchctl kickstart -k`. There is no migration, but take a backup first anyway.
- **`architecture.md` gallery row:** "comments with owner-only replies (since v0.35)".
- **`Glossary.md`:** Comment and Reply stay Draft until the operator check passes, then flip to Active (Step 16).
- **`GDD.md`:** version line to v0.35. **Changelog:** v0.35 entry; Step 16 fills in its *Deployed* line. Commit `v0.35: Phase 10 — gallery comments`.
- **Verify:** `[grep]` `0005_comments` appears in `platforms.md`, and `comments_open` in `platforms.md` and `config.rs`.

### Step 14: Run Tests
- In `G/` (from the laptop: the `rust:1` Docker command in `.claude/memory/framework.md`): `cargo test` and `cargo clippy --all-targets -- -D warnings -A clippy::result_large_err` (the one known lint, Deferred.md). Fix until both are green. Do not proceed to review until they pass.

### Step 15: Code Review
- Run `.claude/agents/workflow/code-review.md` on all changed files. Pay special attention to:
  - **Fails closed:** every refusal fails closed (no cookie, no open, an open too recent, a gap or the cap reached, an identity mismatch, a hidden parent).
  - **SQL:** the inserts check everything in their WHERE.
  - **Privacy:** `guest_hash` is never selected into a struct, a page, JSON or a log. A signed-in row has no hash. Real names show only on the teacher page (최근 댓글), never on `/play`.
  - **Escaping:** maud escapes every user string, and the script inserts only server markup or `textContent`.
  - **Focus:** the load handler doesn't steal focus from the textarea.
  - **Removal:** the removal transaction and the cascades.

  Fix HIGH/MED findings and report the result. Deploy only reviewed code.

### Step 16: Deploy on the Mac Mini and operator check
Claude runs this on the Mac Mini, or the operator runs it there. It is the operator's machine.
1. `cd` to the repo, `git pull`, and confirm HEAD is the Step 13 commit. Check the live level with `sqlite3 "$HOME/Library/Application Support/daedalus-gallery/gallery.db" "SELECT MAX(version) FROM _sqlx_migrations"`. Expect 4; if it is 3, the same steps apply 0004 and 0005.
2. In `Deploy/gallery/`: `cargo build --release`. This installs nothing yet.
3. **Backup with the live binary:** `"$HOME/Library/Application Support/daedalus-gallery/bin/gallery" backup --data "$HOME/Library/Application Support/daedalus-gallery" --to "$HOME/Library/Application Support/daedalus-gallery-backups"`. Copy the printed file **out of the rotating folder at once**: `cp <printed copy> "$HOME/Library/Application Support/daedalus-gallery-backups/pre-v0.35.db"`. Copies are named by UTC date (`backup.rs` `utc_date`) and the nightly job runs at 03:30 KST (18:30 UTC the day before), so after 09:00 KST that night's run, on the new binary, would overwrite the printed copy with the migrated database, and a rollback would restore a database the v0.32 binary refuses (round 1, F2).
4. **Rehearse on a copy of that backup** (the server can't run twice: it binds only 127.0.0.1:8470):
   - Make the copy: `S=$(mktemp -d)`; `cp "$HOME/Library/Application Support/daedalus-gallery-backups/pre-v0.35.db" "$S/gallery.db"`; `mkdir "$S/games"`.
   - Run `target/release/gallery backup --data "$S" --to "$S/out"`. It must succeed; it opens the copy and applies 0005.
   - Run `sqlite3 "$S/gallery.db"` with these checks:
     - `SELECT version FROM _sqlx_migrations ORDER BY version` lists 1–5;
     - `SELECT COUNT(*) FROM comments` is 0;
     - `SELECT COUNT(*) FROM games WHERE removed_at IS NULL` and `SELECT COUNT(*) FROM ratings` equal the same queries on the untouched backup copy.
   - If anything fails, stop: nothing live has changed. Delete `$S` when done.
5. `cp bin/gallery bin/gallery.prev` (the v0.32 binary). Then `install -m 0755 target/release/gallery` into that `bin/`, and run `launchctl kickstart -k gui/$(id -u)/com.orcapafea.daedalus.gallery`.
6. Check `gallery.log` for `gallery listening`, `old open records pruned`, `old hidden comments erased erased=0` and the reconcile line.
7. **Rollback** (soon or not at all): bootout the job, put `gallery.prev` back, restore `pre-v0.35.db` (step 3) per `backup.rs` (delete `-wal`/`-shm` first), and bootstrap. Everything since the deploy is lost: comments, ratings, uploads, removals, account and password changes.

**Operator check (live gallery, documented; report it, don't block):**
- **Do:**
  - **Chrome A, signed out, while the game is still loading:** press "댓글 ↓" at once, click the box and keep typing until the game has started (on a fresh load or with the network throttled, so the load takes several seconds). Then reload and play with the keyboard. Scroll down with "댓글 ↓", click the box, and type Korean with the IME, pressing Enter twice. Within the first 3 minutes, try 남기기. After 3 minutes, press 남기기, then click the game and press its keys. Delete that comment with the inline confirm, then write a new one after 30 s.
  - **Chrome B, signed in as a student (not the owner):** comment. Sign out in a second tab, then press 남기기 again in the first.
  - **Chrome C, as the game's owner:** open the game and reply twice in a row.
  - **As the teacher:** open the game, then the teacher page. Erase one comment.
  - **Optionally, a phone:** open the game page.
- **Observe:**
  - Before 3 minutes the button counts down with the stars; after posting, it counts 30 s.
  - Typing during the load goes on in the box when the game starts; no keystroke lands in the game. Enter makes a new line. "댓글을 남겼어요" shows and the comment appears at the top as 손님. The game kept running throughout, and responds to its keys after a click.
  - B shows the nickname, then "로그인이 끝났어요…" with the draft kept.
  - C has no box, shows the owner note, and its two replies show 만든이 with no wait.
  - On the game page the teacher sees nicknames and 손님 (no real name) with 삭제 on every comment; on the teacher page 최근 댓글 shows "이름 (별명)" and 손님. 최근 댓글 shows A's deleted comment marked 지움, and the erased comment is gone.
  - On the phone: no sideways scroll, and typing doesn't zoom.
- **Must be absent:**
  - a page reload or game restart on any comment action;
  - Enter sending a comment;
  - focus jumping to the game after posting, or the game stealing focus while typing;
  - a guest comment accepted before 3 minutes;
  - a real name on any game page, the teacher's included;
  - stars beside a comment.
- Afterwards the teacher erases the test comments. If the check passes, flip Comment and Reply to Active in the Glossary, and write the Changelog *Deployed* line (time, the `pre-v0.35.db` path, log lines), committed as `docs: gallery v0.35 deployed live`.

---

## Completion Criteria
- [ ] `cargo test` (routing tests unchanged and green) and `cargo clippy --all-targets -- -D warnings` (only `result_large_err` allowed) pass.
- [ ] Anyone comments. A guest only after the 3-minute gate. Each writer waits 30 s between top-level comments, and the owner's replies don't wait. A 21st guest comment in 10 minutes is refused, and signed-in writers are never capped. Every check is inside the INSERT.
- [ ] A POST whose identity doesn't match the session is refused with the right message, and nothing is written.
- [ ] Text is NFC-cleaned. Blank or invisible text is refused, over 300 characters is refused (never cut), at most 5 lines are kept, and everything is escaped.
- [ ] Only the owner replies, one level deep, under visible top-level comments, with 만든이. The owner can't write a top-level comment on their own game or delete others' comments. The teacher's items carry 선생님.
- [ ] A writer's delete hides the comment and its replies. The teacher's 최근 댓글 shows it as 지움 for 30 days, then the start-up purge erases it. A teacher delete erases at once.
- [ ] Real names show only in 최근 댓글 on the teacher page, never on `/play` (the teacher's included), and the guest hash appears nowhere. A signed-in comment stores no hash.
- [ ] Comments survive updates. Owner, teacher and account removal delete them.
- [ ] Every comment action uses fetch. The game never reloads, Enter never submits, the comment box is never auto-focused, and the load handler never steals focus.
- [ ] Look check done at 1280 × 900, 390 × 844 and 1366 × 768. No overflow at 390 px, the font loaded, the frame 16:9 and fully in view, screenshots looked at.
- [ ] Deployed after a backup with the live binary and a rehearsal on a copy of it. The operator check is reported.

---

## Notes to GDD
- **Bring the v0.33 design text to as-built** (Step 13). Flip Comment and Reply to Active in the Glossary once the operator check passes. Record the routes, migration 0005, the config knobs, the platforms.md Access table and cookie note, and the architecture.md gallery row.
- **Implementation choices not in the decisions:**
  - Routes: `GET`/`POST /play/{id}/comments` (POST ≤ 4096 B: `as`, `text`, optional `parent`), `POST /comments/{id}/delete` (≤ 64 B: `as`), `GET /teacher/comments`. The GETs answer JSON with a rendered fragment, because the routing tests require every HTML page to carry the signed-in header.
  - Config: `comments_open`, `comment_gap_secs` (30), `guest_comment_cap` (20), `guest_cap_window_secs` (600), `hidden_comment_keep_secs` (30 days), `comments_page` (20), `recent_comments_page` (50). The guest gate reuses `rating_gate_secs`.
  - **Closing comments** (`comments_open = false`) stops new comments and replies. Existing comments stay readable and deletable, so the teacher can still clean up. If the operator wants "closed" to hide everything, it is a one-line change in `section`.
  - The 30 s gap counts the writer's previous **top-level** comment, hidden ones included. Replies neither wait nor start the clock.
  - Lines after the fifth are joined onto the fifth (not refused).
  - The JS sends NFC text, so NFD input doesn't hit the 4 KB limit.
  - A signed-in browser can also delete guest comments made earlier from the same browser ("guest = one browser").
  - A teacher who owns the game shows both 선생님 and 만든이 on their replies.
  - New dependency: `unicode-normalization` (NFC).
- **New wording (operator to confirm):**
  - `COMMENT_WHO_CHANGED` "다른 이름으로 로그인되어 있어요. 새로 고친 뒤 다시 남겨 주세요." The page showed guest or another user, but a different session exists now.
  - `COMMENTS_CLOSED` "지금은 댓글을 남길 수 없어요."
  - The button countdown "{n}분/{n}초 후에 남길 수 있어요", which extends the given minute form to seconds and is reused for the 30 s gap.
- **Correction:** the inputs said the live database was at 0003. Changelog v0.32 records the v0.31 + v0.32 deploy (14:38 KST, 0004 applied), so this deploy adds only 0005. MEMORY.md and Roadmap §1 still say "not deployed" (fixed in cleanup).
- **Open risks (accepted):**
  - Many incognito windows still allow up to 20 guest comments in 10 minutes. The teacher's delete is the safeguard.
  - Between restarts, hidden rows can outlive 30 days in the table, but they are not listed.
  - Owner gone (owner id NULL): nobody can reply. That is edge data only.

**If anything here contradicts the Overview, fix the Overview.**

---

## Post-Phase Cleanup
After code review passes (and Step 16 is done or handed to the operator):
1. Move this prompt to `Documents/Phases/archive/`.
2. Update the phase status in CLAUDE.md if it lists one.
3. Write `.claude/memory/logs/phases/phase10-log.md`.
4. Update `MEMORY.md` current state. Correct the stale "v0.31–v0.32 not deployed" line, and note that Phase 9's operator check is still open if it is. Update Roadmap §1: drop the `gallery-comments` entry and its "still-undeployed" sentence.
5. `decisions.md`: no new ADR unless an implementation choice above is changed by the operator. `lessons-learned.md`: add "routing tests forbid HTML fragments on Open/Teacher GETs; answer JSON" if it proved useful.
6. Deferred items: file any leftover in `Documents/Operations/Deferred.md` (Source = Phase 10). The `result_large_err` entry's trigger (touching `play.rs`) did not fire.
7. Commit all phase work.

## Workflow
Execute steps sequentially. Proceed to the next step without waiting for user approval. Only stop if blocked. If blocked, stop and report: what step, what was attempted, what failed.

## Review round 1

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

## Review round 2

---
slug: phase10-gallery-comments
round: 2
verdict: approved
reviewer: claude-cowork
updated: 2026-11-24T15:12:00+09:00
blocking: 0
recommendations: 1
---

# Round 2: Phase 10, gallery comments (delta review)

## What I could run, what I verified, what I could not

**Ran:** `device_bash` on the laptop's daedalus folder (grep/sed over the plan, `log.md` and `Deploy/gallery/`). No cargo, no Chrome.

**Delta checked against round 1 and the log's dispositions:**
- **F1 closed.** Step 8 now guards `player.js`'s start-up and `started()` focus with `document.hasFocus()` and keeps the click and window-focus listeners; the manifest lists `player.js`; Step 16 adds typing while the game loads. Verified: `play.rs` 146 embeds the file with `include_str!` and 149 builds `PLAYER_JS_URL` with `pages::versioned("player.js", PLAYER_JS)`, so the plan's "reaches browsers through its fingerprinted address" and "`play.rs` still doesn't change" both hold (the Deferred `result_large_err` trigger stays unfired).
- **F2 closed.** 16.3 copies the printed backup to `daedalus-gallery-backups/pre-v0.35.db`; 16.4 and 16.7 use it; Step 13 adds the rule to platforms.md and `backup.rs`'s note. Verified: `prune_copies` (backup.rs) only selects names starting `gallery-` and ending `.db` in `<to>/db/`, and `pre-v0.35.db` sits one level up, so neither the nightly rename nor the 7-copy prune can touch it.
- **F3–F7 closed** as described in the log (lines 171, 172, 202, 264, 272, 311 of the plan).
- **F8 closed**, with the seam noted in F9 below.
- **R1** is written beside settled item 8 as an open question for the operator, and the plan builds the settled version meanwhile. That's how the protocol says to handle it.
- The plan now includes round 1 verbatim under `## Review round 1`, which is what protocol.md line 164 says to do.

## Non-blocking

### F9 (non-blocking, design): with no local length check, a long paste gets "try again"
**Location:** Step 8 counter (line 305, the F8 remedy) and Step 6 (form rejection → `COMMENT_FAILED`).
**Defect:** this is a seam from F8. Before, the page refused a raw draft over 300 characters, so a request never got near the 4096-byte limit. Now the page sends any length. A student who pastes about 1,400 Korean characters (9 encoded bytes each) goes over 4096 bytes, the `Form` rejection is a 413, and the plan answers it with `COMMENT_FAILED` "남기지 못했어요. 다시 눌러 보세요." Pressing again can never work. Map a 413 rejection to `COMMENT_TOO_LONG` (and say so in test 6's 5000-byte case), or keep a coarse local refusal when the raw count is well over the max (for example, more than twice it).

## Recommendation
R1 stays open for the operator (no change).

## Reviewer's view
Both blocking findings are fixed, and I checked the fixes against `play.rs` and `backup.rs`. All of round 1's non-blocking findings are folded in. F9 is one line in the rejection mapping. I'd build this as written once F9 is folded in. Approved.
