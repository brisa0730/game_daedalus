---
slug: phase5-gallery-server
round: 3
verdict: approved
reviewer: claude-cowork
updated: 2026-11-03T11:46:00+09:00
blocking: 0
recommendations: 0
---

# Round 3: delta review against round 2

## What I could run, verified, and could not

**Could run:** the same read-only shell over the Mac mini's `Daedalus` folder. I read the round 2 dispositions in `log.md` and grepped the prompt's body (above `## Review round 1`) for each fold.

**Verified:**
- **F11 closed.** Step 7 line 270: the throttle is keyed by user id, for existing accounts only, and unknown IDs never create an entry. The § 8 row reads "existing accounts only (one entry per account at most)". The size cap and eviction are gone, so nobody outside can flush the map. The new test (10,001 unknown-ID failures leave a real account's delay and the map size unchanged) covers it.
- **F12 closed.** The restore line (514) deletes `gallery.db-wal` and `gallery.db-shm` before copying.
- **F13 closed.** Line 365 now says "re-checked by the conditional insert (Step 4)" and line 271 says "never a hard lockout". Notes 1 matches.
- **F14 closed.** Line 269 gives the upload route's 401 the JSON body with `login: true`, and `gallery.js` shows it with a `/login` link, backed by a test. Leaving out the pre-send check is acceptable; I offered it as optional.
- No seam from the round 2 folds: keying by user id fits the reset clearing "that login's throttle entry" (look up the id, drop the entry), and removing an account drops its entry.

**Could not verify:** compiling or running anything. The phase isn't built, and every test the prompt names is still to be written.

## Late-pushback test

Asked "what do you think?", I would add one thing that isn't in my earlier rounds. I'm filing it below. It does not block.

### F15 · non-blocking · accuracy · Signing out doesn't close games already opened on that browser
**Location:** Overview ("Nothing can be seen without signing in"); Step 13 (gate cookie 12 h); Notes 3.

Sign-out deletes the gallery session, but the gallery can't clear the games host's `__Host-g_<id>` cookies, because they belong to a different host. So on a shared laptop, the next student can reopen `suwon-play.orcarin.com/g/<id>/` from history for up to 12 h without signing in, and play any game the previous student opened. The harm is small, since these are classmates' games, but the Overview's claim isn't quite true. **Fix:** state it as accepted in Notes 3 and the ADR ("a game opened on a browser stays playable from its history for the gate cookie's lifetime, even after sign-out"), or shorten the gate cookie. The gate only has to outlive loading and reloads, so 1–2 h would do. The Author decides which.

## Reviewer's view

Approved: I would build this as written. Every blocking finding is closed: the dead idle timer (F1), the Argon2 queue lockout (F2) and its throttle-flush follow-on (F11). All non-blocking findings from rounds 1 and 2 are folded too, and F15 is a one-line fold for the close. What's left is execution risk that no document review can settle: the Argon2 timing, the play CSP under real Chrome, and the operator's look at the Korean wording. The prompt already stops for each of these (Steps 5, 22, 23).
