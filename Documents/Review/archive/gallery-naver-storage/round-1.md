---
slug: gallery-naver-storage
round: 1
verdict: changes-requested
reviewer: claude-cowork
updated: 2026-11-04T01:20:00+09:00
blocking: 1
recommendations: 0
---

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
