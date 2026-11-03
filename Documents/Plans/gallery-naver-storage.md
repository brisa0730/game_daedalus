# Plan: Gallery Naver Storage

**Date:** 2026-11-04 | **Status:** approved (handshake round 2, 2026-11-04; critic-reviewed) — not yet executed

## Overview

**Where things stand:** Games in the class gallery load slowly, from school too, because the service that carries the gallery to the internet sends Korean visitors through Los Angeles. A game's files cross the Pacific twice on every load.

**What this plan changes:**
- First, a test: one game is put on Naver's storage in Korea and opened from school. Nothing is built unless it loads fast there.
- Every uploaded game is then also copied to that storage, and players download the game from it. The gallery's pages stay where they are.
- The gallery's own computer keeps the original of every game. If loading from Naver fails, the player tries once more from the gallery's computer, the old, slower way.
- Each time the gallery starts, it checks Naver's copies against its own and fixes any difference. That also moves the class's existing games across.

**How you'll know it worked:** A game opened from the school network starts within seconds, and the class's existing games all play from Naver.

**Not in this plan:** moving the gallery's pages, or Orcarin, off the current route.

**Why a plan and not a patch:** it adds an outside service with a secret key, and changes uploads, removal, playing and the security rules for games.

**Design choice:** the gallery's computer keeps the original of every game and Naver holds a copy that the gallery checks and repairs at every start. A failed copy, a restored backup or an outage then never loses a game, backups stay as they are, and turning Naver off is deleting one settings file.

---

## Source
- **Trigger:** ISS-002 root cause (2026-11-04): Cloudflare serves our zone from LAX (`colo=LAX`, ~258 ms) on the office's KT line, and the operator found the download slow from the school network too. Operator chose Naver Cloud Object Storage over serving the Mac Mini directly and over the office NAS (it holds sensitive files) on 2026-11-04.
- **Related issues:** ISS-002.
- **Scope:** the class gallery (`Deploy/gallery/`) publishes each game's four build files to a public Naver Object Storage bucket (Korea region); the player page loads them from there with a local fallback; a one-time bucket setup script.
- **Depends on:** v0.22. The operator creates the Naver account, bucket and a **sub-account key limited to Object Storage**, and writes the key into a file on the Mac Mini themselves (Step 1). The key never goes through the chat or the repo.

**Facts this plan rests on** (Naver's docs and price page, 2026-11-04): S3-compatible API at `https://kr.object.ncloudstorage.com`, path- or virtual-host-style; object ACLs; Put/Get/Delete Bucket CORS (needs `Content-MD5`). Prices before VAT: storage 28원/GB-month, internet egress 100원/GB (≤ 5 TB), GET 4원 per 10,000. **Unverified until Step 1:** that an object stored with `Content-Encoding: br` metadata is served with that header (Unity's `.br` files need it), whether `Range` gets a 206, and the speed from school.

**Loader facts** (critic, from the code and the pinned Share settings): `loader.js` and the framework load through `<script>` tags without `crossorigin` (need `script-src`, not CORS); the data file uses `fetch` and the wasm `instantiateStreaming(fetch)` (need `connect-src` and `Access-Control-Allow-Origin`). Data Caching and threads are pinned off, so requests are simple (no preflight) and `worker-src` needs nothing. Both sandboxes keep `allow-same-origin` (`gallery.rs:161`, `guard.rs:33`), so the Origin sent is `https://suwon-play.orcarin.com`; without it the Origin would be `null` and CORS would fail.

## File Manifest

| Action | File | Purpose |
|--------|------|---------|
| NEW | `Deploy/naver/bucket.py` | Stdlib-only SigV4 helper: `setup --origin O` (CORS GET/HEAD for the games host), `put-test`, `headers KEY`, `remove-test`; never prints the secret |
| NEW | `Deploy/gallery/src/storage.rs` | `Storage` trait (`put`, `delete_prefix`, `list_prefix`); `NaverStorage` (S3 over HTTPS, SigV4); `StorageConfig` from `<data>/storage.json`; `public_origin()` derived from the configured endpoint + bucket (one source for page URLs and CSP); `publish(game)` and `reconcile()` |
| NEW | `Deploy/gallery/migrations/0003_published_version.sql` | `games.published_version INTEGER NULL` |
| MODIFY | `Deploy/gallery/Cargo.toml` | `reqwest` (rustls, no default features) |
| MODIFY | `Deploy/gallery/src/config.rs` | Optional `storage: Option<StorageConfig>`; validation (file mode 0600, all fields, bucket name lowercase/digits/hyphens, no dots) |
| MODIFY | `Deploy/gallery/src/app.rs` | `AppState.storage: Option<Arc<dyn Storage>>`, `AppState.play_csp: String`; `games_layer` via `from_fn_with_state` |
| MODIFY | `Deploy/gallery/src/guard.rs` | Play CSP built from config: `public_origin()` added to `script-src` and `connect-src` only when configured; otherwise byte-identical to today's |
| MODIFY | `Deploy/gallery/src/upload.rs` | Publish inside the request, while the busy locks are held, after the version switch; conditional `set_published`; delete the old version's prefix |
| MODIFY | `Deploy/gallery/src/gallery.rs` | `remove_game`: after tombstone and local delete, spawn the bucket delete outside the lock (logged) |
| MODIFY | `Deploy/gallery/src/db.rs` | `published_version` on `Game`; `set_published(id, version)` `WHERE id=? AND version=? AND removed_at IS NULL`; `clear_published(id, version)`, conditional the same way |
| MODIFY | `Deploy/gallery/src/play.rs` | `player_page`: bucket URLs when published (local URLs in `data-fallback-*`); `?local=1` forces local URLs |
| MODIFY | `Deploy/gallery/static/player.js` | Passes `showBanner` so every loader error counts as a failure; on a failure with bucket URLs, reload once with `?local=1`; otherwise the error line as now |
| NEW | `Tools/gallery_load.py` | The headed-Chrome CDP probe used throughout (seconds until Unity starts, build-file hosts and headers, console errors, optional throttle and screenshots); from the scratchpad's `cdp_start.py` |
| MODIFY | `Deploy/gallery/src/main.rs` | After the startup sweep: spawn `reconcile()` when storage is configured |
| MODIFY | `Deploy/gallery/src/cli.rs` | `storage check --data DIR` (put, public GET, delete a probe; no secrets printed) |
| NEW | `Deploy/gallery/tests/storage.rs` | Fake-storage tests for publish, serve, fallback URLs, update, remove, failure, reconcile, CSP; `NaverStorage` request-building test (headers, ACL, signature vs AWS's SigV4 test vector) |
| MODIFY | `Deploy/gallery/tests/support/mod.rs` | `TestApp` with an injected fake storage |
| MODIFY | `.claude/CLAUDE.md` | Layout (`Deploy/naver/`); Project Config → Networking and Build/run (the bucket, `storage.json`, `gallery storage check`, `bucket.py`) |

Live, outside the repo: the Naver account, bucket, sub-account and key; `~/Library/Application Support/daedalus-gallery/storage.json`.

---

## Steps

### Step 1: Naver setup and the test page (hand-off: the operator)
- **Operator:** create the Naver Cloud account (payment method registered), a Korea-region Object Storage bucket (globally unique, lowercase, no dots, e.g. `daedalus-suwon-games`), and a **Sub Account** with Object Storage permission only (limited to this bucket if the console allows), then its access key. Write `~/Library/Application Support/daedalus-gallery/storage.json` (`{"endpoint":"https://kr.object.ncloudstorage.com","region":"kr-standard","bucket":"…","access_key":"…","secret_key":"…"}`) and `chmod 600` it; keep a copy of the key in a password manager (backups don't include it). If the console offers a monthly cost alert, set one (e.g. 10,000원).
- **NEW `Deploy/naver/bucket.py`**; `put-test` uploads the test game's four build files under `test/Build/` with the headers `files.rs` sends today (`Content-Type` by type, `Content-Encoding: br` for `.br`), `Cache-Control: public, max-age=31536000, immutable`, `x-amz-acl: public-read`, plus `test/index.html` and `test/player.js` (relative paths).
- `bucket.py` and the test page use the **virtual-host origin** `https://<bucket>.kr.object.ncloudstorage.com`, exactly as `public_origin()` will build it (certificate, CORS and speed can differ from the path-style host). `put-test` prints how long each of the four puts took (publishing runs inside the upload request, which Cloudflare drops at ~100 s).
- **Verify on the Mac:** `headers test/Build/game.wasm.br` shows `content-encoding: br` and `content-type: application/wasm`; a `Range: bytes=0-99` request's status recorded (206 or 200); the four put times recorded; headed Chrome (`python Tools/gallery_load.py <test page URL>`) starts Unity.
- **If `Content-Encoding` is not served:** stop and report (the fallback, Unity's decompression fallback in Share my game, changes the editor and is a separate decision).

### Step 2: School gate (hand-off: the operator at school)
- The operator opens the test page from school in Chrome and reads the time until the level shows (or `game.wasm.br`'s Time in DevTools → Network).
- **Gate:** Unity starts within **15 s**. Otherwise stop: record the numbers in ISS-002, `remove-test`, and the plan ends here.

### Step 3: CORS, checked cross-origin in a browser
- `bucket.py setup --origin https://suwon-play.orcarin.com --origin http://127.0.0.1:8765` (the second is temporary, for this check).
- A local page on `http://127.0.0.1:8765` served with the new play CSP (bucket origin in `script-src`/`connect-src`) inside a sandboxed iframe (`allow-scripts allow-same-origin`) loads the test build from the bucket in headed Chrome: Unity starts, no CSP or CORS error. A `GET` with `Origin: https://suwon-play.orcarin.com` returns `access-control-allow-origin` and `Vary: Origin` (record if `Vary` is missing).
- Then `setup --origin https://suwon-play.orcarin.com` only, and `remove-test`.

### Step 4: Storage module, settings and CSP
- **`storage.rs`**: the trait (boxed futures, crate style); `NaverStorage` with virtual-host URLs built from the configured endpoint's host and the bucket, SigV4 with the crate's `hmac`/`sha2`, `x-amz-acl: public-read` on puts, ListObjectsV2 (keys by scanning `<Key>` elements, continuation tokens), DeleteObject per key, 30 s per request. Errors carry no headers or config (the secret never reaches a log).
- `public_origin()` is the single source for page URLs and the CSP.
- **`config.rs`**: `storage.json` absent → `None` (today's behavior); present but group/other-readable, missing a field, or a bucket name with dots/uppercase → `tracing::error!` naming the problem (never the secret) and run **without** storage: refusing to start would put the gallery in a launchd restart loop, the same reason `main.rs` logs and serves after a failed sweep, while serving from the Mac is fully correct. `gallery storage check` reports the same problems and is the operator's check before a restart (Step 8).
- **`guard.rs`/`app.rs`**: the play CSP string is built once; `games_layer` reads it from state.
- **Tests:** SigV4 matches AWS's documented example; a built put request carries `Content-Type`, `Content-Encoding`, `Cache-Control`, the ACL and the signature headers; without storage the CSP equals today's string exactly; with storage it contains `public_origin()`.

### Step 5: Publish, serve, fall back, remove
- **Migration 0003**: `published_version INTEGER NULL`.
- **Bucket keys** are unique per upload: `games/<id>/v<version>-<updated_at>/Build/<file>` (`updated_at` from the row the version switch wrote). A version number can repeat after a backup restore (`upload.rs:256` counts from the restored row), and browsers keep `immutable` keys for a year; the timestamp makes a repeated version a new key. One function builds the prefix from a `Game` row; reconcile and the page use it.
- **`upload.rs`**: after `set_version` (new game: after `insert_game`), still inside the request and its busy locks: publish the four files, then `set_published(id, version)` (conditional: `WHERE id=? AND version=? AND removed_at IS NULL`), then `delete_prefix` of the previous upload's prefix, built from the pre-update row the request already holds (`Target::Update(game)`). A storage error, or the request being dropped mid-publish, leaves `published_version` unset; the upload answers `ok` (when it still can) and the game plays from the Mac until the next start's reconcile.
- **`play.rs` `player_page`**: storage configured **and** `published_version == version` **and** no `?local=1` → the four `data-*` URLs are `public_origin()` + the game's prefix + `<file>`, plus `data-fallback="1"`; otherwise local `Build/<file>` as now (so with storage turned off, a published row still plays locally under today's CSP). `build_file` is unchanged (the fallback still has the gate, the ETag and `Accept-Ranges: none`).
- **`player.js`**: passes `showBanner(message, type)` to `createUnityInstance`. Unity's loader reports a framework that fails to load or parse, and a data file served without its `Content-Encoding`, only through `showBanner(…, 'error')`; its start promise never settles (checked in the 6000.6 Share build's `game.loader.js`: the framework `<script>`'s `onerror` and the missing-`unityFramework` path). So `type == 'error'`, a rejected promise and a loader-script error all count as a failure: with `data-fallback`, `location.replace` the same page with `?local=1` once; otherwise the error line as now. Other banner types go to the console. A `started` flag makes the failure handler do nothing once `started()` has run (runtime errors use the loader's `errorHandler`, not `showBanner`, but the guard is cheap).
- **`gallery.rs` `remove_game`**: tombstone, local delete, then spawn `delete_prefix("games/<id>/")` outside the busy lock (ids are never reused); a failure is logged and the next start's reconcile retries it.
- **Tests** (fake storage): upload publishes 4 keys with the right headers and records `published_version`; the page names bucket URLs, `data-fallback` and the matching CSP origin; `?local=1` names local URLs; **no storage + a published row → local URLs and today's CSP**; an update publishes the new prefix, then deletes the old one; a publish finishing after a removal or a newer version leaves `published_version` untouched; a failing put, and a publish whose future is dropped, leave no `published_version` and local URLs; removal (student and the teacher's remove-student path) deletes the prefix without holding the lock; `player.js` passes `showBanner` and contains the `?local=1` reload; without storage every existing test passes unchanged.
- **Headed Chrome check of the fallback**, for **each** of the four files in turn (loader, framework, data, wasm): a local server serves the player page with that one bucket URL forced to 404; the page reloads with `?local=1` and Unity starts. Repeat the framework case with a **6000.0** Share build (`python Tools/share_check.py --editor 6000.0` makes one) to confirm its loader has the same failure shape.

### Step 6: Reconcile at start
- **`storage.rs` `reconcile()`**, spawned after the startup sweep, while the server already takes uploads. Order matters:
  1. **List the bucket first** (`games/`), grouping keys by game id and prefix.
  2. For each game id seen in the listing or live in the database: take its busy lock (skip if busy; the next start fixes it), then **re-read its row under the lock** and decide from that row only. Live game: if all four keys exist under its current prefix → `set_published(id, version)`; else `clear_published(id, version)` (conditional on the version, like `set_published`), publish, `set_published`. Then, still under the lock, delete every listed prefix of that game that isn't the row's current prefix. Removed or unknown game: delete all its prefixes.
  3. Never touches local files. Logs a one-line summary (published / repaired / deleted / skipped / failed).
- This also moves the existing games, repairs after a restored backup or a changed bucket, and retries failed deletes.
- **Tests:** reconcile publishes an unpublished game, repairs a game whose objects are missing though the column says published, deletes an orphan prefix and an old upload's prefix, skips a game locked by an upload, and **an upload landing between the listing and that game's turn keeps its new prefix and its `published_version`**.
- **`cli.rs` `storage check`**: put, public GET, delete a probe object; prints only OK/failed with the HTTP status.

### Step 7: Run Tests
- `cargo test`, `cargo clippy --all-targets -- -D warnings` in `Deploy/gallery/`.

### Step 8: Deploy
- `gallery backup` first. `gallery storage check` (it must pass before the restart). Build, install, restart (with `storage.json`). Wait for reconcile's summary in the server log.
- **Verify:** every live game has `published_version == version` (SQL count); the bucket listing holds exactly those games' current prefixes.

### Step 9: Look and timing (last part hand-off: the operator at school)
- Headed Chrome (fresh profile) on the live gallery: three games start; the Network events show the four build files from the bucket host with `content-encoding: br`; no CSP or CORS errors; a test game removed in the gallery has its prefix gone. Screenshot one started game.
- Storage off (move `storage.json` aside, restart): games play from the Mac; restore and restart.
- **Operator at school:** open two games from the gallery and read the start time.

### Step 10: Verify
- Walk the Completion Criteria.

### Step 11: Code Review
- Code-review agent on all changed files; fix HIGH/MED.

---

## Completion Criteria

- [ ] Step 1: the bucket serves `content-encoding: br` on the virtual-host origin; the `Range` behavior and the four put times recorded
- [ ] Step 2 gate: from school, the test page started Unity within 15 s
- [ ] Step 3: a sandboxed cross-origin page loads the build from the bucket in Chrome with no CSP or CORS errors; `Vary: Origin` recorded
- [ ] `cargo test` and clippy pass; the tests cover both ends of every wire: request headers as built, page URLs and CSP from one origin, the fallback reload, conditional `set_published`, removal off the lock, reconcile; no-storage behavior unchanged
- [ ] The fallback reload starts a game in Chrome when any one of the four bucket files is missing (each tested), with the 6000.6 and 6000.0 loaders
- [ ] A bad `storage.json` logs the problem and the gallery serves from the Mac (no restart loop)
- [ ] Live: every live game published, the bucket holds only current versions, Chrome loads build files from the bucket with no console errors; storage off plays from the Mac
- [ ] From school: gallery games start within 15 s

---

## Notes to Spec / Open Risks
- **Public build files.** Anyone can download a build by its URL; the game still runs only inside our sandboxed player page with its CSP. Accepted by the operator (2026-11-04). This and the stored key supersede `decisions.md`'s 2026-11-03 rows ("no stored secret", "no secrets to keep"); a new ADR row records it.
- **Cost exposure.** Egress is metered (~100원/GB; 1,000 full downloads of a 10 MB game ≈ 1,000원). The cost alert (Step 1) is the guard.
- **The secret.** Only in `storage.json` (0600), written by the operator, scoped to Object Storage. Never logged; not in backups (the operator keeps their own copy).
- **ISS-002's safeguard.** The bucket may answer `Range` with 206, so `Accept-Ranges: none` no longer covers bucket loads; versioned, `immutable` keys mean no revalidation. Step 1's result goes into ISS-002. Disposition: resolve ISS-002 if Step 9's school loads start within 15 s, with the unexplained cached-partial stall kept as a Changelog note and a Deferred entry for the load watchdog; otherwise keep it open with the numbers.
- **Reconcile and uploads.** Reconcile lists the bucket first, then takes each game's busy lock and re-reads its row before deciding; a game busy at that moment is skipped and fixed at the next start.
- **Publish time.** Publishing ~10 MB to Naver runs inside the upload request after the zip crossed the tunnel; Step 1 measures it. A request dropped mid-publish is safe: no `published_version`, local play, reconcile repairs.
- **Orcarin and the gallery pages** still go through LAX; their files are small. Out of scope.

---

## Post-Plan Cleanup
After code review passes:
1. Append execution trailers to this plan file.
2. Move this plan to `Documents/Plans/archive/`.
3. GDD `systems.md` § Share (Safety: "serves only the build's four files" reworded; Play: files from Naver with a local fallback); GDD version bump. `decisions.md` ADR row (Naver Object Storage for build files; alternatives: the Mac Mini direct, the office NAS, a paid Cloudflare plan; supersedes the "no stored secret" points). `framework.md`: Naver Object Storage notes learned. Roadmap checked.
4. Changelog new version; ISS-002 per the disposition above.
5. Deferred.md: scan triggers; file leftovers (moving the gallery pages, the load watchdog if not built) with triggers.
6. Commit all plan work.

**Critic findings folded in (2026-11-04):** HIGH: play-time fallback (`?local=1` reload); sync replaced by in-server reconcile under the busy locks (no race with uploads, repairs restores); conditional `set_published` and publish inside the request's locks. MED: sub-account key and the superseded decisions rows; a cross-origin browser check before building (Step 3); the `Range` safeguard recorded; removal's bucket delete off the lock with reconcile retries; request-building test and one origin function. LOW: bucket name validation; bucket change handled by reconcile; `Vary: Origin` checked; the operator keeps the key; `bucket.py` takes the origin as an argument; template steps split, operator hand-offs marked; CLAUDE.md Layout and the GDD Safety wording in cleanup.

## Workflow
Execute steps sequentially. Proceed to the next step without waiting for user approval, except at the operator hand-offs (Steps 1, 2 and the end of 9). Only stop if blocked. If blocked, stop and report: what step, what was attempted, what failed.

## Review round 1

# Review round 1: gallery-naver-storage

**What I could run:** `device_bash` over the Mac mini folder (grep, sed, find, ls). I read the plan whole, `log.md`, and
`Deploy/gallery/src/{play,guard,gallery,upload,files,main,app,backup,config,db}.rs`, `static/player.js`, `Cargo.toml`,
`decisions.md` rows 58-59, GDD `systems.md` § Safety, `Issues.md` ISS-002, and the Unity loader the spike build ships
(`TestResults/gallery-spike/dev/Build/dev.loader.js`).
**Not run:** no cargo, no Chrome, no Naver request. Everything about Naver's behaviour (Content-Encoding, Range, ACL,
CORS) is unverified by me, as the plan itself says.

**Verified:** `gallery.rs:161` and `guard.rs:33` both carry `allow-same-origin` (so the Origin sent is the games host).
`PLAY_CSP` (`guard.rs:26-29`) has `script-src 'self' blob: 'wasm-unsafe-eval'` and `connect-src 'self' blob: data:`,
so the bucket origin must be added to exactly those two, as planned. The loader fetches data and wasm with plain
`fetch` (`mode` default, no credentials, `dev.loader.js:685-700`, `608-617`), so simple CORS GETs; framework via a
`<script>` element without `crossorigin` (`:746-748`). `player_page` (`play.rs`) emits the four `data-*` URLs and
`build_file` sends `Accept-Ranges: none` and a version ETag, as described. `upload.rs:260` switches the version
before deleting the old folder, so "after `set_version`" is a real point. The teacher's remove-student path calls
`gallery::remove_game` (`teacher.rs:394`), so one change covers both removals. `backup.rs` copies only the database and
`games/`, so `storage.json` is indeed not in backups. Cost: 1,000 × 10 MB = 10 GB × 100원 = 1,000원 ✓; real brotli
builds are ~10 MB (`gallery-spike/brotli/Build`: 7.3 MB wasm + 2.8 MB data). `decisions.md` rows 58/59 contain the
"no stored secret" / "no secrets to keep" text the plan supersedes ✓.

---

## F1: the fallback never fires when `framework.js` fails to load
**Type:** blocking · **Category:** accuracy · **Severity:** HIGH · **Location:** Step 5 `player.js`; Overview "If
loading from Naver fails, the player tries once more"; Design choice.

The plan's fallback triggers on "loader error or rejected `createUnityInstance`". A framework failure is neither. In
Unity's loader, `downloadFramework()` returns a promise whose `script.onerror` only calls `showBanner(…, 'error')`
(`dev.loader.js:788-790`), and its `onload` with a missing `unityFramework` does the same (`:752-777`); neither path
rejects or resolves. `showBanner` without a `config.showBanner` just does `console.error` (`dev.loader.js:2-16`), and
`player.js` passes no `showBanner`. So when the bucket's `framework.js.br` 404s, is denied (a put that missed the
ACL), or is blocked by `script-src`, `createUnityInstance` never settles: the loading bar spins forever and the
student sees no fallback and no error line. The same holds for a missing `Content-Encoding` on the data file
(`:713-724`, banner only).

The bucket adds exactly the partial-failure modes this misses (one object missing or private while the other three
are fine). The fallback is the plan's main safety net ("an outage never loses a game"), so this is blocking.

**Fix:** give `createUnityInstance` a `showBanner(msg, type)` in `player.js` that treats `type == 'error'` as a load
failure (fallback reload when `data-fallback` is set, else the error line). Then make Step 5's headed check force the
failure on **each** of the four files in turn (loader, framework, data, wasm), not "a bucket URL", and assert the
`?local=1` reload for each. Please confirm the loader in a current 6000.0 and 6000.6 Share build has the same
`downloadFramework` shape (I read the spike's `dev.loader.js` only).

## F2: reconcile's delete pass and per-game repair need an order and a re-read under the lock
**Type:** non-blocking · **Category:** design · **Severity:** MED · **Location:** Step 6.

Reconcile runs while the server is already taking uploads. The plan doesn't say when it reads the live games'
versions relative to listing `games/`, or whether a game is re-read after taking its busy lock. Two consequences:
(a) if the live-version snapshot is taken before the listing, an update that finishes in between leaves its new
`v<N+1>/` prefix in the listing but not in the snapshot, and the delete pass removes the version that is now live
(`published_version` says N+1, every play 404s to the fallback until the next restart); (b) if a game's row is read
before its lock and an upload lands in between, reconcile's unconditional `clear_published(id)` clears the new
version's flag and its publish of the old version fails (the upload already deleted that folder, `upload.rs:262`).
**Fix:** list the bucket first, then for each game take the lock and re-read the row before any decision; in the
delete pass, take the owning game's lock and re-check its current version before deleting a prefix; make
`clear_published` conditional on the version too. Add a test with an upload between list and delete.

## F3: "storage off" must ignore `published_version`, and a test should hold it
**Type:** non-blocking · **Category:** design · **Severity:** MED · **Location:** Step 5 `play.rs`; Step 9 "Storage
off".

Step 5 says "published and no `?local=1` → bucket URLs". After storage is turned off (the plan's one-file off switch),
every game still has `published_version` set. The condition must be "storage configured **and** published", else the
page names bucket URLs that the (now bucket-less) CSP blocks. Step 9 checks it by hand, but "without storage every
existing test passes unchanged" never has a row with `published_version` set. **Fix:** state the condition and add a
test: no storage + a published row → local URLs and today's CSP.

## F4: refusing to start on a bad `storage.json` takes the whole gallery down in a launchd loop
**Type:** non-blocking · **Category:** design · **Severity:** LOW · **Location:** Step 4 `config.rs`.

`main.rs` already chose "log and serve" for the startup sweep because "launchd would restart it forever". A
mode-0644 or misspelled `storage.json` would now keep every page and upload down until the operator notices, while
the degraded mode (serve from the Mac, no reconcile) is fully correct. **Fix:** either log loudly and run without
storage, or keep the refusal but say why it beats degrading (and make `gallery storage check` the operator's
pre-restart check in Step 8).

## F5: `immutable` version keys can be reused after a backup restore
**Type:** non-blocking · **Category:** design · **Severity:** LOW · **Location:** Step 5 key layout; Notes "repairs
after a restored backup".

The next version is `game.version + 1` from the database (`upload.rs:256`). Restore a backup taken at v2 when the
game was at v3, and the next upload is v3 again with different files, under the same bucket key that browsers hold
with `max-age=31536000, immutable`; they keep playing the pre-restore game for up to a year. (Today's ETag
`"<id>-v3"` has the same reuse, but `no-cache` at least revalidates.) Reconcile repairs the bucket, not caches.
**Fix:** put something unique per upload into the key (e.g. `v<version>-<updated_at>`), or drop `immutable` and use
a shorter max-age; or record this as an accepted risk.

## F6: test with the same URL form the server will emit
**Type:** non-blocking · **Category:** design · **Severity:** LOW · **Location:** Steps 1 and 3.

`NaverStorage` and `public_origin()` use virtual-host URLs (`https://<bucket>.kr.object.ncloudstorage.com`), but
Steps 1 and 3 don't say which form `bucket.py` and the test page use. TLS certificate coverage, CORS and the school
speed can differ by host. **Fix:** state that the test page and Step 3's loads use the virtual-host origin exactly as
`public_origin()` will build it.

## F7: `cdp_start.py` is not in the repo
**Type:** non-blocking · **Category:** accuracy · **Severity:** LOW · **Location:** Step 1 "headed Chrome
(`cdp_start.py` on the test page)".

`find`/`grep` over the repo (excluding `Library/`) finds the name only in this plan. **Fix:** name where it lives (or
the command used), so the step can be repeated.

## F8: record publish time against the upload request's limits
**Type:** non-blocking · **Category:** design · **Severity:** LOW · **Location:** Steps 1, 4, 5.

Publishing runs inside the upload request, after the student's zip has already crossed the slow tunnel, and
Cloudflare drops a proxied request at about 100 s; the plan's per-object timeout is a flat 30 s. With ~10 MB builds
this is probably fine, but nothing measures it. **Fix:** time `put-test`'s four puts in Step 1 and record it; note
that a dropped request mid-publish is safe (reconcile repairs it) so a test covers "publish future dropped → no
`published_version`, local URLs".

---

## Reviewer's view

I would build this after F1. The approach is sound and the gates come before any code, but F1 means the fallback
the plan leans on is missing for one of the four files. F2 and F3 are small ordering and condition fixes that keep
reconcile and the off switch honest; F4 to F8 are clarifications or accepted-risk notes.

## Review round 2

# Review round 2: gallery-naver-storage (delta)

**What I could run:** `device_bash` over the Mac mini folder. I read `log.md`'s round 1 dispositions and the plan
above `## Review round 1`, and checked the new pieces against `db.rs`, the spike's `dev.loader.js` and the round 1
findings. Nothing executed (no cargo, Chrome or Naver).

## Round 1 findings

- **F1 (blocking): closed.** `player.js` now passes `showBanner` and treats `type == 'error'`, a rejected promise and
  a loader-script error as failures; Step 5's Chrome check breaks each of the four files in turn, with the 6000.0
  loader repeated. I also checked the seam this opens: could a `showBanner(…, 'error')` fire **after** the game has
  started and reload a running game? In the loader, `showBanner` errors come only from `downloadBinary` and
  `downloadFramework` (startup); a runtime error goes through `errorHandler` → `alert` (`dev.loader.js:378-407`), not
  `showBanner`. So no. (A one-line guard so the failure handler does nothing after `started()` would still be cheap.)
  The Author's re-check in the 6000.6 `game.loader.js` is accepted on their report.
- **F2: closed.** List first, lock and re-read per game, deletes under the same lock, version-conditional
  `clear_published`, and the "upload between listing and turn" test. A new game uploaded during reconcile either is
  absent from the listing or is busy (its id is locked from before `insert_game`, `upload.rs:145`), so it is safe too.
- **F3: closed.** Condition is now storage **and** `published_version == version` **and** no `?local=1`, with the
  test.
- **F4: closed.** Log and run without storage; `storage check` before the Step 8 restart.
- **F5: closed.** Keys `games/<id>/v<version>-<updated_at>/`. Seam checked: `updated_at` is written only by
  `insert_game` (`db.rs:379`) and `set_version` (`db.rs:455`), so no title or nickname edit can move a game's prefix
  away from its published keys. One prefix function shared by page and reconcile, as needed. The upload's delete of
  "the previous upload's prefix" must use the pre-update row (`Target::Update(game)`), which the request already holds.
- **F6, F7, F8: closed.** Virtual-host origin in Steps 1 and 3; `Tools/gallery_load.py` in the manifest; put times
  recorded, dropped-publish test and note.

No new findings. Late-pushback test: nothing I would add beyond the optional guard noted under F1.

## Reviewer's view

I would build this as written. F1's fix makes the fallback real for all four files, and F2/F5's key and ordering
changes hold up against the code paths I could trace. What remains unproven is outside the document: Naver serving
`Content-Encoding: br` (Step 1) and the school load time (Step 2), both gated before any code.
