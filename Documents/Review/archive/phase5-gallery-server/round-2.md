---
slug: phase5-gallery-server
round: 2
verdict: changes-requested
reviewer: claude-cowork
updated: 2026-11-03T11:47:00+09:00
blocking: 1
recommendations: 0
---

# Round 2: delta review against round 1

## What I could run, verified, and could not

**Could run:** the same read-only shell over the Mac mini's `Daedalus` folder. I read `log.md`'s round 1 dispositions and the changed sections of the prompt (§ Operator decisions 8, Steps 2, 4, 5, 7, 10, 11, 12, 18), and grepped for leftover text from round 1.

**Verified folded as the log says:**
- **F1:** idle 1 h, absolute cap 12 h (table row and Step 7). The three Step 7 tests use real states, and the header carries the 로그아웃 line. Closed.
- **F2:** Step 5 says unknown IDs are never hashed, with the trade stated, a zero-hash test, and the Argon2 row saying "only existing accounts". Closed, but see F11, which this fix opened.
- **F3:** 100 passwords plus deterministic checks. Closed.
- **F4:** `app::routes()` is one table that `router()` is built from, and the Step 2 and Step 7 tests iterate it. Closed.
- **F5:** Step 4 uses a single conditional `INSERT … SELECT … WHERE (…) < 10`. Step 10 states the new-game order, and the concurrency test asserts a 4xx, the message and no folder. Closed, with one leftover line (F13).
- **F6:** leftover `.tmp` files are deleted and the temp name is unique, the mirror keeps missing folders 7 days via `.gone`, and there's a restore line, a "what it protects against" line and tests. Closed, but the restore line has a gap (F12).
- **F7:** `loading="lazy"` with fixed sizes, and the contract is now ≤ 1,000,000 bytes and ≤ 1280 × 720. Closed.
- **F8:** one `max_upload_bytes` = 50,000,000, sent to the page as `data-max-bytes`, with a test. Closed.
- **F9:** a reset clears the throttle entry, `known_browsers` PK is `(token_hash, user_id)`, a second-student test exists, and the residual risk is stated. Closed.
- **F10:** check 5 now requires `daedalus.json`, and a mutator was added. Closed.
- **R1–R3:** recorded beside their questions.

**Could not verify:** that any of this compiles or passes. Nothing has been built, as the log says.

---

## Findings

### F11 · blocking · design · The F2 fix made the throttle map free to flush, which reopens F2
**Location:** Step 7 Throttle ("keyed by the normalized login text **whether or not the account exists**" and "holds at most 10,000 (oldest dropped first)"); § Operator decisions 8 (Throttle map row); Step 5 (unknown IDs answered at once).

Before round 1, every unknown-ID attempt cost an Argon2 hash, so filling the 10,000-entry map was slow. Now an unknown ID is answered at once with no hash, but it still makes a map entry. A script can send 10,000 made-up IDs for almost nothing, push every real account's entry out, and start each real ID again at the bottom of its 1 → 30 s ladder. In each ~31 s cycle an evicted ID allows attempts at about 0, 1, 3, 7, 15 and 31 s (5–6 attempts), against 1 per 30 s under the cap. So:
- The steady hashing load becomes about 5 × accounts / 30 per second instead of accounts / 30. Argon2 handles roughly 20–40 per second, so from about 120 accounts (one gallery for every class) the two permits fill up again and real sign-ins time out with "잠시 후". That's F2's whole-class lockout again.
- Online guessing gets about 5 times faster per account.

**Fix:** unknown IDs never touch the throttle. They aren't hashed, so there's nothing to slow down. The map then holds at most one entry per real account, and eviction can't be triggered from outside. Change "whether or not the account exists" to "for existing accounts only", drop the cap and eviction or keep them only as an invariant guard, update the § 8 row, and add a test: 10,001 unknown-ID failures leave a real account's pending delay unchanged.

### F12 · non-blocking · accuracy · Restore can mix the old database with the live write-ahead log
**Location:** Step 18 Restore; Notes 3.

The pool runs in WAL mode (Step 4). The restore line copies `gallery-YYYY-MM-DD.db` over `gallery.db` after `bootout`. If the server didn't close its pool cleanly (launchd sends SIGTERM, and the prompt never mentions a graceful shutdown), `gallery.db-wal` and `gallery.db-shm` from the live database are still in the data folder. When SQLite next opens the database, it replays that log on top of the restored file. The result is a corrupt database or a silent mix of the two. **Fix:** the restore line deletes `gallery.db-wal` and `gallery.db-shm` before copying, or `serve` closes the pool on SIGTERM. The first is one clause and doesn't depend on the second.

### F13 · non-blocking · accuracy · Two stale lines after the fold
**Location:** Step 10 caps bullet (line 364): "re-checked inside the insert transaction". Since F5 it's a single conditional INSERT, as Step 4 says. Step 7 throttle (line 271): "There is never a lockout". Three lines later the stated residual risk describes a script holding an ID at the cap, and Notes 1 was softened to "without a hard lockout". **Fix:** "re-checked by the conditional insert (Step 4)", and "never a hard lockout".

### F14 · non-blocking · design · With a 1-hour idle timeout, an upload from a long-open tab gets an unreadable 401
**Location:** Step 7 (`SignedIn` → 401 on the upload route); Step 10 Response (JSON shape); Step 11 ("Shows the server's message").

F1's fix makes this common. A student opens the upload page, spends most of a class period in Unity on Share my game, and then presses Upload: the session has idled out. The extractor answers 401, but the prompt gives that 401 no body. `gallery.js` "shows the server's message", so it gets nothing or a JSON parse error, after a 50 MB upload. **Fix:** the upload route's 401 uses the Step 10 JSON shape with a Korean "로그인이 끝났어요. 다시 로그인한 뒤 올려 주세요" (or similar) message. `gallery.js` shows it with a link to `/login`, and a test asserts the 401 body. A cheaper check before sending, such as a `HEAD` on a signed-in route, would also save the wasted upload. That part is optional.

---

## Reviewer's view

Every round 1 finding is folded correctly. I'd build this once F11 is fixed: the F2 fix left the throttle map open to a free flush, and the fix is one sentence plus one test. F12–F14 are short folds. Round 3 should be a quick check of the F11 text.
