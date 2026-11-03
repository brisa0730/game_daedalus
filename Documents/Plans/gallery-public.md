# Plan: Public Gallery

**Date:** 2026-11-03 | **Status:** approved (handshake, 3 rounds; awaiting operator approval to execute)

## Overview

**Where things stand:** Only signed-in people can see or play games. Students want friends and family to play. Cards show the name the teacher typed, often a real name.

**What this plan changes:**
- Anyone can browse the gallery and play every game.
- Uploading, removing, passwords and the teacher's page still need sign-in.
- Students get a public nickname. Their real name stays visible to the teacher only. Nicknames are set before anyone outside the class can visit.
- Each game page gets a copy-link button.
- Search engines are asked not to list the gallery.

**How you'll know it worked:** A browser that isn't signed in plays a game from a copied link. Upload still asks for sign-in. A renamed student's cards show the nickname.

**Not in this plan:** Keeping games from reading each other's saves.

**Why a plan and not a patch:** It changes who can reach which page, across the server, tests and design docs.

**Design choice:** Games still play only inside the gallery's sandboxed frame. That keeps games apart from the gallery; it no longer limits who plays.

---

## Source
- **Trigger:** operator request, 2026-11-03: "students want to share this with their friends and relatives … id/pw for students so they can upload, but public so anybody can play." Settled in the same conversation: (1) the **whole gallery** is public, not only per-game links; (2) students appear by **nickname**; (3) public pages are **noindex**; (4) bandwidth is acceptable, no action.
- **Related issues:** none
- **Scope:** gallery access rules (browse/play open; upload/manage gated), a teacher rename action for nicknames, a copy-link button, noindex headers.
- **Depends on:** v0.19 gallery look (committed, `018d4f8`).

## File Manifest

| Action | File | Purpose |
|--------|------|---------|
| MODIFY | `Deploy/gallery/src/app.rs` | New `Access::Open` (session optional); `/`, `/shots/{file}`, `/play/{id}` become `Open`; new teacher rename route |
| MODIFY | `Deploy/gallery/src/session.rs` | `require_access` passes an `Open` request through without a session, still attaching `CurrentUser` when one exists |
| MODIFY | `Deploy/gallery/src/pages.rs` | The shell renders a signed-out header (logo, 게임 목록, 로그인) when there is no user |
| MODIFY | `Deploy/gallery/src/gallery.rs` | `list` and `play_view` take an optional user; signed-out list has no "My games"; play view gets the copy-link control |
| MODIFY | `Deploy/gallery/src/teacher.rs` | Rename form per student row and the `rename` handler; the name column and create forms say 별명 with a public-visibility hint |
| NEW | `Deploy/gallery/migrations/0002_real_name.sql` | `real_name` column for the teacher only, filled from today's names |
| MODIFY | `Deploy/gallery/src/db.rs` | `real_name` on `User`/`StudentRow`; `create_students` takes both names; `set_names` |
| MODIFY | `Deploy/gallery/src/guard.rs` | `X-Robots-Tag: noindex` on every response from both hosts |
| MODIFY | `Deploy/gallery/src/strings.rs` | Nickname labels and hint, rename button, copy-link texts, signed-out sign-in link, login welcome line |
| MODIFY | `Deploy/gallery/static/gallery.js` | Copy-link click: write the link to the clipboard, say so; on failure, select the visible link |
| MODIFY | `Deploy/gallery/static/gallery.css` | Signed-out header and copy-link control, in the v0.19 look |
| MODIFY | `Deploy/gallery/tests/routing.rs` | `Open` arm in the session test; header test's redirect case moves to `/upload`; `X-Robots-Tag` on both hosts |
| MODIFY | `Deploy/gallery/tests/games.rs` | Signed-out browse and play; copy link carries the gallery address |
| MODIFY | `Deploy/gallery/tests/teacher.rs` | Rename: works, cleans, refuses empty and non-students, escapes, shows on cards |
| MODIFY | `Deploy/gallery/tests/look.rs` | Snapshots of the signed-out list, play view and login; the copy-link wires |
| MODIFY | `Deploy/gallery/tests/password.rs` | "Session is gone" probes move from `GET /` to `GET /upload` |
| MODIFY | `Deploy/gallery/tests/sign_in.rs` | The same for the idle and max-age probes; sign-out now lands on `/` |
| MODIFY | `Deploy/gallery/src/play.rs` | Doc comment only: `/enter`'s frame rule keeps games in the sandbox; it no longer keeps out people outside the class |

---

## Steps

### Step 1: Open access
- **MODIFY `app.rs`**: add `Access::Open` ("anyone; a session, if there is one, is attached"). `GET /`, `GET /shots/{file}`, `GET /play/{id}` → `Open`. Every other route keeps its rule. Add `POST /teacher/students/{id}/rename` → `Teacher`.
- **MODIFY `session.rs`**: in `require_access`, a missing session on an `Open` route continues without `CurrentUser`. With a session, `CurrentUser` and `SessionHash` are inserted as today, so the signed-in header and idle-touch behave identically. Update the doc comments.
- `/login` stays `Public` (it already reads the session itself).
- `post_logout` redirects to `/` (public now) instead of `/login`.
- Update the stale doc comments: `session.rs` module and `require_access`, `pages.rs` `shell`, `gallery.rs` module, `play.rs` `enter`.
- **Verify:** `grep -n "Open, " src/app.rs` prints exactly the three route lines.

### Step 2: Signed-out pages
- **MODIFY `pages.rs`**: `shell` with `user = None` renders the header with the logo, 게임 목록 and a **로그인** link to `/login`. There is no upload, password or sign-out link, and no shared-laptop note. The signed-in header is unchanged. Error pages (`not_found(None)`, `forbidden(None)`) get this header too. The router's 404 fallback (`app.rs:136`) becomes a small handler that reads the session through `session::current_user`, as `login_page` does, so a signed-in student who mistypes an address still sees their own header, not 로그인. `guard.rs:43`'s cross-origin 403 keeps `None`, because no student navigates to it. The login page also gets the signed-out header, with a new `Nav::Login`, which marks 로그인 as current (`aria-current`) there. The look check confirms that the header sits cleanly above the login hero, without a doubled-up logo.
- **MODIFY `gallery.rs`**:
  - `list` takes `Option<Extension<CurrentUser>>`. Signed out, it shows only 모든 게임 (no "My games" section, no add card, no remove links). Signed in, nothing changes.
  - `card` takes `Option<&User>`. `may_remove` is false without a user.
  - `play_view` takes the optional user and signs the 60 s enter grant for anyone. `play.rs` is unchanged: `/enter` still accepts only a frame load from the same site with a valid grant, so a games-host link copied out of the frame still doesn't play.
  - **Copy link:** in the play bar, a read-only text field holding `{gallery_origin}/play/{id}` (`#share-url`) and a **링크 복사** button (`#copy-link`, `data-msg-copied`, `data-msg-failed`). It is shown to everyone, so a relative can pass it on too.
- **MODIFY `gallery.js`**: on `#copy-link` click, run `navigator.clipboard.writeText(url)` and put the button's text to 복사했어요! for 2 s. On rejection or a missing API, select the text in `#share-url` and say 링크를 선택했어요. 복사해서 보내 주세요. The wording names no key, because Ctrl and Cmd differ. The script still never listens for messages.
- **MODIFY `strings.rs`**: `COPY_LINK`, `LINK_COPIED`, `LINK_SELECTED`, `NAV_LOGIN` (로그인). `LOGIN_WELCOME` becomes 게임을 올리려면 로그인하세요 (signing in is no longer needed to play). `LOGIN_HINT` stays.
- **MODIFY `gallery.css`**: style the signed-out header (the 로그인 link styled like the nav) and the copy-link field and button in the play bar. The field is narrow on desktop and wraps under the title at 390 px with no horizontal overflow.
- **Verify:** Step 5 tests and Step 6 look check.

### Step 3: Noindex
- **MODIFY `guard.rs`**: `gallery_layer` and `games_layer` both set `X-Robots-Tag: noindex, nofollow` on every response they wrap (pages, screenshots, build files, errors). The unknown-host empty 404 isn't wrapped and needs none. No `robots.txt`: a crawler blocked by it never sees the noindex.
- **Verify:** routing tests in Step 5.

### Step 4: Real names and nicknames
Operator decision (2026-11-03): **the gallery keeps each student's real name, for the teacher only.** `display_name` becomes the public nickname. A new `real_name` column holds the name the teacher knows the student by.
- **NEW `migrations/0002_real_name.sql`**:
  - `ALTER TABLE users ADD COLUMN real_name TEXT NOT NULL DEFAULT '';`
  - `UPDATE users SET real_name = display_name WHERE role = 'student';`
  - Today's names are real names, so every existing student keeps theirs, and their nickname starts as the same text until the teacher renames it. sqlx's migrator runs this once at start (`db.rs:129`). A restored older backup gets it on the next start too.
- **MODIFY `db.rs`**:
  - `User` and `StudentRow` gain `real_name`. Only `students()` and `user_by_id` (the teacher pages) need it; the game-list query keeps selecting `display_name` only, so no public query can carry it.
  - `create_students` takes `(login, real_name, nickname, hash)` and inserts both.
  - New `set_names(user_id, real_name, nickname) -> Result<()>`: one `UPDATE` of both columns. Removing a student deletes their games, so both names stay editable. That also covers a nickname the migration copied in as a "real name".
- **MODIFY `teacher.rs`**:
  - **Adding students:** a line is `ID, 이름, 별명`, split at the first two commas or tabs, so a nickname may contain commas and a real name may not. Both names are cleaned with `clean_label(…, config.name_max_chars)`, and both are required (`LineStatus::NoName` / new `NoNickname`). A missing nickname never falls back to the real name, so a real name can't be published by leaving the nickname out. The add-one form gets a third field, 별명. The preview table shows ID, 이름, 별명, 상태.
  - **Students table:** columns ID, 이름 and 별명, 게임 수, then reset and remove. 이름 and 별명 are one inline `POST /teacher/students/{id}/rename` form: two prefilled inputs, `real_name` and `name`, and a **바꾸기** button. Above the table goes the hint "별명은 갤러리에서 누구나 볼 수 있어요. 이름은 선생님만 봐요."
  - **Rename handler:** same `student()` target rule as reset (students only, else 404). It runs `clean_label` on both names, and both are required. An empty one answers 400 with `pages::message` (`LINE_NO_NAME` or `LINE_NO_NICKNAME`), and nothing changes. Success logs `student renamed` (login and by, never the names) and answers 303 to `/teacher`.
  - **Slips and remove confirm:** the slips print the real name with the nickname beside it, so the teacher hands each one to the right student. The remove-account confirm names both.
- **MODIFY `strings.rs`**: `COL_NAME` stays 이름. New: `COL_NICKNAME` 별명, `RENAME` 바꾸기, `NAMES_HINT`, `LINE_NO_NICKNAME` 별명이 없어요. `PASTE_HINT` becomes 한 줄에 한 명: 아이디, 이름, 별명. `remove_account_confirm` takes both names.
- The teacher account made with `gallery teacher create` gets `real_name = ''` (the column default). Its display name is unchanged and public on the teacher's games; the operator checks it at rollout.
- **Verify:** Step 5 tests.

### Step 5: Tests
- **`routing.rs`**:
  - `every_non_public_route_needs_a_session` gains an `Open` arm. Without a cookie, an `Open` route is never 401 or 303 to `/login`: `/` is 200, and `/play/1` and `/shots/…` are 404 for a missing game, not a redirect.
  - `every_signed_in_page_shows_the_name_and_sign_out` already covers `Open` routes with a cookie (it filters `!= Public`).
  - `gallery_headers_are_on_every_kind_of_response` takes its 303 from `GET /upload` without a cookie, and asserts `x-robots-tag` contains `noindex` on all four responses.
  - `the_games_host_only_serves` (or a sibling) asserts `x-robots-tag` on a games-host response.
- The headers test's `missing` probe (a 404 fetched with a cookie) asserts the signed-in name and 로그아웃, and no `/login` link. Without a cookie, the same 404 shows the 로그인 link.
- **`games.rs`**:
  - New test `anyone_browses_and_plays_without_signing_in`. Signed out, `/` lists every live game with its title and nickname. The page has no `MY_GAMES`, no `/upload` link, no remove link and no 로그아웃, but has a `/login` link. `/shots/<id>.png?v=…` is 200 PNG. `/play/<id>` frames `…/enter/<id>?grant=` with a grant that verifies, and that grant opens `/enter` as a frame (gate cookie set). Signed out, `/upload`, `/password`, `/games/<id>/remove` and `/teacher` all 303 to `/login`, and `POST /upload` returns 401 JSON.
  - The play view test also asserts `#copy-link` and `#share-url` with value `https://suwon.orcarin.com/play/<id>`.
- **`teacher.rs`**:
  - New test `real_names_never_leave_the_teacher_pages`. Create a student with real name 김실명 and nickname 별빛, and give them a game. 김실명 appears on `/teacher`, on the slips and on the remove confirm. It appears in no response a student or a signed-out visitor can fetch: `/` (signed out and as a student), `/play/<id>`, `/upload` and `/password`.
- New test: a paste line missing the nickname (`kim, 김실명`) is marked 별명이 없어요 and creates nothing. A three-field line creates the student with both names stored apart.
- `db.rs` unit test: the 0002 migration on a database holding a student made with the 0001 schema copies `display_name` into `real_name`, and leaves the teacher's empty.
- Existing teacher tests that paste `ID, name` lines (`tests/teacher.rs`, `tests/look.rs`) move to three fields. `tests/support/mod.rs` is unchanged: its `student()` and `teacher()` helpers go through `db.create_user`, which leaves `real_name` at the column default `''`. The real-name tests create their students through the teacher's paste route.
- **`src/teacher.rs` unit tests:**
  - `pasted_lines_are_checked` moves to three-field lines, plus one two-field line that must come out `NoNickname`.
  - `a_name_may_contain_commas` becomes `a_nickname_may_contain_commas`: `"kim, 김민수, 별, 빛"` gives real name 김민수 and nickname "별, 빛".
- New test `rename_changes_the_nickname_everywhere`. It renames both names at once. The new real name shows on `/teacher` only. Renaming to a padded, multi-space name stores it cleaned. The card on `/` and the play view's 만듦 show the new nickname, and the old one is gone. An empty or whitespace real name or nickname is refused, and both names stay as they were. Renaming the teacher's id or an unknown id gives 404. A hostile nickname comes out escaped on the teacher page and on the card.
  - `students_are_refused_on_every_teacher_route` and the POST-origin test pick up the new route from the table automatically.
- **"The session is gone" probes:** six tests check that a session ended by redirecting `GET /` to `/login`: `password.rs:131-132`, `sign_in.rs:129,131,141` and `teacher.rs:98,115`. `/` is now `Open`, so it answers 200 even after the session ends. Each probe moves to `GET /upload`, which still asserts the 303 to `/login`; asserting a 200 instead would test nothing. `sign_in.rs`'s sign-out test also gains an assertion that the response carries `location: /`.
- **`look.rs`**: add snapshots `list-public` (`/` without a cookie), `play-public` and the teacher page with the nickname hint. Assert the copy-link ids are on `play` and `play-public`, and no inline style anywhere.
- **Verify:** `cargo test` and `cargo clippy --all-targets -- -D warnings` in `Deploy/gallery/` pass.

### Step 6: Look check
- As in the gallery-look plan: serve `TestResults/gallery-look/` over localhost, take headless Chrome screenshots at 1280 × 900 and 390 × 844 of `list-public`, `play-public`, `play`, `login` and `teacher`, plus both play pages at 1366 × 768, and look at each. Run the CDP check on those pages: no horizontal overflow at 390 px, Galmuri loaded, and the play frame within 1% of 16:9 and inside the window with the copy-link control in the bar.
- In headless Chrome on `play-public` (CDP, clipboard read and write permissions granted), click `#copy-link` with `Input.dispatchMouseEvent` (a real click; a scripted `el.click()` has no user gesture and would always take the fallback): the clipboard holds the play address and the button says 복사했어요!. With the permission denied, the field's text is selected and the fallback message shows.

### Step 7: Run tests
- `cargo test` and `cargo clippy --all-targets -- -D warnings` in `Deploy/gallery/`. Fix failures before going on.

### Step 8: Code Review
- Run the code-review agent (`.claude/agents/workflow/code-review.md`) on all changed files, and fix HIGH/MED.
- Then re-run Step 7, before anything is deployed: this is an access change on an internet-facing server, so a review finding must never be live.

### Step 9: Deploy and live check
- **Two deploys, so no real name is ever public.** Both are built from the reviewed working tree; the commit comes in Post-Plan Cleanup, as in earlier gallery plans.
  1. **Nicknames first.** Temporarily set the three routes back to `Page` (a local edit, never committed) and build a release. Just before installing it, run the nightly backup job now, `launchctl kickstart gui/$(id -u)/com.orcapafea.daedalus.gallery-backup`, so its own paths are used (no quoting of "Application Support"). Then confirm that today's `db/gallery-YYYY-MM-DD.db` in `~/Library/Application Support/daedalus-gallery-backups/` has a modification time of just now. Copies are named by date, so a same-day run replaces the 03:30 one; check the time, not just that the file exists. Deploy 1 applies migration 0002, and sqlx refuses to open a database that has a migration the binary doesn't know. Rolling back past deploy 1 therefore means the v0.19 binary plus that backup's database; the new binary with the routes set back to `Page` needs no restore. The gallery stays sign-in only, and the teacher page can rename.
  2. **Rename.** After the migration, every student's nickname still equals their real name. The operator:
     - gives each student a nickname;
     - checks the teacher's own display name;
     - checks every game title, and glances at every card's screenshot (a level can spell a name in tiles), for a real name. The teacher can remove such a game.

     Claude waits for the operator's word.
  3. **Go public.** Restore the three routes, so that `git diff src/app.rs` shows only this plan's change, then rebuild and redeploy.
- Live check in Chrome through the tunnel, in a window that isn't signed in:
  - The list shows every game, with no My games section.
  - A game plays inside the frame (keyboard focus, full screen still 16:9).
  - 링크 복사 copies `https://suwon.orcarin.com/play/<id>`, and that link opened in a fresh signed-out window plays.
  - `https://suwon-play.orcarin.com/g/<id>/` opened directly shows the reopen message.
  - `/upload` sends to sign-in.
  - `curl -sI https://suwon.orcarin.com/` shows `x-robots-tag: noindex, nofollow`.
- Signed in as a test student: upload an update, then remove it. Signed in as the teacher: rename a test student, and the card shows the new nickname.

### Step 10 (Final): Verify
- Walk the Completion Criteria.

---

## Completion Criteria

- [ ] `cargo test` and `cargo clippy --all-targets -- -D warnings` pass in `Deploy/gallery/`
- [ ] Exactly `/`, `/shots/{file}`, `/play/{id}` are `Access::Open`. Every upload, remove, password, logout and teacher route still requires a session (the routing test iterates the table).
- [ ] Signed out, the list and play view render with the signed-out header. The play view's grant verifies and opens `/enter` as a frame. A games-host page opened directly still refuses.
- [ ] Signed in, the list, play view and header are unchanged (existing `look.rs` and `games.rs` assertions still pass).
- [ ] `X-Robots-Tag: noindex, nofollow` is on every response from both hosts (tests), and live (`curl -sI`).
- [ ] The teacher renames a student, the new nickname shows on cards and play view, and empty or non-student renames are refused (tests).
- [ ] The copy-link button copies the gallery play address, and the fallback selects it (headless Chrome check, then live).
- [ ] The look check's screenshots of `list-public`, `play-public`, `play`, `login` and `teacher` at desktop and phone width were inspected: no overflow at 390 px, Galmuri loaded.
- [ ] Live: the nickname deploy went first and the operator confirmed the renames before the public deploy. A signed-out window browses and plays, and a copied link plays in a fresh window.
- [ ] The six session-gone probes still assert the 303 to `/login`, via `/upload`.
- [ ] The 0002 migration keeps every existing student's name as `real_name`. A real name appears only on teacher pages (test). A student can't be created without a nickname.

---

## Notes to GDD / Open Risks

**GDD (Post-Plan Cleanup, version v0.20):**
- `GDD.md`: a new **Decided (v0.20)** line saying the gallery is public to browse and play, sign-in only to upload and manage, students appear by nickname, and pages are noindex. It replaces v0.12's "only signed-in users can see or play anything" and v0.16's "display name, set by the teacher".
- `Glossary.md`: "Class gallery" (line 20) no longer says signed-in students play; "Display name" (line 25) becomes **Nickname** (Draft), public, set and changed by the teacher.
- `content.md:31`: "which only the teacher and the students can enter" becomes: anyone can view the gallery; only the class uploads; the teacher's remove is still the takedown path, now for public content.
- `systems.md` § Share:
  - Share window: "the card shows the student's display name" → nickname.
  - Safety: "a copied play link opened outside the gallery doesn't enter" becomes "a games-host link opened outside the gallery's frame doesn't enter; the gallery's own play link works for anyone".
  - Intro: "a website only the teacher and the students can enter" → anyone can browse and play, and only the class signs in.
  - Sign in: drop "Only signed-in users see or play anything."
  - Play: display name → **nickname** the teacher sets and can change, public. Signed-out viewers see no My games. Add the copy-link button.
  - Teacher: renames students.
  - Safety: games-share-saves risk re-accepted for a public gallery, since uploaders are still only the class.
- `platforms.md` § External Services: add the `Open` rule and `X-Robots-Tag`.
- `decisions.md`: an ADR row (public gallery, nickname, noindex).

**Decided by the operator (2026-11-03):** store the student's real name, for the teacher only (Step 4). This replaces the critic's open question about losing real names.
- Reviewer recommends: accept losing real names, and keep no real names on the server. Reason: a name that is never stored can't leak through a bug or a breach, and it avoids a schema change (round 1, R1).
- Author's counter-view: real names are already on the server today, in `display_name`, so the new column adds no new exposure. A test pins it to the teacher's pages. The operator chose to keep them so slips and confirms identify the student.
- `Glossary.md` gains **Real name** (Draft): the name the teacher knows a student by, shown only on the teacher's pages.
- `decisions.md`'s ADR row includes this decision.

**Risks:**
- **The grant is open to anyone.** Any client can fetch `/play/{id}`, take the grant and call `/enter` with a forged `Sec-Fetch-Dest: iframe` (curl). That gets the build files, which are public anyway now. The frame rule is a sandbox rule, not access control, and the ADR says so.
- **Moderation:** anything a student uploads is now public at once. The teacher's remove is the only control (unchanged). The operator accepted it with the decision.
- **Load:** each play downloads about 7–10 MB from the Mac Mini through the tunnel. Accepted (decision 4).
- **Saves shared across games:** now in visitors' browsers too. Uploaders are still only the class, so the v0.14 acceptance stands, restated in the ADR.

---

## Post-Plan Cleanup
After code review passes:
1. Append execution trailers to this plan file:
   - `**Approval:** User confirmed YYYY-MM-DD.`
   - `**Execution:** COMPLETE YYYY-MM-DD.`
   - `**Verification:** <one-line summary of what passed>`
   - `**Version note:** <optional>`
2. Move this plan to `Documents/Plans/archive/`.
3. GDD and `decisions.md` per Notes above. Add a Changelog v0.20 entry and bump `GDD.md` to v0.20.
4. **Deferred items:** file any leftover in `Deploy`; delete Deferred entries whose trigger fired.
5. Commit all plan work: `v0.20: gallery public — anyone browses and plays; students sign in to upload; nicknames`.

## Workflow
Execute steps sequentially. Proceed to the next step without waiting for user approval. Only stop if blocked. If blocked, stop and report: what step, what was attempted, what failed.

**Critic (2026-11-03), folded in:** HIGH: six "session is gone" probes on `GET /` (password.rs, sign_in.rs, teacher.rs) move to `/upload`; both test files are now in the manifest. MED: the login page gets the signed-out header with `Nav::Login`, checked by the look check. Real-name loss after a rename is recorded as an open question for the operator. Two-deploy rollout, so no real name is ever public (also checks the teacher's name and game titles). Glossary (Class gallery, Display name → Nickname), the GDD v0.16 line and systems.md's Share window and Safety lines are added to the GDD notes. LOW: stale doc comments listed (play.rs, pages.rs, session.rs, gallery.rs); the grant is open to anyone and the frame rule is a sandbox, not access control (Design choice and Risks); copy fallback text names no key, and the CDP click uses `Input.dispatchMouseEvent`; logout goes to `/`; an empty rename answers 400 through `pages::message`; the Step 1 grep is fixed; X-Robots-Tag wording says "every response they wrap"; Overview cut to about 150 words. Not taken: screenshot `Cache-Control: public` (not needed, no Cloudflare cache rule).

---

## Review round 1


# Review round 1: gallery-public

## What I could run

I had shell access to the Mac mini repo folder (`device_bash`: `cat`, `grep`, `sed`, `git log` over `Deploy/gallery/`, `Documents/`). HEAD is `018d4f8` (v0.19), matching the plan's dependency. I did **not** run `cargo build`, `cargo test`, clippy, the look check or the live server. Everything below is checked against source text only.

## What I verified (holds)

- `app.rs`: the route table and `Access` enum are as described. `/`, `/shots/{file}`, `/play/{id}` are `Page` today; `gallery_router` sends every non-`Public` rule through `session::require_access`, so a new `Open` arm there is the only place it needs handling. `grep -n "Open, " src/app.rs` will match only route lines (the enum variant is `Open,` + newline).
- `session.rs:77-96` `require_access`: a missing session returns before any `CurrentUser` insert, so the plan's "continue without `CurrentUser` on `Open`" is a one-arm change. `login_page` already redirects a signed-in browser to `/`. `post_logout` (line 387) goes to `/login` today.
- `pages.rs` `shell`: the header renders only when `user` is `Some`; `login_form` calls `pages::page(…, None, None, …)`, so a `Nav::Login` and a signed-out header branch are needed exactly as Step 2 says.
- `gallery.rs`: `list`, `card`, `may_remove`, `play_view` all take a non-optional user today; `play_view` signs the grant from game id + time only (no user), so signing it for anyone is a no-op change. Screenshot `Cache-Control: private, …` is fine for a public image.
- `play.rs` `enter`: only frame-dest + same-site + valid grant; unchanged behaviour is correct, and the plan's Risks paragraph (curl can forge `Sec-Fetch-Dest`) is accurate.
- `guard.rs`: both layers wrap fallbacks too, so "every response they wrap" is right; `dispatch`'s unknown-host 404 is outside both.
- `teacher.rs`: `student()` (line 260) is the students-only 404 rule; `clean_label` + `config.name_max_chars` (20) is the create path's rule; `LINE_NO_NAME`, `COL_NAME`, `PASTE_HINT` exist in `strings.rs:141-153`.
- Every "session is gone" probe citation checks out: `password.rs:131-132`, `sign_in.rs:129,131,141`, `teacher.rs:98,115` all assert `location: /login` on `GET /`. I found no other test that relies on a signed-out `GET /`, `/play` or `/shots` redirecting (`games.rs`, `look.rs`, `play.rs`, `upload.rs` all pass a cookie).
- `routing.rs`: `every_signed_in_page_shows_the_name_and_sign_out` filters `!= Public`, so `Open` is covered with a cookie; `gallery_headers_are_on_every_kind_of_response` takes its 303 from `GET /` without a cookie, so it does need to move.
- `config.rs:55`: `gallery_origin` is `https://suwon.orcarin.com`, so the copy-link value the test expects is right.
- Glossary lines 20 and 25, GDD.md v0.12 and v0.16 lines, systems.md 197/200/206/208/211: all exist and say what the Notes say they say.

## Findings

### F1 — blocking — design — Step 10 (code review) runs after the public deploy
**Location:** Steps 8 → 10.
**Defect:** The plan makes the gallery reachable by anyone on the internet in Step 8.3, then runs the code-review agent in Step 10 and "fixes HIGH/MED". This change is an access-control change on an internet-facing server; the review is exactly the step most likely to find a HIGH in it (an `Open` arm that lets something through, a handler still assuming `CurrentUser`). As written, such a finding is live and public before anyone looks for it, and its fix needs a redeploy the plan never schedules, so the live binary can end up differing from the reviewed, committed code.
**Fix:** move the code review (and its fixes, plus a re-run of Step 7) to before Step 8. Step 9's walk of the criteria can stay after.

### F2 — non-blocking — accuracy — Step 8.3 "rebuild from the committed tree"
**Location:** Step 8.3; Post-Plan Cleanup item 5.
**Defect:** Nothing is committed until Post-Plan Cleanup item 5, which runs after Step 8. At 8.3 there is no committed tree with this work in it, so the instruction can't be followed literally; the real intent is "the working tree with the three routes restored". Earlier gallery plans (gallery-change-password Step 6) also deploy from the working tree, so that's the pattern; the wording just needs to match it, or the commit needs to come first.
**Fix:** say "rebuild after restoring the three routes (`git diff --stat src/app.rs` shows only this plan's change)" or move the commit ahead of the deploy.

### F3 — non-blocking — accuracy — `content.md` § Student Content is missing from the GDD notes
**Location:** Notes to GDD.
**Defect:** `Documents/GDD/content.md:31` says the class gallery is one "which only the teacher and the students can enter". After this plan that is false, and it is the line that frames the takedown path. The Notes list GDD.md, Glossary, systems.md, platforms.md and decisions.md but not content.md. Also, the Notes have two separate bullets both headed `systems.md` § Share; merging them would make the list easier to follow.
**Fix:** add content.md:31 to the Notes (the gallery is public to view; uploaders are still only the class; teacher remove is still the takedown path, now for public content).

### F4 — non-blocking — design — the rename pass doesn't cover screenshots
**Location:** Step 8.2.
**Defect:** The point of the two-deploy rollout is that no real name is ever public. Step 8.2 checks student names, the teacher's name and game titles. But each card's screenshot is now served to anyone (`/shots/{file}` becomes `Open`), and it is a frame of the student's own level, which can contain the student's name written in tiles or a sign. Nothing in the plan looks at them.
**Fix:** add "glance at each card's screenshot" to the operator's 8.2 pass (the teacher can remove such a game, as for titles).

### F5 — non-blocking — design — the signed-out header on the gallery's 404 fallback shows 로그인 to signed-in users
**Location:** Step 2, "Error pages (`not_found(None)`, `forbidden(None)`) get this header too."
**Defect:** The router fallback (`app.rs` `.fallback(|| async { pages::not_found(None) })`) and `gallery_layer`'s cross-origin refusal (`pages::forbidden(None)`) never see the session. Today they render with no header. With this plan, a signed-in student who mistypes a URL gets the signed-out header with a 로그인 link, which looks like they've been signed out. Harmless (the link bounces them to `/`), but it's a visible inconsistency that `routing.rs`'s `missing` probe (with a cookie) will show and no test asserts against.
**Fix:** either accept and say so, or have those two `None` call sites render a header with only the logo and 게임 목록 (no 로그인), so it is correct whether or not the visitor is signed in.

### R1 — recommendation — the open question on losing real names after a rename
**Location:** Notes, "Open question for the operator".
**Recommendation:** accept, as the plan proposes: the teacher keeps their own ID → student list off the server. **Reason:** a real name that is never stored on an internet-facing server can't leak through a bug or a breach, which a teacher-only column could; and it avoids a schema change and its upgrade step. The cost (slips and confirms show nicknames) is small because the login ID is printed on every slip.

## Not verified

- That `Option<Extension<CurrentUser>>` compiles as an extractor under the locked axum 0.8 version (I believe `Extension` implements `OptionalFromRequestParts` in 0.8; the compiler will settle it).
- The CDP clipboard check: reading the clipboard back in headless Chrome needs the read permission granted as well as write. Not a finding; the Author will see it when running Step 6.
- No test, build, look check or live check was run.

## Reviewer's view

I would build this plan once F1 is fixed. The access change itself is small and well-placed (one new `Access` arm in the one place every route goes through), the six probe migrations and the test changes all check out against the code, and the two-deploy rollout is the right way to keep real names off the internet. The only thing I'd stop on is the order: the code review has to come before the public deploy (F1). F2–F5 are wording and small gaps to fold in, and R1 answers the open question.

---

## Review round 2


# Review round 2: gallery-public

## What I could run

The same as round 1: shell access to the Mac mini repo folder (`cat`, `grep`, `sed` over `Deploy/gallery/` and the plan). I did not run `cargo`, the look check or the live server. This is a delta review against round 1, plus a full read of the rewritten Step 4 and the parts of Step 5 and Step 9 that depend on it, since the operator's real-name decision landed after my round 1 read.

## Round 1 findings

- **F1 (blocking): resolved.** Step 8 is now Code Review with a re-run of Step 7, and Step 9 (deploy) comes after it. The headings run 1–10 in order.
- **F2: resolved.** Step 9 says both builds come from the reviewed working tree, and 9.3 checks `git diff src/app.rs`.
- **F3: resolved.** `content.md:31` is in the Notes; the two systems.md bullets are merged.
- **F4: resolved.** Step 9.2 includes screenshots.
- **F5: resolved, with a different fix that I agree with.** A fallback handler that reads the session through `session::current_user` is better than the logo-only header I suggested. Keeping `guard.rs`'s cross-origin 403 at `None` is fine. The new `missing`-probe assertion covers it.
- **R1: withdrawn.** The operator settled the question (store real names, teacher only) before my round landed, so my recommendation answered a question that was already closed. The Author's counter-view is correct: `display_name` already holds real names on the server today, so a teacher-only column adds no new exposure. Nothing to fold in.

## New findings (rewritten Step 4)

### F6 — blocking — design — the only way to fix a real name deletes the student's games
**Location:** Step 4, `db.rs` bullet ("A real name typed wrong is fixed by removing and re-adding the student; no editing it") and the 0002 migration.
**Defect:** Removing a student is `teacher::remove` (`teacher.rs`, the handler after `remove_confirm`): it calls `remove_game` on every live game the student owns, then `delete_user`. So the plan's only fix for a wrong real name destroys that student's games and their upload history. In this project, losing student work is the worst bug class (CLAUDE.md § Student projects). And the migration makes a wrong real name likely on day one: `UPDATE users SET real_name = display_name WHERE role = 'student'` assumes every current name is a real name. A teacher who already typed a nickname for some student gets that nickname stored as the real name, with no fix except deleting the student's games.
**Fix:** let the teacher edit the real name too. The cheapest version adds a second input (`real_name`) to the same inline rename form, so one `POST …/rename` handler sets both names. It uses the same `clean_label` and both names are required (400 + `LINE_NO_NAME` / `LINE_NO_NICKNAME`). It's one more `UPDATE` column in `set_nickname` (call it `set_names`) and one more assertion in `rename_changes_the_nickname_everywhere`. Then drop the "remove and re-add" sentence.

### F7 — non-blocking — design — after deploy 1, the v0.19 binary can't start
**Location:** Step 9.1; Step 4's migration.
**Defect:** `db.rs` runs `sqlx::migrate!("./migrations").run(&pool)` on every open (sqlx 0.8, `Cargo.toml:15`). sqlx's migrator, by default, refuses to run when the database records a migration the binary doesn't have (`MigrateError::VersionMissing`). After deploy 1 applies `0002_real_name`, reinstalling the v0.19 binary to roll back makes the server, `gallery backup` and `gallery teacher …` all fail to open the database. The plan has no rollback note and takes no backup before deploy 1. (I couldn't run this; it's sqlx's documented default. Please confirm against the locked version.)
**Fix:** in 9.1, run `gallery backup` just before installing the new binary. Then add one line: "rollback past deploy 1 = the v0.19 binary plus that backup's database copy" (or the new binary with the routes set back to `Page`, which needs no restore).

### F8 — non-blocking — accuracy — the three-field paste breaks unit tests the plan doesn't list
**Location:** Step 5, "Existing teacher tests that paste `ID, name` lines (`teacher.rs`, `look.rs`, `support/`) move to three fields"; File Manifest.
**Defect:** `src/teacher.rs`'s own unit tests depend on the two-field split at `parse_lines` (`teacher.rs:60`, `l.find([',', '\t'])`):
- `a_name_may_contain_commas` asserts `"kim, 김, 민수"` → name `"김, 민수"`. Under "split at the first two commas" the same line gives real name 김 and nickname 민수, so the test fails. Its premise needs restating: a nickname may still contain commas, and a real name may not.
- `pasted_lines_are_checked` uses two-field lines (`kim, 김민수`, `park,`), which now all come out `NoNickname`.

Step 5's `teacher.rs` reads as `tests/teacher.rs`, so these in-module tests aren't covered by it. Also, `tests/support/mod.rs` is named in Step 5 but missing from the File Manifest.
**Fix:** name both unit tests in Step 5 with their new expectations, and add `tests/support/mod.rs` to the Manifest.

## What I verified this round

- `migrations/` holds only `0001_init.sql`; `users` has no `real_name`, so `0002` is the next number. `ALTER TABLE … ADD COLUMN … NOT NULL DEFAULT ''` is valid SQLite.
- `User::from_row` reads by column name from `SELECT *` (`user_by_login`, `user_by_id`), so adding `real_name` to `User` works once the migration has run; `students()` lists columns explicitly and needs `u.real_name` added. That matches the plan.
- `GAME_COLUMNS` (`db.rs:104`) selects only `u.display_name AS owner_name`, so the game list and play view can't carry `real_name`, as Step 4 says.
- `backup::run` opens the database with `Db::open_existing`, which runs the migrator, so "a restored older backup gets it on the next start" holds.
- `create_user` (the teacher CLI) doesn't name `real_name`, so the teacher gets `''` from the column default, as stated.

## Not verified

- The sqlx `VersionMissing` behaviour on the locked version (F7).
- No build, test, look check or live check was run.

## Reviewer's view

The round 1 fixes are all in, and the public-access part of the plan is ready to build. One thing still stops me, and it comes from the new real-name step: the plan's only fix for a wrong real name is deleting the student, and that deletes their games (F6). Letting the rename form edit the real name too is a small change. F7 (back up before the migrating deploy) and F8 (two unit tests and a Manifest line) are quick folds.

---

## Review round 3


# Review round 3: gallery-public

## What I could run

The same as earlier rounds: shell access to the Mac mini repo folder (`grep`, `sed`, `cat`). This is a delta review against round 2. I ran no build, tests, look check or live server.

## Round 2 findings

- **F6 (blocking): resolved.** Step 4 now has `set_names(user_id, real_name, nickname)`: one inline form with two prefilled inputs, both names required. The "remove and re-add" sentence is gone, and the reason is stated (removing a student deletes their games). It also covers a nickname that the migration copied in as a real name. `rename_changes_the_nickname_everywhere` now also renames the real name and checks that it shows on `/teacher` only.
- **F7: resolved.** Step 9.1 backs up just before installing and states both rollback paths. I checked the folders against `Deploy/launchd/com.orcapafea.daedalus.gallery-backup.plist:19-24`, and they match the nightly job. The Author confirmed the sqlx default in sqlx-core 0.8.6, which settles my unverified note.
- **F8: resolved; the Author's rejection of the Manifest half is correct.** `tests/support/mod.rs:155-157` `add_user` goes through `db.create_user`, which doesn't name `real_name`, so it needs no change. I withdraw that half of F8. The two unit tests are now named with their new expectations, and Step 4 states the rule: a nickname may contain commas, a real name may not.

## New findings

### F9 — non-blocking — accuracy — Step 9.1's backup command breaks on the space in "Application Support"
**Location:** Step 9.1, `gallery backup --data ~/Library/Application Support/daedalus-gallery --to ~/Library/Application Support/daedalus-gallery-backups`.
**Defect:** If this is pasted into a shell as written, the unquoted space splits each path into two arguments, so the backup that the rollback depends on is never taken.
**Fix:** quote both paths (`"$HOME/Library/Application Support/…"`), or run the nightly job itself, as the plist header says: `launchctl kickstart gui/$(id -u)/com.orcapafea.daedalus.gallery-backup`. Then check that a fresh dated copy appears.

## Late-pushback test

I asked myself whether "what do you think?" would get anything from me that isn't already in these round files. It wouldn't. The access change goes through the one place every route already passes. The real-name column is reachable only from teacher queries, and a test pins that down. The rollout keeps real names off the public site, and the code review now runs before anything goes public.

## Reviewer's view

I would build this plan as written. Every blocking finding is closed: F1 (the review now runs before the public deploy) and F6 (a real name can be fixed without deleting games). F9 is a quoting fix to fold in at close.
