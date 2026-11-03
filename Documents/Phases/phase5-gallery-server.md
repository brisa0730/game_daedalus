# Phase 5: Gallery Server

## Overview

**Where things stand:** The class gallery website is designed and its risky parts were tried in a test, but there is no real website yet. Nobody can sign in, upload a game or play a classmate's.

**What this phase changes:**
- Students and the teacher sign in with their own ID and password. Nothing can be seen without signing in.
- The teacher adds students one at a time or as a pasted list, prints a slip for each one, resets passwords and removes games or accounts.
- A student uploads a game as a new game or as an update to one of theirs. An update keeps the same address, so players keep their saves. A wrong file gets a clear message saying what to do instead.
- Every game shows as a card and plays full-page, with a fullscreen button.
- Every page is in Korean.
- The site starts by itself on the Mac Mini and is backed up every night.

**How you'll know it worked:** You sign in at the class address, upload our test level, see its card, play it full-screen, upload it again and find your progress still there.

**Not in this phase:** the editor's Share button that makes the zip. That is the next phase, and classroom use waits for it.

---

## Source
- **GDD sections:**
  - `systems.md` § Share
  - `platforms.md` § Storage, § External Services (class gallery, games host, builds, accounts)
  - `architecture.md` (gets a gallery server row)
  - `Glossary.md` (Shared build, Class gallery, Games host, Play grant)
- **Decisions:** `decisions.md` rows dated 2026-11-02:
  - class gallery
  - server stack (Rust Axum + SQLite)
  - isolation (spike outcome)
  - builds and limits
- **Spike:** `Documents/Plans/archive/class-gallery-spike.md` (Findings and Recommendation). Its header map, play grants and same-origin guard are kept in `Deploy/gallery/src/`.
- **This phase's decisions are not in the GDD yet.** This prompt is their home until the phase lands; § Notes to GDD carries them.
- **GDD version:** v0.15; this phase ships as v0.16.
- **Depends on:**
  - The class gallery spike (v0.14): `files.rs` (`headers_for`, `response_headers`, `safe_join`), `play_token.rs` (`Key`, `Purpose`, `sign`, `verify`), `guard.rs` (`same_origin`, `FRAMING_HEADERS`).
  - The `suwon` tunnel and both DNS records.
  - `Deploy/cloudflared/suwon.yml` (both hostnames → `http://127.0.0.1:8470`).
  - ISS-001 fixed (v0.15), so WebGL builds draw.
  - `Assets/Dev/Editor/GallerySpikeBuild.cs`, used only to rebuild the real test build.
  - If a name in this prompt differs from the code, the code wins; note it in the phase log.

## Operator decisions

The operator approved this phase's scope (2026-11-03): accounts with teacher-created logins, display names set by the teacher instead of the editor's nickname, the teacher page, upload with replace-in-place, the game list and play view, and deployment with a stop before going live. The operator decided **Korean UI**. The security mechanics (sessions, throttle, zip checks, headers, CSP) follow the spike's findings and the design panel; they are Claude's design, reviewed by the critic and the handshake. Everything below marked **PROPOSED** is Claude's default; the operator may override it before or during the phase.

1. **UI language: Korean (decided).** Every student- and teacher-facing string lives in one Rust module, `strings.rs`, written in Korean. That covers pages, buttons, error and next-step messages, and the printable slip. Pages declare `<html lang="ko">` and `<meta charset="utf-8">`, served as `text/html; charset=utf-8`. Generated passwords and login IDs stay ASCII so they type on any keyboard. Unity menu names inside messages stay as Unity shows them (`Daedalus → Share my game`).
2. **Upload target (PROPOSED):** the student picks "update one of my games" (and which one) or "new game". The title is only a label taken from the zip's metadata.
3. **Students may remove their own games; at most 10 live games per account; a 20 GB total disk cap (PROPOSED).**
4. **Server-owned player page (PROPOSED):** the zip's `index.html` is ignored. The games host serves its own page, which loads the build by its file names.
5. **Play-host CSP (PROPOSED):** strict, tried with a real build. Anything Unity needs that opens a way to send data out is recorded as an accepted risk (phishing inside the frame).
6. **One gallery for all classes (PROPOSED).**
7. **Daily backups (PROPOSED):** `VACUUM INTO` plus a mirror of the games folder at 03:30. Keep 7 database copies.
8. **Numbers (PROPOSED):**

   | Setting | Value |
   |---|---|
   | Session idle timeout | 1 h (about one class period plus margin; Reviewer recommends: idle 1 h, absolute 12 h, plus a "shared laptop: press 로그아웃" line in the header — idle 12 h covered nothing (round 1, R1)) |
   | Game gate cookie | 2 h (it only has to outlive loading and reloads; shorter limits how long a game opened on a shared laptop stays playable from history after sign-out) |
   | Title length | 40 characters |
   | Display name length | 20 characters |
   | Login ID | 3–20 characters of `a-z 0-9 - _` |
   | Sign-in throttle | 1 s, doubling, capped at 30 s; skipped by a known-browser cookie for that account |
   | Session absolute cap | 12 h from sign-in |
   | Argon2 | 2 at once, 50–100 ms each on the Mac Mini; only existing accounts are ever hashed; a sign-in that can't get a slot within 3 s gets "잠시 후" without hashing |
   | Upload size | 50,000,000 bytes (one value in `config.rs`; the upload page passes it to `gallery.js` as `data-max-bytes`) |
   | Throttle map | existing accounts only (one entry per account at most); unknown IDs never enter it |
   | Uploads | at most 3 unpacking at once |
   | Screenshot | ≤ 1,000,000 bytes, ≤ 1280 × 720 |
   | Zip entries | ≤ 256 |
   | Bytes unpacked | ≤ 120 MB |

9. **Teacher may upload (PROPOSED):** the same upload path and cap as a student.
10. **Bulk paste is all-or-nothing (PROPOSED):** Create stays disabled while any previewed line has an error.

## File Manifest

Crate root `G/` = `Deploy/gallery/`.

| Action | File | Purpose |
|--------|------|---------|
| MODIFY | `G/Cargo.toml`, `G/Cargo.lock` | Direct deps:<br>- tokio<br>- sqlx (sqlite, runtime-tokio, migrate)<br>- argon2<br>- maud (no axum feature; pages return `Html(markup.into_string())`)<br>- zip (deflate only)<br>- serde, serde_json<br>- tracing, tracing-subscriber<br>- http-body-util<br>Dev-deps: tower (util), tempfile |
| MODIFY | `G/src/lib.rs` | Declares the new modules; doc comment says this is the server, not only building blocks |
| NEW | `G/src/main.rs` | Thin entry: `serve`, `teacher create\|reset`, `backup` subcommands |
| NEW | `G/src/config.rs` | Data-folder layout, bind address `127.0.0.1:8470`, hostnames and origins, every limit in one place |
| NEW | `G/src/app.rs` | `AppState`, `router(state)`, routing by `Host` (gallery / games host / unknown) |
| MODIFY | `G/src/guard.rs` | The guard and the response headers become Axum middleware layers (gallery and games host); full gallery CSP |
| MODIFY | `G/src/files.rs` | `safe_join` comment: uploads never unpack symlinks (pointer to `share_zip`) |
| MODIFY | `G/src/play_token.rs` | Grant and gate lifetimes as consts; the `Key` comment records the per-process decision |
| NEW | `G/src/strings.rs` | Every user-facing string, in Korean |
| NEW | `G/src/text.rs` | Cleaning for titles and display names; login ID rules |
| NEW | `G/src/pages.rs` | maud page shell (lang, charset, header with the signed-in name and Sign out), shared bits |
| NEW | `G/src/db.rs` | SQLite pool, migrations, the queries for users, sessions and games |
| NEW | `G/migrations/0001_init.sql` | `users`, `sessions`, `known_browsers`, `games` |
| NEW | `G/src/accounts.rs` | Argon2 hashing (spawn_blocking + semaphore), readable password generator with its word list |
| NEW | `G/src/session.rs` | Sign in and sign out, session cookie, signed-in extractor, throttle |
| NEW | `G/src/cli.rs` | `teacher create` and `teacher reset` |
| NEW | `G/src/teacher.rs` | Teacher page: students list, add or paste with preview, slip, reset, remove account |
| NEW | `G/src/share_zip.rs` | Share zip validator and unpacker (pure of HTTP) |
| NEW | `G/src/upload.rs` | Upload route and page: body limit, streaming, target, caps, version swap |
| NEW | `G/src/gallery.rs` | Game list, My games, screenshots, remove a game, the play view page |
| NEW | `G/src/play.rs` | Games host: grant → cookie → 302, gated build files, player page |
| NEW | `G/src/backup.rs` | `backup` subcommand |
| NEW | `G/static/gallery.css`, `G/static/gallery.js`, `G/static/player.js` | Embedded with `include_str!`; no inline scripts anywhere |
| NEW | `G/tests/support/mod.rs` | Test app on a temp data folder, synthetic Share zip builder, request helpers |
| NEW | `G/tests/routing.rs`, `G/tests/sign_in.rs`, `G/tests/teacher.rs`, `G/tests/upload.rs`, `G/tests/games.rs`, `G/tests/play.rs` | Router integration tests (`tower::ServiceExt::oneshot`) |
| NEW | `G/examples/share_zip_from_build.rs` | Dev only: makes a Share zip from a Unity WebGL build folder (for the manual Chrome check) |
| NEW | `Deploy/launchd/com.orcapafea.daedalus.gallery.plist` | Server job |
| NEW | `Deploy/launchd/com.orcapafea.daedalus.gallery-tunnel.plist` | `suwon` tunnel job |
| NEW | `Deploy/launchd/com.orcapafea.daedalus.gallery-backup.plist` | Daily backup job |
| MODIFY | `Deploy/cloudflared/suwon.yml` | Comment: the source; deploy installs a copy to `~/.cloudflared/suwon.yml`, which the tunnel job reads |
| MODIFY | Docs at cleanup | GDD (`systems.md`, `platforms.md`, `architecture.md`, `Glossary.md`, `GDD.md`), `decisions.md`, `Roadmap.md`, `Deferred.md`, `Changelog.md`, `CLAUDE.md`, `framework.md`, `MEMORY.md`, the phase log |

No Unity file changes. Step 0 runs a Unity build but commits nothing from it.

---

## Conventions for every step

- **Rust:** edition 2024. `cargo clippy --all-targets -- -D warnings` stays clean. No `unwrap()`/`expect()` on request data; `expect` only for real invariants, with a reason.
- **Simplicity:** one binary, no feature flags, no "prepared for" code. Hand-parse the three subcommands and the `Cookie` header. No multipart: the upload is a raw `application/zip` body.
- **Wiring integrity (CLAUDE.md rule 7):** every guard, header and check ships with a router test that proves it is applied. A check that never runs is the defect this project fears most. Wherever input is missing, every check fails **closed**. Each new guard is seen failing once on a planted change, then reverted.
- **Hostile content:** titles, display names and file names from uploads are untrusted.
  - HTML goes only through maud's escaping. `PreEscaped` appears nowhere except in the shell's fixed asset tags (grep at review).
  - No response takes its content type from an upload.
  - No route builds a path from request text without `safe_join` or the database's stored names.
- **One home for strings and numbers:** user-facing text only in `strings.rs`; limits only in `config.rs`.
- **Logs** (stderr → launchd log file):
  - One line per sign-in success or failure (login ID only), throttle start, upload accepted or rejected (reason code), teacher action and startup.
  - **Never** a password, password hash, session token, grant or gate cookie.
- **Clock:** `config::unix_now()` is the only clock read. Pure functions such as the throttle math take `now` as an argument. Tests that need time edit stored timestamps instead of sleeping.
- **Tests:** in `Deploy/gallery/` run `cargo test` and `cargo clippy --all-targets -- -D warnings`. Test apps use cheap Argon2 settings passed in through the config, never a `cfg(test)` branch in the server code.

---

## Steps

### Step 0: Precondition — the real test build
- The Brotli build in `TestResults/gallery-spike/brotli/` predates the ISS-001 fix and draws nothing; rebuild it now, before any server work, so the phase never stops mid-way for Unity.
- If the Unity Editor has the dev project open (`pgrep -fl "Unity.*-projectPath"` or the project's `Temp/UnityLockfile`), **stop and ask the operator to close it**. Then: `/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -executeMethod Orcapafea.Daedalus.Dev.GallerySpikeBuild.Build -buildTarget WebGL -spikeCompression brotli -spikeOutput TestResults/gallery-spike/brotli -logFile TestResults/gallery-spike/build-brotli.log`.
- Afterwards run `git status` and restore any churn Unity left (`Assets/Settings/UniversalRP.asset` per framework.md, and any reserialized `ProjectSettings/` or `EditorBuildSettings` file). Nothing from the build is committed.
- **Verify:** `[manual]` the build log shows success; `git status` is clean; the build folder has `Build/brotli.{loader.js,data.br,framework.js.br,wasm.br}`.

### Step 1: Dependencies and the binary's skeleton
- **MODIFY `G/Cargo.toml`:** add the deps listed in the manifest at their current versions. Use `zip` with `default-features = false, features = ["deflate"]`. Use `sqlx` with `default-features = false, features = ["runtime-tokio", "sqlite", "migrate", "macros"]`; `migrate!` is the only macro, and queries are runtime `sqlx::query`.
- **NEW `G/src/config.rs`:**
  - `Config` holds:
    - the data folder
    - the bind address (always `127.0.0.1:8470`; never configurable to another interface)
    - gallery and games hostnames and origins (`https://suwon.orcarin.com`, `https://suwon-play.orcarin.com`)
    - Argon2 parameters
    - every limit from § Operator decisions 8
  - Data-folder layout: `gallery.db`, `games/`, `staging/`; created at start with mode 0700.
  - `unix_now()`.
- **NEW `G/src/main.rs`:**
  - `gallery serve --data <dir>` (the default subcommand)
  - `gallery teacher create <id> <display name> --data <dir>`
  - `gallery teacher reset <id> --data <dir>`
  - `gallery backup --data <dir> --to <dir>`
  - Unknown input prints usage and exits 2.
  - `serve` initializes tracing to stderr, opens the database, sweeps `staging/` (deletes leftovers) and binds.
- **MODIFY `G/src/lib.rs`:** module list and doc comment.
- **Tests:** a usage test for the argument parser (pure function); `Config` binds to loopback only.
- **Verify:** `[automated]` `cargo test`; `[manual]` `cargo run -- serve --data <scratchpad>/gallery-data` starts, logs its bind address, and `lsof -iTCP:8470 -sTCP:LISTEN` shows `127.0.0.1` only.

### Step 2: Routing by host, the guard and header layers
- **NEW `G/src/app.rs`:**
  - `AppState` (config, db pool, play `Key`, throttle, semaphores, busy-game set).
  - `router(state) -> Router` dispatches on `Host`:
    - `suwon.orcarin.com` → gallery router.
    - `suwon-play.orcarin.com` → games router.
    - Anything else (including `127.0.0.1:8470`) → 404 with no body. There is no local host branch: loopback checks send `Host: suwon.orcarin.com` and `Origin: https://suwon.orcarin.com` with curl, since the origin speaks plain HTTP behind the tunnel.
- **MODIFY `G/src/guard.rs`:** keep `same_origin`. Add two layers built on `axum::middleware::from_fn_with_state`.
  - **Gallery layer:**
    - Any method other than GET/HEAD must pass `same_origin` against the gallery origin, else 403. It fails closed.
    - Every response gets the headers below. The layer **appends** its CSP (a handler's own CSP, such as the screenshot's `sandbox`, also applies) and leaves a `Cache-Control` the handler already set alone:
      - `Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; frame-src https://suwon-play.orcarin.com; frame-ancestors 'self'; form-action 'self'; base-uri 'none'; object-src 'none'`. This replaces the `frame-ancestors`-only value; keep `X-Frame-Options: SAMEORIGIN`.
      - `X-Content-Type-Options: nosniff`
      - `Referrer-Policy: same-origin`
      - `Cache-Control: no-store`, except static assets
  - **Games layer:** non-GET/HEAD → 405. Every response gets nosniff, `Referrer-Policy: no-referrer` and the play CSP from Step 15.
  - Replace `FRAMING_HEADERS` with the layer's header list; update its tests.
- **Tests (`tests/routing.rs`):**
  - unknown host 404, including `Host: 127.0.0.1:8470`
  - a POST with `Sec-Fetch-Site: same-site` / `cross-site` / missing headers / `Origin: null` / the sibling origin → 403; same-origin passes
  - CSP and framing headers on a 200, a 303, a 403 and a 404
  - a handler-set CSP and `Cache-Control` survive the layer
  - games host POST → 405
  - **The enumerated POST list:** the test iterates every POST row of `app::routes()` (the one table `router()` is built from; Step 7) on the real router. Each later step adds its routes to that table; each POST row is asserted 403 for cross-site, same-site and missing headers, and 303/401 without a session (except `/login`).
- **Verify:** `[automated]` `cargo test`.

### Step 3: Korean strings, text cleaning and the page shell
- **NEW `G/src/strings.rs`:**
  - Korean consts for every label, button and message, plus small `fn`s for messages with values, for example `signed_in_as(name)` → "{name} 님으로 로그인됨" and `wait_seconds(n)`.
  - One comment at the top: the one home for UI text.
- **NEW `G/src/text.rs`:**
  - `clean_label(raw, max_chars) -> Option<String>`:
    - trims
    - removes control characters (Unicode `Cc`) and bidi overrides (U+202A–U+202E, U+2066–U+2069)
    - collapses runs of whitespace
    - caps at `max_chars` Unicode scalar values
    - returns `None` if nothing is left
  - `normalize_login(raw) -> Option<String>`: lowercase ASCII `a-z 0-9 - _`, 3–20 characters.
- **NEW `G/src/pages.rs`:**
  - `shell(title, user: Option<&User>, body) -> Markup`:
    - `<!doctype html>`, `<html lang="ko">`, `<meta charset="utf-8">`, viewport
    - links to `/static/gallery.css` and `/static/gallery.js`
    - a header with the signed-in display name, a Sign out button (a POST form) and links (게임 목록, 업로드, plus 선생님 for the teacher)
    - font stack with Apple SD Gothic Neo, Malgun Gothic, sans-serif
  - Static routes `/static/gallery.css` and `/static/gallery.js` (public, `Cache-Control: no-cache`).
- **NEW `G/static/gallery.css`, `G/static/gallery.js`:** start minimal; later steps add to them.
- **Tests:**
  - `clean_label` cases (control chars, bidi, cap, empty)
  - `normalize_login` cases
  - the shell contains `lang="ko"` and the charset meta
  - a display name `<script>alert(1)</script>` renders escaped
- **Verify:** `[automated]` `cargo test`; `[grep]` no Hangul in any `.rs` file other than `strings.rs` (and test assertions).

### Step 4: Database
- **NEW `G/migrations/0001_init.sql`:**
  - `users(id INTEGER PRIMARY KEY, login TEXT NOT NULL UNIQUE, display_name TEXT NOT NULL, role TEXT NOT NULL CHECK (role IN ('teacher','student')), password_hash TEXT NOT NULL, created_at INTEGER NOT NULL)`
  - `sessions(token_hash BLOB PRIMARY KEY, user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE, created_at INTEGER NOT NULL, last_seen INTEGER NOT NULL)`
  - `known_browsers(token_hash BLOB NOT NULL, user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE, created_at INTEGER NOT NULL, PRIMARY KEY (token_hash, user_id))`: one browser token can be known for several logins (a shared laptop)
  - `games(id TEXT PRIMARY KEY, owner_id INTEGER REFERENCES users(id) ON DELETE SET NULL, title TEXT NOT NULL, build_name TEXT NOT NULL, version INTEGER NOT NULL, bytes INTEGER NOT NULL, created_at INTEGER NOT NULL, updated_at INTEGER NOT NULL, removed_at INTEGER)`
  - Removed games keep their row (a tombstone), so an id is never reused.
- **NEW `G/src/db.rs`:**
  - Pool with `foreign_keys(true)`, WAL, `busy_timeout` 5 s; `sqlx::migrate!` at open.
  - Typed functions for every query later steps need: users by login or id, create, set password, delete; sessions insert, find, touch, delete for a user, prune; known browsers insert, check, delete for a user; games insert as **one conditional statement** (`INSERT … SELECT … WHERE (SELECT COUNT(*) FROM games WHERE owner_id=? AND removed_at IS NULL) < 10`; zero rows inserted = the cap, so two concurrent new uploads never surface a database error), find live, list live, list by owner, sum of bytes, set version (`WHERE id=? AND removed_at IS NULL AND version=?`, returning rows changed), tombstone.
  - No SQL outside `db.rs`.
- **Tests:**
  - deleting a user cascades its sessions and sets `owner_id` NULL on games
  - a tombstoned id can't be inserted again (PRIMARY KEY)
  - the login UNIQUE constraint holds
- **Verify:** `[automated]` `cargo test`.

### Step 5: Password hashing and readable passwords
- **NEW `G/src/accounts.rs`:**
  - `hash(password)` and `verify(hash, password)` run Argon2id in `tokio::task::spawn_blocking` behind a 2-permit semaphore. Acquiring a permit waits at most 3 s; a timeout returns `Busy` and nothing is hashed (the sign-in form answers "잠시 후 다시 해 보세요"). Parameters come from `Config`; the PHC string records them, so a later change keeps old hashes valid.
  - **Unknown IDs are never hashed.** A sign-in for a login that doesn't exist is answered at once with the same "아이디나 비밀번호가 틀려요" message. That lets anyone probe which IDs exist, which is accepted: IDs are printed on slips and aren't secret here. Hashing unknown IDs (to hide existence) would let a script with random IDs fill both Argon2 slots and time out every real sign-in, a whole-class lockout. With only real accounts hashed, each throttled 1 → 30 s, steady load stays near accounts/30 hashes per second.
  - `generate_password()`:
    - `word-word-ddd` from an embedded list of ~256 short lowercase English words (3–6 letters, no `l`)
    - digits 2–9, no 0/1
    - from `rand`'s OS-seeded thread RNG
    - about 25 bits
- **Measure on the Mac Mini:** time one hash at the chosen parameters (start from m = 19 MiB, t = 2, p = 1). Tune to 50–100 ms in a release build, set the result as the `Config` default, and record it in the phase log.
- **Tests:**
  - round trip
  - a wrong password fails
  - generated passwords match `^[a-km-z]+-[a-km-z]+-[2-9]{3}$`
  - 100 generated passwords are all distinct (P(repeat) ≈ 0.015%; 1,000 would fail ~1.5% of correct runs), and every generated word is from the list and every digit in 2–9
  - the word list has no `l`, no duplicates and ≥ 256 entries
  - with both permits held, a third verify returns `Busy` after the timeout (short timeout in the test config) and never runs
- `teacher reset` and the teacher page's reset and removal delete the user's known browsers as well as their sessions, and a reset also clears that login's throttle entry (Steps 6 and 8).
- **Verify:** `[automated]` `cargo test`; `[manual]` the measured time is in the log.

### Step 6: Teacher account from the command line
- **NEW `G/src/cli.rs`:**
  - `teacher create <id> <display name>` refuses if a teacher already exists ("use `teacher reset`") or the ID is taken. It stores the hash and prints the password once to stdout, never to the log.
  - `teacher reset <id>` sets a new password, deletes that teacher's sessions and prints the password once.
  - There is no web route for either.
- **Tests:** create → the user exists with role teacher; a second create refuses; reset changes the hash and deletes sessions; a bad ID or name is refused.
- **Verify:** `[automated]` `cargo test`.

### Step 7: Sign in, sign out and sessions
- **NEW `G/src/session.rs`:**
  - **Pages and routes:** `GET /login` (the form) and `POST /login`. `POST /logout` deletes the session and clears the cookie.
  - **Session cookie:** `__Host-gallery_session=<32 random bytes, hex>`; HttpOnly, Secure, SameSite=Lax, Path=/; no Max-Age, so it lasts one browser session. Only the SHA-256 of the token is stored.
  - **Idle timeout** of 1 h on `last_seen`, updated at most every 5 minutes, and an **absolute cap** of 12 h from `created_at` (shared school laptops passed between class periods; Chrome's "continue where you left off" keeps session cookies). The header carries one line telling students to press 로그아웃 on a shared laptop. Expired sessions are deleted when found and at startup.
  - **`SignedIn` extractor:** a missing, unknown or expired session on a gallery page → 303 to `/login`, and on the upload route → 401 with the Step 10 JSON body `{"ok":false,"message":"로그인이 끝났어요. 다시 로그인한 뒤 올려 주세요","login":true}` (a 1 h idle timeout makes this common after a class period in Unity); `gallery.js` shows it with a link to `/login`. **Every gallery route except `/login` and `/static/*` requires it.**
  - **Throttle:** in memory, keyed by user id, **for existing accounts only**. Unknown IDs are never hashed (Step 5), so there is nothing to slow down, and they never create an entry: the map holds at most one entry per real account and can't be flushed from outside.
    - After a failure, the next attempt is allowed after a delay that starts at 1 s and doubles to a 30 s cap. Success clears it. There is never a hard lockout, nothing per IP, and no global budget (a global cliff would let anyone online lock out the whole class).
    - An attempt that comes too early gets the form back with `strings::wait_seconds(n)`, and its password isn't checked.
    - **Known browser:** a successful sign-in also sets `__Host-gallery_known=<32 random bytes, hex>` (HttpOnly, Secure, SameSite=Lax, Path=/, Max-Age 1 year), stored hashed in `known_browsers` for that user. A browser that already has the cookie keeps its token and gains a row for the new login, so one shared laptop is known for several students. An attempt carrying a valid known-browser cookie **for the login being tried** skips the delay, so a classmate hammering a friend's ID (printed on slips) can't keep the friend out of a browser the friend used before. It never skips the password check.
    - **Residual risk (accepted, stated in the ADR):** a script trying a classmate's ID at every allowed moment can still hold that ID at the 30 s cap for a browser the classmate has never signed in on (slip day, a new or cleared browser). The teacher's remedy: a password reset also clears that login's throttle entry.
    - The map drops entries untouched for an hour. An account's removal drops its entry.
  - **Logging:** sign-in success and failure are logged with the login ID only.
- **Tests (`tests/sign_in.rs`):**
  - right password → 303 to `/` with the `__Host-` cookie and its attributes
  - wrong password → the form with a message
  - an immediate second attempt → the wait message
  - an unknown ID gets the same message without any hash (hash counter in the test config stays at 0)
  - 10,001 unknown-ID failures leave a real account's pending delay unchanged and the throttle map's size unchanged
  - the throttle doubles and caps (pure function with `now`)
  - a browser with a valid known-browser cookie for that login signs in during the delay; a known-browser cookie for another login doesn't skip it; a second student signing in on the same browser keeps the first student's skip
  - a teacher reset clears the login's throttle entry
  - **every gallery route comes from one table**: `app::routes()` returns `(method, path, handler, access: Public | SignedIn | Teacher)` rows, `router()` is built by iterating that table, and the enumerated POST and GET tests iterate the same table, so a route can't exist without its row (axum 0.8's `Router` can't list its routes)
  - **every gallery GET route** except `/login` and static → 303 `/login` without a session, and with a session its page shows the signed-in display name and the Sign out form (enumerated list; a new route must join it; a test fails if the router has a GET route not in it)
  - a session created 2 h ago with `last_seen` 1 h 1 min ago → `/login` (idle); a session created 12 h 1 min ago with `last_seen` 1 min ago → `/login` (absolute); a session created 2 h ago with `last_seen` 10 min ago still works
  - logout invalidates the token
  - `GET /logout` → 405
- **Verify:** `[automated]` `cargo test`.

### Step 8: Teacher page
- **NEW `G/src/teacher.rs`.** Every route checks role teacher (a student gets 403).
  - **`GET /teacher`:**
    - a students table: ID, display name, live game count, Reset password, Remove
    - a two-field add form (ID, display name)
    - a paste box ("한 줄에 한 명: 아이디, 이름"; comma or tab separator, split at the first one)
  - **`POST /teacher/students/preview`** (both forms post here as text): a table of the lines, each marked OK, invalid ID, missing name, duplicate in the paste, or ID already taken. Create is shown only when every line is OK (PROPOSED). It carries the same text back.
  - **`POST /teacher/students/create`:**
    - re-validates on the server
    - creates every student with a generated password in one transaction
    - renders the **slip page**: per student, the gallery address, ID, display name and password in large type; print CSS puts several slips on a page with cut lines; `Cache-Control: no-store`
    - passwords are never stored in plain text or shown again
  - **`POST /teacher/students/<id>/reset`:** new password, sessions and known browsers deleted, a page showing it once in large type (and a one-slip print).
  - **`GET /teacher/students/<id>/remove`:** a confirm page naming the account and its game count. **`POST`** to the same path:
    - takes each of their games' busy lock (Step 10); if any is uploading, refuses with "업로드 중이에요"
    - tombstones their games and deletes their game folders
    - deletes the user (sessions cascade)
    - logs it
  - The teacher can't remove themselves here.
- **Tests (`tests/teacher.rs`):**
  - a student gets 403 on every teacher route
  - preview marks each error kind
  - create with any error refuses
  - create makes the accounts, and each printed password signs in
  - reset: the old session is gone and the new password works
  - remove: user gone, sessions gone, games tombstoned, folders deleted from disk, and the id stays taken in `games`
  - a cross-site POST → 403 (layer wired)
- **Verify:** `[automated]` `cargo test`.

### Step 9: The Share zip contract and its validator
- **Write the contract first** into this prompt's Notes (synced to `platforms.md` § Share zip, its one home; Phase 6 produces exactly this):
  - **Files at the zip's root** (no wrapper folder):
    - `daedalus.json`: UTF-8 JSON `{"format": 1, "title": "<text>"}`; unknown keys ignored
    - `screenshot.png`: PNG, ≤ 1,000,000 bytes, ≤ 1280 × 720
    - `Build/<name>.loader.js`, `Build/<name>.data.br`, `Build/<name>.framework.js.br`, `Build/<name>.wasm.br`: exactly one of each, the same `<name>` (Unity names it after the output folder), `<name>` matching `[A-Za-z0-9_-]{1,64}`, Brotli only
  - Entries are Stored or Deflated. No two entries share a name.
  - Anything else (`index.html`, `TemplateData/`, `StreamingAssets/`, other `Build/` files, other root files) is **ignored**: never unpacked, never served.
  - **Any** symlink, absolute path, `..` component, backslash or drive-letter name rejects the zip, ignored or not.
- **NEW `G/src/share_zip.rs`:** `check_and_unpack(zip_path, staging_dir, limits) -> Result<Unpacked { title, build_name, bytes }, Reject>`, run in `spawn_blocking`. Checks in this order; each `Reject` maps to one Korean next-step message in `strings.rs`:
  1. Not a zip (no `PK` signature, or the archive won't open) → "zip 파일이 아니에요. Daedalus → Share my game 으로 만든 zip을 올려 주세요."
  2. More than 256 entries, a duplicate entry name, or any unsafe entry name or symlink → "이 zip은 열 수 없어요…" plus the Share hint. Unsafe = the hand checks (absolute, `..`, backslash, drive letter) **and** `enclosed_name()` returning `None` (also catches NUL and other bad names). Symlink = `unix_mode() & 0o170000 == 0o120000`; belt-and-braces, since nothing is ever extracted as a link.
  3. A whole Unity project (`Assets/`, `ProjectSettings/` or `Packages/manifest.json` at any depth) → "Unity 프로젝트 전체를 압축했어요…".
  4. Only image files → "스크린샷 폴더를 압축한 것 같아요…".
  5. A single top-level folder that contains `daedalus.json` → "zip 안에 폴더가 한 겹 더 있어요…". (A wrapper with `Build/` but no `daedalus.json`, e.g. a zipped Build Settings output folder, falls through to check 7's message.)
  6. No `Build/` → "Daedalus가 만든 zip이 아니에요…".
  7. `Build/` has `.gz` or uncompressed `.wasm`/`.data`, or no `daedalus.json` → "Build Settings 로 만든 빌드예요. Daedalus → Share my game 을 써 주세요…".
  8. `daedalus.json` over 4 KB, bad JSON, or `format` ≠ 1 → "Daedalus를 업데이트한 뒤 다시 Share my game…".
  9. Wrong set of build files or mismatched names → re-share message.
  10. Screenshot missing, without the PNG signature, too big, or with IHDR dimensions over the cap → re-share message.
  11. Title empty after `clean_label` (40 characters) → re-share message.
- **Unpacking:**
  - Only the six allowlisted files are written, into `staging_dir`, created with `create_new`, at paths built from the fixed names and the validated `build_name`, never from entry names.
  - Counted by **bytes actually written**, through a limited reader: a total over 120 MB stops and rejects. Header sizes are never trusted.
  - Entries using any compression other than Stored or Deflated are rejected.
- **`G/tests/support/mod.rs`:** `ShareZip` builder that writes a synthetic zip in memory:
  - tiny fake Build files
  - a hand-built minimal PNG (signature + IHDR)
  - `daedalus.json`
  - mutators for each bad case: symlink entry, absolute name, `..` name, duplicate name, oversized `daedalus.json`, wrapper folder (with `daedalus.json`), wrapped Build Settings folder (`MyGame/{index.html, Build/, TemplateData/}` → the Build Settings message), project folder, gzip build, missing metadata, format 2, big screenshot, bomb entry (small compressed, large uncompressed)
  - No binary fixture is committed.
- **Tests (unit in `share_zip.rs` or `tests/upload.rs`):**
  - one per reject case, including **separate tests for symlink, absolute path and `..`**
  - the zip bomb stops at the cap with no more than the cap written
  - extras are ignored and not on disk
  - a good zip unpacks exactly six files
- **Verify:** `[automated]` `cargo test`. This closes the Deferred.md symlink entry (deleted at cleanup).

### Step 10: Upload, replace in place and caps (server side)
- **NEW `G/src/upload.rs`:** `POST /upload?game=new|<id>`, body `application/zip`.
  - **Body limit, hand-written:** `DefaultBodyLimit` only limits buffering extractors, and this handler streams `Body` frames, so it doesn't apply here (the 2 MB default stays on every other route). The handler itself answers 413 at once for a `Content-Length` over `config.max_upload_bytes` (50,000,000), and otherwise streams the body (`http_body_util::BodyExt::frame`, or the body wrapped in `http_body_util::Limited`) into `staging/<random>.zip`, counting bytes and abandoning with 413 past the limit (covers a chunked body with no `Content-Length`).
  - **Before unpacking:**
    - `game=<id>` must be a live game owned by the user, else 404 (the teacher uses their own; another user's id is 404, fail closed)
    - `game=new` needs fewer than 10 live games (re-checked by the conditional insert (Step 4), so two concurrent new uploads can't both pass)
    - live bytes + the 120 MB worst case must fit the 20 GB cap; re-checked with `Unpacked.bytes` before the rename
    - the game must not be busy: a per-game busy set, so two uploads to one game → the second gets "업로드 중이에요". Removing a game or its owner's account takes the same busy lock (Steps 8 and 12).
    - a 3-permit unpack semaphore
  - **New game:** id = 10 random characters from `abcdefghjkmnpqrstuvwxyz23456789`, retried on collision with any row, tombstones included, so it is **never reused and never a slug**.
  - **New game order:** draw the id → unpack to `staging/<random>/` → rename into `games/<id>/v1/` → the conditional insert (Step 4). Zero rows inserted (the cap was reached meanwhile) → delete the folder and answer with the 10-game message. No row exists before its files do, so a failed unpack never shows a broken card.
  - **Swap (update):**
    1. unpack to `staging/<random>/`
    2. rename into `games/<id>/v<version+1>/`
    3. one UPDATE sets `version`, `title`, `build_name`, `bytes`, `updated_at` `WHERE id=? AND removed_at IS NULL AND version=<old>`; zero rows changed → delete the new folder and reject
    4. then delete the old version folder
  - A failure at any point deletes staging, and the old version keeps playing. The address `/g/<id>/` never changes.
  - **Startup:** delete `staging/*` and any `games/<id>/v*` that isn't the current version or belongs to a tombstoned game.
  - **Response:** JSON `{"ok":true,"play":"/play/<id>"}` or `{"ok":false,"message":"<Korean>"}`; 4xx on rejection.
  - **Logs:** accepted (id, version, bytes) or rejected (reason code).
- **Tests (`tests/upload.rs`):**
  - a new upload creates a game and its card data
  - re-upload as `game=<id>` keeps the id, bumps the version and changes the title label
  - a **failed re-upload** (bad zip) leaves the old version serving the old bytes
  - ids are never reused after removal (remove, then upload new 20 times: no id repeats, none equals the removed one)
  - 11th new game refused
  - another student's id → 404
  - over `max_upload_bytes` (50,000,000) → 413, both by `Content-Length` and as a chunked body without one
  - removing a game while its busy lock is held refuses, and an UPDATE against a tombstoned row changes nothing and leaves no folder
  - two concurrent `game=new` uploads at 9 live games: only one succeeds, and the other gets a 4xx with the 10-game message (not a database error) and leaves no folder
  - no session → 401 with the JSON body and its Korean message
  - cross-site → 403
  - the disk cap is refused (cap lowered in the test config)
  - startup sweep removes stale folders
- **Verify:** `[automated]` `cargo test`.

### Step 11: Upload page (browser)
- **MODIFY `G/src/upload.rs`:** `GET /upload` shows:
  - radio choices "내 게임 업데이트하기" (lists their live games by title with screenshot thumbnails) and "새 게임"
  - a file input (`accept=".zip"`)
  - a progress bar
  - a message area
- **MODIFY `G/static/gallery.js`:**
  - Checks size ≤ the page's `data-max-bytes` (rendered from `config.rs`; no second copy of the limit in JS) and a `.zip` name **before sending**, with plain Korean messages: Cloudflare shows its own error page above 100 MB, and our limit is lower.
  - Sends with `XMLHttpRequest` (for `upload.onprogress`) as `POST /upload?game=…`.
  - Shows the server's message, or goes to `play` on success.
  - Disables the button while sending.
- **Tests:** the page lists only the user's live games; `data-max-bytes` equals `config.max_upload_bytes`; the page has no inline script (CSP compliance).
- **Verify:** `[automated]` `cargo test`; the manual look comes in Step 22.

### Step 12: Game list, My games, screenshots, removing a game
- **NEW `G/src/gallery.rs`:**
  - **`GET /`:** a "내 게임" strip of the user's live games at the top (with an upload link when empty), then every live game as a card (screenshot, title, owner's **display name**), newest update first. Every card and thumbnail `<img>` has `loading="lazy"` and fixed `width`/`height`, so a class opening the list at once doesn't download every screenshot.
  - **`GET /shots/{file}?v=<version>`** (`file` = `<id>.png`, the suffix stripped in the handler; axum 0.8 captures take a whole segment): the live game's `screenshot.png` with:
    - `Content-Type: image/png` (fixed, never from the upload)
    - nosniff
    - `Content-Security-Policy: sandbox`
    - `Cache-Control: private, no-cache`
    - 404 for removed or unknown ids
    - the id is checked against the id alphabet before any lookup
  - **`GET /games/<id>/remove`:** a confirm page. **`POST`** to the same path, by the owner or the teacher, else 404: take the game's busy lock (refuse with "업로드 중이에요" if held), tombstone, delete the folder, log.
- **Tests (`tests/games.rs`):**
  - the card shows the title from metadata and the display name from the account (both wires)
  - a hostile title and display name render escaped
  - the screenshot headers are exactly as listed
  - a removed game: no card, screenshot 404, play 404
  - a student can't remove another's game (404)
  - the teacher can remove any game
  - cross-site POST → 403
- **Verify:** `[automated]` `cargo test`.

### Step 13: Play grant on the games host
- **MODIFY `G/src/play_token.rs`:** `ENTER_TTL_SECS = 60` and `GATE_TTL_SECS = 2 * 3600`. The `Key` doc records the decision: **a per-process key**. A restart invalidates every gate cookie, and reopening the game from the gallery gets a new grant.
- **NEW `G/src/play.rs`:**
  - **`GET /enter/<id>?grant=<token>`:**
    - refuses (403) unless `Sec-Fetch-Dest: iframe`, and, when `Sec-Fetch-Site` is present, unless it is `same-site`: a copied frame link opened in a tab or sent outside the class doesn't enter
    - verifies an `Enter` grant for that id (a live game)
    - sets `__Host-g_<id>=<Gate token>`; Secure, HttpOnly, SameSite=Lax, Path=/, Max-Age = gate TTL
    - 302 to `/g/<id>/`
    - an invalid or expired grant gets 403
  - **`/g/<id>/…`** requires a valid `Gate` cookie for that id and a live game, else a 403 page with one Korean line: "갤러리에서 게임을 다시 열어 주세요".
- **Tests (`tests/play.rs`):**
  - a good grant (with `Sec-Fetch-Dest: iframe`, `Sec-Fetch-Site: same-site`) → cookie with its attributes plus a 302 to the clean path (no query)
  - the same grant with `Sec-Fetch-Dest: document`, with no `Sec-Fetch-Dest`, or with `Sec-Fetch-Site: cross-site` → 403
  - an expired, tampered or other-game grant → 403
  - a gate cookie for game A doesn't open game B
  - a removed game → 404 even with a cookie
  - a fresh `AppState` (restart) rejects the old cookie
- **Verify:** `[automated]` `cargo test`.

### Step 14: Player page and build files
- **MODIFY `G/src/play.rs`:**
  - **`GET /g/<id>/`** is the server's player page:
    - full-window `<canvas id="unity-canvas" tabindex="-1">`
    - `data-loader`, `data-data`, `data-framework` and `data-code` attributes built from the stored `build_name` (escaped by maud)
    - `<script src="player.js">`
    - `lang="ko"`
  - **`GET /g/<id>/player.js`:** `static/player.js`. It reads the data attributes, loads the loader script, calls `createUnityInstance(canvas, {dataUrl, frameworkUrl, codeUrl, companyName, productName})`, focuses the canvas on load and on click, and shows a one-line Korean error if loading fails.
  - **`GET /g/<id>/Build/<file>`:** served only when `<file>` is one of the four names from the database. Read from `games/<id>/v<version>/Build/` with `files::response_headers`, so `Content-Encoding: br`, `Vary` and `Cache-Control: private, no-cache, no-transform` are kept from the spike. Everything else is 404, including `index.html` and `TemplateData`.
- **Tests:**
  - the player page names exactly the stored files
  - each Build file has the spike headers (`content-encoding: br` once)
  - an ignored file present in the zip is 404
  - path tricks (`../`, encoded `%2e%2e`) → 404
- **Verify:** `[automated]` `cargo test`.

### Step 15: Play-host CSP
- **MODIFY `G/src/guard.rs`** games layer CSP (PROPOSED; started strict):
  - `sandbox allow-scripts allow-same-origin allow-pointer-lock` on every HTML response (the player page and the 403 page), so a games-host page opened top-level is sandboxed exactly like the frame (no top navigation, no popups, no forms); `allow-same-origin` keeps IndexedDB saves
  - `default-src 'none'`
  - `script-src 'self' blob: 'wasm-unsafe-eval'` (Unity 6 may also need `'unsafe-eval'` for emscripten's dynCall; add it only if the console shows it)
  - `worker-src 'self' blob:`
  - `connect-src 'self' blob: data:`
  - `img-src 'self' blob: data:`
  - `style-src 'self' 'unsafe-inline'`
  - `media-src 'self' blob: data:`
  - `form-action 'none'`
  - `base-uri 'none'`
  - `object-src 'none'`
  - `frame-ancestors https://suwon.orcarin.com`
- The Chrome check (Step 22, on a scratch data folder before anything goes live) is the first real load under it; Step 17's loopback check only reads headers. Relax only what Chrome's console shows Unity needs, and record each relaxation in Notes. If `connect-src`, `img-src` or `form-action` has to open to another origin, record "a game can show a fake sign-in inside its frame and send it away" as an accepted risk in the ADR row.
- **Tests:** the CSP is on the player page, `player.js`, Build files, the 403 page and the 404 (layer wired); the `sandbox` directive is on the player page and the 403 page.
- **Verify:** `[automated]` `cargo test`.

### Step 16: Play view page on the gallery
- **MODIFY `G/src/gallery.rs`:** `GET /play/<id>` (live game, signed in) shows:
  - a top bar with "← 게임 목록", the title and display name, a "전체 화면" button and the note "키보드나 게임패드가 필요해요"
  - below it, a full-page `<iframe id="game" src="https://suwon-play.orcarin.com/enter/<id>?grant=<fresh Enter grant>">` with `sandbox="allow-scripts allow-same-origin allow-pointer-lock"` and `allow="fullscreen; gamepad; autoplay"`. Keep `gamepad`/`autoplay` only if the Chrome check (Step 22) shows they're needed and work; otherwise drop them and note it.
- **MODIFY `G/static/gallery.js`:**
  - focus the iframe on load
  - the fullscreen button calls `game.requestFullscreen()`
  - **no `message` listener anywhere** (grep)
- **Tests:**
  - the iframe has exactly the sandbox and allow values above
  - the grant in `src` verifies for this id
  - a removed or unknown id → 404
  - no `addEventListener("message"` in the static files
- **Verify:** `[automated]` `cargo test`; `[grep]` the message listener.

### Step 17: Share zip from the real build and loopback smoke check
- **NEW `G/examples/share_zip_from_build.rs`:** `cargo run --example share_zip_from_build -- <build dir> <out.zip> <title> <screenshot.png>`. It writes a Share zip in the contract's shape from a Unity build folder (Step 0's), using `TestResults/trap-dev-scene.png` (or any PNG) as the screenshot. Run it into `TestResults/gallery-fixture.zip`.
- **Loopback smoke** (scratch data folder in the scratchpad):
  - `gallery teacher create` there (scratch password only), then sign in with curl against `127.0.0.1:8470` sending `Host: suwon.orcarin.com` and `Origin: https://suwon.orcarin.com` (curl sends `__Host-` cookies over plain HTTP from its cookie jar with `-b`)
  - create a test student and upload the fixture zip
  - with `Host: suwon-play.orcarin.com`, `Sec-Fetch-Dest: iframe` and `Sec-Fetch-Site: same-site`, take a grant through `/enter`, then `curl -I` the Build files for their headers
  - The framed play needs the real hostnames over HTTPS, so it is checked in the Chrome check (Step 22).
- **Verify:** `[manual]` the zip is accepted; the headers match the spike's; record sizes in the phase log.

### Step 18: Backups
- **NEW `G/src/backup.rs`:** `gallery backup --data <dir> --to <dir>`:
  1. delete any leftover `<to>/db/*.tmp`, then `VACUUM INTO` a uniquely named temp file there (`VACUUM INTO` refuses an existing target), then rename it over `gallery-YYYY-MM-DD.db`; keep the newest 7
  2. then mirror `games/`: copy version folders missing from `<to>/games/`. A folder no longer in the source (a replaced version or a removed game) is **kept for 7 days** (its first-missing date recorded in `<to>/games/.gone`) and then pruned, so every kept database copy can be restored with its files. Version folders never change once written.
  3. one log line with the result
- Exit non-zero on any failure.
- **Restore** (a header comment in `backup.rs` and Notes item 3): bootout the server job, delete `gallery.db-wal` and `gallery.db-shm` (WAL mode; a live log replayed onto the restored file would corrupt or mix it), copy the chosen `gallery-YYYY-MM-DD.db` to `gallery.db` and the mirror's `games/` back, bootstrap the job.
- **What it protects against:** database corruption and mistakes (a removed game, a bad re-upload) within 7 days. Not losing the Mac Mini's disk: the backup sits on the same disk (off-machine copies deferred).
- **Tests:** a backup of a temp data folder opens as a valid database with the same rows; two backups on the same day both succeed; a run with a leftover `.tmp` succeeds; a removed game's folder stays in the mirror for 7 days (dates edited in `.gone`) and is pruned after; the 8th copy prunes the oldest.
- **Verify:** `[automated]` `cargo test`.

### Step 19: launchd jobs
- **NEW `Deploy/launchd/com.orcapafea.daedalus.gallery.plist`**, following Orcarin's user LaunchAgent:
  - `ProgramArguments`: `/Users/brisa/Library/Application Support/daedalus-gallery/bin/gallery`, `serve`, `--data`, `/Users/brisa/Library/Application Support/daedalus-gallery`
  - `WorkingDirectory` = the data folder
  - `RunAtLoad` and `KeepAlive` true; `ThrottleInterval` 10
  - stdout and stderr → `/Users/brisa/Library/Logs/daedalus-gallery/gallery.log`
  - A header comment holds the install commands, every path quoted (it contains "Application Support"):
    - `cargo build --release`
    - `install -m 0755` into `bin/`
    - `mkdir -p` the log folder
    - copy the plist to `~/Library/LaunchAgents/`
    - `launchctl bootstrap gui/$(id -u) …`
    - to redeploy: `launchctl kickstart -k gui/$(id -u)/com.orcapafea.daedalus.gallery`
  - **No secrets exist** (the play key is generated per process, and sessions live in SQLite), so there is no env file. If one is ever needed, it goes in a 0600 file read by a wrapper, never in the plist (Thoth's pattern).
- **NEW `…gallery-tunnel.plist`**, modeled on `com.orcarin.cloudflared.plist`:
  - `/opt/homebrew/bin/cloudflared tunnel --config /Users/brisa/.cloudflared/suwon.yml run suwon` (the form in suwon.yml's comment and framework.md; always pass `--config`). The config is a **copy** installed from the repo's `Deploy/cloudflared/suwon.yml`: a launchd job may be blocked by macOS privacy protection from reading `~/Documents`, and a `git pull` must not change the live tunnel. Redeploying the tunnel = copy the file, then `kickstart -k` the job.
  - `RunAtLoad` and `KeepAlive`
  - logs to `~/Library/Logs/daedalus-gallery/tunnel.log`
  - Credentials stay in `~/.cloudflared/`, found by tunnel id.
- **NEW `…gallery-backup.plist`:** `gallery backup --data … --to "/Users/brisa/Library/Application Support/daedalus-gallery-backups"` at `StartCalendarInterval` 03:30 (Octopus runs at 03:00); `RunAtLoad` false; log to `backup.log`.
- **MODIFY `Deploy/cloudflared/suwon.yml`:** comment names the tunnel job.
- `.gitignore`: nothing new. The data, backups and logs are outside the repo, and `TestResults/` is already ignored. Confirm with `git status`.
- **Verify:** `[manual]` `plutil -lint` on all three.

### Step 20: Run tests
- In `Deploy/gallery/`: `cargo test` and `cargo clippy --all-targets -- -D warnings`.
- Unity suites are not needed: no Unity file changed (`git status` shows none after Step 0's restore). The CLAUDE.md Testing table says gallery changes → cargo only.

### Step 21: Code review (before anything goes on the internet)
- Run the code-review agent (`.claude/agents/workflow/code-review.md`) on every changed file. Ask specifically about:
  - every gallery state change going through the same-origin layer, and no GET changing state (the enumerated POST and GET lists match the router)
  - every gallery page requiring a session
  - the games host never seeing the gallery cookie and never trusting anything but the grant or gate; `/enter` frame-only; the `sandbox` CSP on games-host HTML
  - zip handling: unsafe and duplicate names checked before any write, bytes counted as written, only allowlisted files written at paths from validated names, `create_new`
  - no content type, path or HTML from an upload except through maud escaping or `safe_join` / the stored names; `PreEscaped` only in the shell
  - the throttle failing closed (too early → no password check), the known-browser skip scoped to its login, and Argon2 bounded by the semaphore with its timeout
  - sessions and known browsers deleted on reset and on removal (both wires tested)
  - removal and upload serialized by the busy lock; the version UPDATE guarded
  - logs never containing a password, hash, session token, known-browser token, grant or gate value
  - both ends of every wire:
    - metadata title → card
    - account display name → card and header
    - `build_name` → player page → Build route
    - version → served folder and screenshot `?v=`
    - grant → cookie → files
    - the limits in `config.rs` → their checks
- Fix any HIGH/MED issues and re-run Step 20.

### Step 22: STOP for operator approval, then the Chrome check through the tunnel
- **Stop for the operator.** This puts the server on the internet through `suwon.orcarin.com`, first on a scratch data folder, then live under launchd (Step 23). Proceed only on approval.
- **Chrome check:** run the server in the foreground on the scratch data folder and the tunnel in the foreground (`cloudflared tunnel --config Deploy/cloudflared/suwon.yml run suwon`). Drive headed desktop Chrome with a throwaway CDP script in the scratchpad and a fresh `--user-data-dir` (as in the spike):
  1. The sign-in page; a wrong password; the wait message.
  2. Sign in as the scratch teacher, paste two students and see the preview, then create them; screenshot the slip page and its print preview.
  3. Sign in as a student. On the upload page: a non-zip and a file over 50,000,000 bytes are refused in the browser; the fixture zip uploads with the progress bar.
  4. The game list shows the card (screenshot, title, display name) and the My games strip.
  5. Play: the trap bench draws in the frame and the frame has focus (arrow keys move the player). Fullscreen works via an `Input.dispatchMouseEvent` click. Read the console for CSP violations.
  6. **Saves:** note the save probe's `PlayerPrefs` count from the frame's console, reload (the count increases), re-upload the same zip as "update", reopen: the count continues and doesn't restart at 1.
  7. Gamepad: in the frame, `navigator.getGamepads()` doesn't throw (permissions policy). If a gamepad is connected, press a button and see the player react; otherwise note "no pad".
  8. Restart the server and reload the frame: the Korean 403 line appears; "← 게임 목록", then reopen, plays again.
  9. Copy the frame's `/enter/…?grant=…` URL into a new tab: refused. Open `/g/<id>/` top-level with a valid gate cookie: it runs sandboxed (a `window.top.location` or `window.open` test from the console is blocked).
  10. Remove the game as the student, then remove a student as the teacher.
  11. Response headers seen in DevTools: gallery CSP and framing; play CSP with `sandbox`; `content-encoding: br` on Build files; the screenshot's `sandbox` CSP.
- Claude looks at every screenshot. Fix and re-run anything that fails (re-running Steps 20–21 for any server change). Record any CSP relaxation and its risk in Notes.
- **Operator look (short):** the operator reads the Korean pages, slip and messages and approves the wording or corrects it (edits only in `strings.rs`).
- Stop the foreground server and tunnel, then delete the scratch data folder (its passwords go with it).
- **Verify:** `[manual]` screenshots in `TestResults/gallery-phase5/`, inspected; results table in the phase log.

### Step 23: Live deploy (operator-approved in Step 22)
- Build release, install the binary and create the data, backup and log folders (0700). Copy `Deploy/cloudflared/suwon.yml` to `~/.cloudflared/suwon.yml`. Install and bootstrap the three jobs.
- **The operator** runs `gallery teacher create <id> "<name>" --data …` in their own Terminal, so the password never appears in Claude's transcript or the logs.
- **Smoke:**
  - `curl -I https://suwon.orcarin.com/` → 303 `/login` with the gallery headers
  - `curl -I https://suwon-play.orcarin.com/g/x/` → 403 or 404 with the play CSP
  - `launchctl print gui/$(id -u)/com.orcapafea.daedalus.gallery` shows it running
  - kill the process: launchd restarts it within ~10 s
  - `launchctl kickstart -k gui/$(id -u)/com.orcapafea.daedalus.gallery-tunnel`: the tunnel comes back under launchd (not only in a foreground run) and both hostnames answer
  - `curl -H 'Host: 127.0.0.1:8470' https://suwon.orcarin.com/` does not reach the gallery
  - the `orcarin` and `octopus` tunnels still serve their hostnames
- Run `launchctl kickstart gui/$(id -u)/com.orcapafea.daedalus.gallery-backup` once: a dated database copy and the games mirror appear.
- **Login limit:** LaunchAgents run only after the operator logs in, so after a power cut or update reboot the gallery stays down until someone logs in (FileVault blocks auto-login). Record whether auto-login is on, and state the limit in platforms.md (Notes item 3).
- The operator signs in as the teacher once.
- **Verify:** `[manual]` each smoke line recorded in the phase log.

### Step 24 (Final): Re-run after deploy
- `cargo test` and `cargo clippy --all-targets -- -D warnings` once more on the deployed commit. Any later fix re-runs Steps 20–21, then rebuild, reinstall, `kickstart -k` the live job and repeat the Step 23 smoke.
- Report the result to the user.

---

## Completion Criteria

- [ ] `cargo test` and `cargo clippy --all-targets -- -D warnings` green; the server binds only `127.0.0.1:8470`.
- [ ] Accounts:
  - Argon2id bounded at 2 concurrent hashes with a 3 s wait (tested), 50–100 ms measured on the Mac Mini
  - `__Host-` browser-session cookie; sessions in SQLite with a 1 h idle timeout and a 12 h absolute cap (both tested from real states)
  - sessions and known browsers deleted on password reset and on account removal; a reset clears the throttle entry
  - per-account doubling delay from 1 s, capped at 30 s, skipped by that account's known-browser cookie; unknown IDs never hashed; nothing per IP, no global budget
  - every gallery route built from one table that the route tests iterate
  - nothing visible without signing in
  - signed-in name and Sign out on every page
- [ ] `gallery teacher create|reset` works; no web route for it.
- [ ] Teacher page:
  - add one or paste many, with a preview marking errors
  - a printable slip shown once
  - reset shows the new password once
  - remove any game or account (with confirm)
  - every route refuses students
- [ ] Upload:
  - "update one of my games" or "new game"
  - 50,000,000 bytes (one value in `config.rs`) on that route only, streamed, with a browser size check and progress bar
  - a failed re-upload leaves the old version playing
  - same address forever; ids random and never reused
  - 10 games per account; disk cap
  - students remove their own games
- [ ] Share zip contract written into Notes for `platforms.md`. Every reject case gives a Korean next-step message. Symlink, absolute path and `..` each have a test. Zip bomb capped by bytes written.
- [ ] Hostile content: escaped titles and names (tested); screenshots checked for PNG signature and size, and served as `image/png` with nosniff and `CSP: sandbox`.
- [ ] Play:
  - dedicated page with a full-page frame from `suwon-play.orcarin.com`, the exact sandbox and allow values, focus, fullscreen, back link, keyboard/gamepad note
  - grant → `__Host-` cookie → 302 to `/g/<id>/`; `/enter` works only inside a frame; games-host HTML carries the `sandbox` CSP
  - per-process key (restart → reopen works)
  - server-owned player page
  - play CSP in place, with any relaxation recorded
- [ ] Every page in Korean with `lang="ko"` and UTF-8; all UI text only in `strings.rs`; the operator approved the wording.
- [ ] The Chrome check through the tunnel passed with the real Brotli build: sign in, upload, card, play, focus, fullscreen, gamepad policy, save kept across a re-upload. Screenshots inspected.
- [ ] Code review before going live: no open HIGH/MED.
- [ ] Live: server, tunnel and backup jobs installed and running (the tunnel reading its installed config copy); the operator's teacher account exists; one backup has run; the other tunnels are unaffected; the after-login limit recorded.

---

## Notes to GDD

Synced at completion (GDD v0.15 → v0.16). **PROPOSED** items keep their marker until the operator confirms them; drop it at sync when confirmed.

1. **systems.md § Share** (rewrite the bullets):
   - **Make the upload:** Daedalus → Share my game asks for a **title only** (the nickname prompt is dropped; Phase 6 `share-my-game` builds it). It writes one zip in the **Share zip** shape (platforms.md § Share zip). The rest of the bullet stays.
   - **Sign in:** one ID and password per student, plus the teacher's. Passwords are readable (`word-word-123`) and generated by the gallery. No email, no self-service recovery, no forced change. A forgotten password → the teacher resets it. The teacher's own account is made and reset by the operator on the Mac Mini. Signing in slows down after wrong passwords (up to 30 s between tries) without a hard lockout, and a browser that has signed in to that account before isn't slowed. Someone repeatedly trying a classmate's ID can keep that ID slowed on a browser the classmate has never used; a teacher reset clears it. A sign-in lasts until the browser closes, 1 hour idle, or 12 hours in all; on a shared laptop, press 로그아웃.
   - **Upload:** the student picks "update one of my games" or "new game". An update replaces that game at the same address, so players keep their saves. A failed upload leaves the old version playing. At most 10 games per student (PROPOSED). Students can remove their own games (PROPOSED). The gallery checks the zip and answers every mistake with a plain next step (whole Unity project zipped, a Build Settings build, not a zip, a screenshots folder…).
   - **Play:** the game list shows cards (title, the student's **display name** as the teacher typed it, screenshot) with "My games" on top. A game opens on its own page, full-window, with a fullscreen button, a way back, and a note that it needs a keyboard or gamepad.
   - **Teacher:** adds students one at a time or by pasting `ID, name` lines with a preview, prints a slip per student (address, ID, password) shown once, resets passwords (the new one shown once), removes any game or account.
   - **Language:** the gallery is in **Korean**. IDs and passwords are ASCII. Unity menu names in messages stay as Unity shows them.
   - One gallery serves every class (PROPOSED).
2. **platforms.md**, a new **§ Share zip**, the contract Phase 6 must produce exactly:
   - Files at the zip root (no wrapper folder): `daedalus.json` `{"format":1,"title":…}` (unknown keys ignored); `screenshot.png` (PNG, ≤ 1,000,000 bytes, ≤ 1280 × 720); `Build/<name>.loader.js`, `.data.br`, `.framework.js.br`, `.wasm.br` (exactly one each, one shared `<name>` of `[A-Za-z0-9_-]{1,64}`, Brotli only).
   - Entries Stored or Deflated, no duplicate names; ≤ 256 entries; ≤ 120 MB unpacked; ≤ 50,000,000 bytes zipped; `daedalus.json` ≤ 4 KB.
   - Anything else is ignored and never served (`index.html`, `TemplateData/`, `StreamingAssets/`, extra files).
   - A symlink, absolute path, `..`, backslash or drive-letter name rejects the zip.
   - The title is cleaned (control and bidi characters removed, ≤ 40 characters).
   - The gallery serves its own player page; the zip's `index.html` is never used.
3. **platforms.md § External Services**, gallery details:
   - **Server:** `gallery serve` on `127.0.0.1:8470`, routing by `Host` (only the two public hostnames; anything else is 404).
   - **Availability:** the jobs are user LaunchAgents, so after a reboot the gallery runs only once the operator has logged in to the Mac Mini (auto-login state as recorded in Step 23).
   - **Tunnel config:** the job reads `~/.cloudflared/suwon.yml`, a copy of `Deploy/cloudflared/suwon.yml` installed at deploy.
   - **Data folder:** `~/Library/Application Support/daedalus-gallery/` (`gallery.db`, `games/<id>/v<n>/`, `staging/`). Re-uploads unpack to a new version folder, then one database update switches to it.
   - **launchd jobs** in `Deploy/launchd/` (server, `suwon` tunnel, 03:30 backup: `VACUUM INTO`, 7 copies, plus a games mirror that keeps gone folders 7 days, in `~/Library/Application Support/daedalus-gallery-backups/`; PROPOSED). Restores any of the last 7 nights (the restore line from Step 18); same disk only, so it doesn't survive losing the disk.
   - **No secrets:** the play key is per process, so a restart ends open games' file access and reopening from the gallery fixes it.
   - **Sessions:** browser-session cookie, 1 h idle, 12 h absolute (PROPOSED). Gate cookie 2 h (PROPOSED). **Accepted:** sign-out can't clear the games host's cookies (another host), so a game opened on a browser stays playable from its history for up to 2 h after sign-out.
   - **Gallery CSP:** `default-src 'self'`, `frame-src` the games host, `frame-ancestors 'self'`, `form-action 'self'`.
   - **Games host CSP:** as built in Step 15 (including `sandbox` on its HTML, so a page opened outside the frame is still sandboxed), with any relaxation listed. `/enter` works only as a frame load (`Sec-Fetch-Dest: iframe`).
   - **Screenshots** are served from the gallery as `image/png` with nosniff and `CSP: sandbox`.
   - **Frame:** `allow="fullscreen…"` as verified.
4. **architecture.md § System Map**, a new row:
   - **Gallery server** (`Deploy/gallery/`, Rust crate `gallery`). Owns: accounts, sessions, uploads, the Share zip check, game files and versions, play grants, the gallery and games-host pages, backups.
   - Must not: listen beyond loopback; trust anything from an upload (names, types, paths, HTML); change state on GET or from another origin; serve a gallery page into another site's frame.
   - It shares nothing with the Unity package except the Share zip contract.
5. **Glossary.md:**
   - new **Draft**: Share zip, Display name (set by the teacher, shown on cards), Account slip, Player page (the games host's own page for a build), Game address (`/g/<id>/`, fixed for life)
   - update **Shared build** to say it arrives as a Share zip
   - **Play grant** unchanged
6. **decisions.md**, ADR rows (2026-11-03):
   - Accounts and sessions (Argon2 with a bounded queue, only existing accounts hashed so random IDs can't fill it, ID existence probeable and accepted since IDs are on slips; `__Host-` cookie, SQLite sessions, 1 h idle and 12 h absolute; per-account growing delay skipped by a known-browser cookie, a teacher reset clears it; nothing per IP; no global budget because it would be a whole-class lockout). Residual risk: a script can keep a classmate's ID slowed on a browser that classmate never used.
   - Frame-only entry and the `sandbox` CSP on games-host pages (a copied play link can't run a game outside the sandbox).
   - Teacher bootstrap through the operator's CLI only.
   - **Display name set by the teacher replaces the editor's nickname.** Supersedes "(title, nickname)" in the 2026-11-02 class gallery row.
   - Upload target chosen by the student; own-game removal; 10-game and disk caps (PROPOSED → operator).
   - Share zip contract and the server-owned player page.
   - Per-process play key; gate cookie 2 h, and the accepted risk that a game opened on a browser stays playable from history for that long after sign-out.
   - Play-host CSP, and the frame-phishing risk if it had to be relaxed.
   - **Korean UI**: every string in `strings.rs`; ASCII IDs and passwords; Phase 6 editor prompts stay English, while the messages the gallery shows are Korean.
   - One gallery for all classes.
   - launchd deployment and daily backups.
7. **Roadmap Milestone 3:**
   - Phase 5 `gallery-server` done (server live, accounts, upload, play, backups).
   - Next: Phase 6 `share-my-game`, which must produce exactly the Share zip and drops the nickname prompt.
   - **Classroom launch and sharing wait for Phase 6.**
   - The "Class gallery" pre-launch gate stays.
   - Add a §0 row when the milestone closes, after Phase 6.
8. **CLAUDE.md:**
   - Layout: `Deploy/` holds the gallery server crate, `Deploy/launchd/` and the tunnel config (drop "arrives with `class-gallery`").
   - Project Config Networking: the gallery is built, in Korean; students upload in the browser.
   - Build/run: `gallery serve|teacher|backup`, the `share_zip_from_build` example, and the redeploy line.
9. **framework.md:** a new short § Gallery server (Axum 0.8 / sqlx / Chrome), with whatever this phase finds. Candidates:
   - `DefaultBodyLimit` limits only buffering extractors; a streamed `Body` needs its own limit
   - launchd jobs and macOS privacy protection on `~/Documents` (configs installed outside it)
   - Chrome's permissions policy for gamepad in a cross-origin frame
   - what Unity's loader needs in the CSP
   - launchd not creating log folders
10. **Deferred.md:** delete "The gallery's file serving doesn't check symlinks" (its trigger fired; Step 9 closes it). Keep "The spike's WebGL build script and save probe stay in `Assets/Dev`"; its trigger is Share my game (Phase 6).

**Open questions for the operator** (defaults in § Operator decisions):
- Teacher uploads (yes); bulk paste all-or-nothing (yes). Reviewer recommends: accept both — teacher uploads post an example level through the student path; all-or-nothing keeps printed slips matching what was created (round 1, R2).
- The numbers in item 8. Reviewer recommends: idle 1 h, absolute 12 h, plus a "press 로그아웃 on a shared laptop" line (round 1, R1); folded in as the default.
- Backups kept on the same Mac Mini only (yes, off-machine deferred). Reviewer recommends: accept same-machine for v1 only with the 7-day retention fix, say what it protects against, and defer copying `gallery-*.db` to a second disk or cloud folder with that as the trigger (round 1, R3). If kept, file a Deferred.md entry at cleanup: "Gallery backups live on the same disk" — trigger: the operator adds a second disk or cloud folder, or before the classroom launch.

**If anything here contradicts the Overview, fix the Overview.**

---

## Post-Phase Cleanup
After code review passes:
1. Move this prompt to `Documents/Phases/archive/`.
2. CLAUDE.md has no phase-status line; apply Notes item 8.
3. Write `.claude/memory/logs/phases/phase5-log.md`. Per step: the Argon2 timing, the fixture sizes, the Chrome check results table with screenshots, CSP relaxations, the live smoke results and the operator's answers.
4. Update `MEMORY.md` current state.
5. Apply the Notes to GDD: GDD v0.16 header, Changelog entry, `systems.md`, `platforms.md`, `architecture.md`, `Glossary.md`, `decisions.md`, Roadmap Milestone 3, `framework.md`, and `lessons-learned.md` if a rule was learned.
6. **Deferred items:** delete the symlink entry (Notes item 10). File any leftover found during the phase (Source = Phase 5), each with a trigger. See CLAUDE.md → Log Discipline.
7. Ask the operator, then commit all phase work as `v0.16: Phase 5 — gallery server: Korean class gallery with accounts, teacher page, upload and play, live under launchd`. Confirm `Assets/Settings/UniversalRP.asset` isn't in the commit.

## Workflow
Execute steps sequentially. Proceed to the next step without waiting for user approval. Stop only if blocked, at Step 0 if the Unity Editor has the dev project open (ask the operator to close it), and at Step 22, the planned operator stop before anything goes on the internet (it also holds the operator's look at the Korean wording). In Step 23 the operator runs the teacher-create command themselves. If blocked, stop and report: what step, what was attempted, what failed.

---

## Review round 1

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

## Review round 2

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

## Review round 3

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
