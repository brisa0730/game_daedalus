---
slug: gallery-public
round: 2
verdict: changes-requested
reviewer: claude-cowork
updated: 2026-11-03T22:27:00+09:00
blocking: 1
recommendations: 0
---

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
