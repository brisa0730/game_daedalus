---
slug: phase5-gallery-server
round: 1
verdict: changes-requested
reviewer: claude-cowork
updated: 2026-11-03T11:40:00+09:00
blocking: 2
recommendations: 3
---

# Round 1: full read of `Documents/Phases/phase5-gallery-server.md`

## What I could run, verified, and could not

**Could run:** read-only shell over the Mac mini's connected `Daedalus` folder (`ls`, `cat`, `grep`, `sed`). No cargo, no Unity, no Chrome, nothing outside the folder.

**Verified against the files:**
- Every spike symbol the prompt depends on exists as named: `files.rs` `headers_for`, `response_headers`, `safe_join` (comment at l.54 points at Deferred.md as the prompt says); `play_token.rs` `Key`, `Purpose {Enter, Gate}`, `sign`, `verify`, and the `Key::generate` doc that leaves the per-process decision to this phase (l.19–20); `guard.rs` `same_origin` and `FRAMING_HEADERS` (2 entries: `frame-ancestors 'self'`, `X-Frame-Options: SAMEORIGIN`).
- `Cargo.toml`: edition 2024, axum 0.8, hmac, sha2, rand 0.9, subtle. `rand` is already there for the password generator and tokens.
- `Deploy/cloudflared/suwon.yml`: both hostnames → `http://127.0.0.1:8470`, catch-all 404, run line `cloudflared tunnel --config Deploy/cloudflared/suwon.yml run suwon`.
- `GallerySpikeBuild.cs` takes `-spikeCompression brotli|gzip|none` and `-spikeOutput`, as Step 0 uses them. The existing `TestResults/gallery-spike/brotli/Build/` holds exactly `brotli.{loader.js,data.br,framework.js.br,wasm.br}` (Step 0's verify line).
- decisions.md 2026-11-02 rows exist (class gallery, server stack, isolation, builds and limits); the class gallery row does say "(title, nickname, screenshot)", which Notes 6 supersedes.
- Deferred.md has both entries Notes 10 names, with the triggers it states.
- framework.md: the tunnel keeps the public `Host` (so Step 2's host routing and the `Host: 127.0.0.1` 404 hold); saves are keyed by the page URL up to its last `/`, so a changed `build_name` on re-upload does not lose saves (I checked this before raising it, and it does not need a finding).
- Recomputed: password space 256² × 8³ = 33,554,432 = 2²⁵ (the "about 25 bits" holds); game id alphabet `abcdefghjkmnpqrstuvwxyz23456789` = 23 letters + 8 digits = 31.

**Could not verify (accepted on trust or left to the Author):** that `cargo test`/clippy pass; the Argon2 timing; anything about `~/Library/LaunchAgents/com.orcarin.cloudflared.plist` (outside the connected folder); Chrome's behaviour under the play CSP; Axum 0.8's public API (F4 rests on my knowledge that `Router` exposes no route list; please confirm).

---

## Findings

### F1 · blocking · accuracy · Session idle timeout can never fire
**Location:** § Operator decisions 8 (idle 12 h, absolute cap 12 h); Step 7 "Idle timeout" and its test; Notes 1 and 3.

`last_seen ≥ created_at` always, so `last_seen + 12 h ≥ created_at + 12 h`: the absolute cap fires first (or at the same moment) for every session. The idle timeout, its "updated at most every 5 minutes" write, and the Step 7 test are dead. The test can only pass by editing `last_seen` to before `created_at`, a state the server never produces, so it tests nothing real.

This matters because the prompt gives shared school laptops as the reason for these timers. As written, the next period's student who opens a restored Chrome is signed in as the previous student for the rest of 12 h, and can remove that student's games. Notes 1 tells students "a sign-in lasts … 12 hours idle", which is not a separate limit.

**Fix:** either make idle shorter than the cap (see R1) and test it from a real state (a session created 2 h ago, `last_seen` 1 h 1 min ago), or drop the idle timer, the `last_seen` writes and the test, and say only "12 hours from sign-in". Either is fine; the current text is not.

### F2 · blocking · design · The Argon2 queue is a whole-class sign-in lockout anyone online can trigger
**Location:** § Operator decisions 8 (Argon2 row); Step 5 `Busy`; Step 7 throttle ("no global budget (a global cliff would let anyone online lock out the whole class)"); Notes 6 ADR.

The throttle is per login text, and an unknown ID is hashed with `verify_dummy()` so it costs the same as a real one. So an attacker who sends each attempt with a fresh random valid-format ID is never delayed, and every attempt takes an Argon2 slot. With 2 permits at 50–100 ms the server hashes about 20–40 per second. An attacker holding about 100 requests in flight builds a queue longer than the 3 s wait; tokio's semaphore is FIFO, so every real student's sign-in times out with "잠시 후 다시 해 보세요" for as long as the script runs. That is the global cliff the prompt says it avoids. It's reachable without an account and needs only a short script, which a classmate can write.

**Fix (simplest):** don't hash for IDs that don't exist. Answer them at once with the same wrong-ID-or-password message, and accept that existence can be probed. IDs are printed on slips and aren't secret in this design. Then only real accounts reach Argon2, each throttled 1 → 30 s, so steady load is about N/30 hashes per second (10/s even at 300 accounts across every class), well under capacity. State the trade in the ADR. If the Author prefers to keep hiding existence, the prompt must name another bound that keeps unknown IDs from taking the permits real accounts need. A second permit pool alone isn't enough, because its timing shows which IDs exist under load.

### F3 · non-blocking · accuracy · The "1,000 passwords all distinct" test is flaky
**Location:** Step 5 tests.

With N = 2²⁵ and n = 1,000, P(any repeat) ≈ 1 − e^(−n(n−1)/2N) = 1 − e^(−499,500/33,554,432) ≈ **1.5%** per run. A correct generator fails CI about once in 67 runs. **Fix:** 100 passwords (≈ 0.015%), or assert something deterministic, such as every word coming from the list and the digits from 2–9.

### F4 · non-blocking · design · "A test fails if the router has a route not in the list" has no mechanism
**Location:** Step 2 (enumerated POST list), Step 7 (enumerated GET list), Step 21.

Axum 0.8's `Router` has no public way to list its routes. "Compare against the route table the app exposes for tests" therefore only works if that table is what *builds* the router. If routes are added with `.route()` and the table is maintained beside it, a new route missing from both is invisible to the test. That's the fail-open wire rule 7 forbids, on the exact guard the test exists to prove. **Fix:** say that `router()` is built by iterating one table of `(method, path, handler, access: Public | SignedIn | Teacher)`, and the POST/GET tests iterate the same table. Then a route can't exist without its row.

### F5 · non-blocking · accuracy · The 10-game recheck in SQLite gives the loser a 500, and new-game order is unstated
**Location:** Step 4 "games insert (counting … inside the same transaction)"; Step 10 caps and swap; its concurrency test.

sqlx's `begin()` is `BEGIN DEFERRED`. In WAL mode both concurrent uploads read 9 live games. The first upgrades to a write and commits. The second's upgrade fails with `SQLITE_BUSY_SNAPSHOT`, which `busy_timeout` does not retry. Only one succeeds, so the test passes, but the loser gets a database error, not the Korean 10-game message. **Fix:** make the insert a single statement (`INSERT … SELECT … WHERE (SELECT COUNT(*) …) < 10`, zero rows inserted means the cap message), or `BEGIN IMMEDIATE`. Have the test assert the loser's 4xx and message.

The swap in Step 10 is written for an update (`WHERE … version=<old>`). For `game=new` it doesn't say when the row is inserted relative to the unpack and rename. If it comes first, a failed unpack leaves a live row with no files, which shows a broken card. Please state the order: id → unpack → rename into `games/<id>/v1/` → conditional insert, and if zero rows are inserted, delete the folder.

### F6 · non-blocking · design · Backups can't restore anything older than last night, and one crash stops them for good
**Location:** Step 18; § Operator decisions 7; Notes 3.

- The games mirror deletes every version folder that is gone from the source, so each night it drops replaced versions and removed games. Six of the seven kept database copies then point at `games/<id>/v<n>` folders that no longer exist in the mirror. A game removed by mistake (or by a classmate in a still-signed-in session, F1) can't be recovered after the next 03:30. The mirror sits on the same disk as the source, so it doesn't protect against losing that disk either.
- `VACUUM INTO` refuses an existing target. If a run dies after creating its temp file, every later run fails on that file. The failure shows only as a non-zero exit in `backup.log`, which nobody reads.

**Fix:** delete any stale temp file first, or use a unique temp name, and add a test for it. Keep folders that left the source for 7 days and prune them by age, so every kept database copy can be restored. Add a single restore line (stop the job, copy the database and folders back, start). If the Author keeps the current shape, Notes should say plainly that the backup restores only last night's state.

### F7 · non-blocking · design · The game list downloads every screenshot at full size
**Location:** Step 12 `GET /`; Step 11 thumbnails; Share zip contract (screenshot ≤ 2 MB, ≤ 2048 × 2048); § Operator decisions 6 (one gallery for all classes).

`/` shows every live game's `screenshot.png` as sent, with no thumbnails. At 30 students × up to 10 games, each page load can be up to 600 MB in the worst case. A whole class opens it at the same moment on school Wi-Fi, and one gallery for every class multiplies that. **Fix:** cheap and fits the CSP: `loading="lazy"` with `width` and `height` on card and thumbnail `<img>`. Also consider tightening the contract while Phase 6 hasn't been built, for example 1280 × 720 and ≤ 1 MB, since Phase 6 makes the screenshot.

### F8 · non-blocking · accuracy · The upload limit is 50 MiB in one place and 50 MB in others, and it has two homes
**Location:** Step 10 ("over 50 MiB"); Step 11 JS ("≤ 50 MB"); Step 22 item 3; Notes 2 ("≤ 50 MB zipped"); decisions.md row ("50 MB").

50 MiB is 52,428,800 bytes. A 51 MB zip is accepted by the server but refused by the browser if the JS uses 50,000,000, and the opposite happens if the JS uses MiB. The JS also holds a second copy of a limit the conventions keep "only in `config.rs`". **Fix:** one byte count in `config.rs`, rendered into the upload page as a `data-max-bytes` attribute that `gallery.js` reads, and the contract states it in bytes.

### F9 · non-blocking · design · "Never locks anyone out" holds only for browsers that already signed in
**Location:** Step 7 throttle and known browser; Notes 1 ("never locks anyone out"); Notes 6 ADR.

An early attempt isn't checked and doesn't move the next allowed time. So a script that tries a classmate's ID (printed on their slip) at each allowed moment keeps the ID at the 30 s cap and takes every opening. The real student gets in only if they press Sign in in the gap between the allowed moment and the script's request. The known-browser cookie protects a browser that has signed in to that ID before. It doesn't protect the first sign-in on slip day, a new or cleared browser, or the second student on a shared laptop: one cookie name per browser, so the second student's sign-in overwrites the first's. **Fix:** say the residual risk honestly in Notes 1 and the ADR. Give the teacher a remedy: password reset also clears that login's throttle entry, which turns "can't sign in" into something the teacher can fix. Optionally let `__Host-gallery_known` hold several logins.

### F10 · non-blocking · accuracy · The commonest mistake can get the wrong first message
**Location:** Step 9 checks 5 and 7.

A student who zips a Build Settings output folder sends `MyGame/{index.html, Build/, TemplateData/}`. Check 5 ("the expected files are inside one wrapper folder") is undefined here: the folder has `Build/` but no `daedalus.json` or screenshot. If check 5 fires, the student unwraps it, uploads again, and only then gets check 7's message that the build itself is wrong. That's two round trips, and the first message points the wrong way. **Fix:** define check 5 as "a single top-level folder that contains `daedalus.json`". A wrapper with `Build/` and no `daedalus.json` falls through to the Build Settings message. Add that case to the `ShareZip` mutators.

---

## Recommendations (open questions; the operator decides)

### R1 · recommendation · Session numbers (§ Operator decisions 8, Open questions)
Set **idle 1 h, absolute 12 h** (or idle = one class period plus a margin), and add one line on the slip or the header telling students to press 로그아웃 on a shared laptop. An idle timer can't fully protect a laptop handed over mid-period, but 1 h covers the common case of laptops passed between class periods. A 12 h idle covers nothing (F1).

### R2 · recommendation · Teacher uploads and all-or-nothing paste (Open questions)
Accept both defaults. Teacher uploads let the teacher post an example level through the same path students use. All-or-nothing paste keeps the printed slips matching what was created. A partial create would leave the teacher working out which lines were made.

### R3 · recommendation · Backups on the same Mac mini (Open questions)
Accept same-machine for v1 only together with F6's fix, and say in Notes what they protect against (database corruption and mistakes within 7 days, not losing the disk). The database is small; copying just `gallery-*.db` to a second disk or cloud folder later is the cheapest step up. Leave it deferred with that as its trigger.

---

## Reviewer's view

I would not build this as written yet. F2 brings back the whole-class lockout the design set out to avoid, through the Argon2 queue, and F1 means the shared-laptop timer the prompt relies on doesn't exist. Both have small fixes. The rest (F3–F10) are folds the Author can make without changing the design. The structure is sound: the spike's building blocks are reused as cited, the zip contract and fail-closed guards are well specified, and there's a stop before going live.
