---
slug: gallery-naver-storage
round: 2
verdict: approved
reviewer: claude-cowork
updated: 2026-11-04T01:22:00+09:00
blocking: 0
recommendations: 0
---

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
