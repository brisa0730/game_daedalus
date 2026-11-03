---
slug: gallery-public
round: 3
verdict: approved
reviewer: claude-cowork
updated: 2026-11-03T22:29:00+09:00
blocking: 0
recommendations: 0
---

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
