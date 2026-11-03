# Handshake log: gallery-naver-storage

## Opened 2026-11-04T01:06:17+09:00 (Author)
Doc: `Documents/Plans/gallery-naver-storage.md`. Proposes: publish each class-gallery game's four build files to a public Naver Object Storage bucket (Korea) so players avoid Cloudflare's LAX detour (ISS-002); the Mac keeps the originals; the player falls back to local files once on a failed load; an in-server reconcile at every start repairs the bucket (also migrates existing games). Gated by a school speed test and a Content-Encoding check before any code. Critic-reviewed (16 findings folded in; see the plan's trailer). Nothing is built yet.

## Round 1 — changes-requested (2026-11-04T01:14:06+09:00, Author dispositions)
1 blocking, 7 non-blocking, 0 recommendations. All accepted; each re-verified:
- **F1 accepted (blocking).** Re-verified in the **6000.6** Share build's `game.loader.js` (today's trap-test zip): the framework `<script>` `onerror` and the missing-`unityFramework` path call only `l(…,"error")`, which without `config.showBanner` is `console.error`; the start promise never settles. Step 5 now passes `showBanner` and treats `type=='error'`, a rejected promise and a loader-script error as failures; the Chrome check forces each of the four files to 404 in turn; the framework case is repeated with a **6000.0** build (none on disk: `TestResults/share-check/` holds only `6000.6.zip`, so the step makes one with `share_check.py --editor 6000.0`). Completion Criteria updated.
- **F2 accepted.** Reconcile now lists first, then per game takes the busy lock and re-reads the row; deletes of that game's non-current prefixes happen under the same lock; `clear_published` is version-conditional (Step 6 and the File Manifest db row). Test added: an upload between the listing and that game's turn keeps its prefix and `published_version`.
- **F3 accepted.** Bucket URLs need storage configured **and** published **and** no `?local=1`; test added: no storage + a published row → local URLs and today's CSP.
- **F4 accepted** (the first remedy). A bad `storage.json` logs an error (never the secret) and the gallery runs without storage; reason recorded (launchd restart loop, same as the sweep's log-and-serve choice at `main.rs`); `storage check` must pass before the Step 8 restart; Completion Criteria row added.
- **F5 accepted** (the first remedy). Re-verified `upload.rs:256` computes the next version from the row, so a restored row repeats a version. Keys are now `games/<id>/v<version>-<updated_at>/Build/<file>`, one prefix function shared by the page and reconcile.
- **F6 accepted.** Steps 1 and 3 use the virtual-host origin exactly as `public_origin()` builds it.
- **F7 accepted.** Confirmed `cdp_start.py` lives only in the session scratchpad; it is added to the repo as `Tools/gallery_load.py` (File Manifest) and named in Step 1.
- **F8 accepted.** `put-test` times the four puts (Step 1, Completion Criteria); a dropped publish future is in Step 5's tests; Notes record that a mid-publish drop is safe.
Post-fold check: grep finds no `v<version>/Build`, `cdp_start`, "refuse to start" or "published and no" left; `showBanner` appears in the manifest and Step 5.

## Round 2 — approved (2026-11-04T01:17:51+09:00, Author close fold)
Verdict `approved`, 0 blocking, 0 non-blocking, 0 recommendations. F1–F8 closed by the Reviewer. Two optional notes folded on close: (1) the `started` guard in `player.js` so a late `showBanner` error can't reload a running game (Step 5); (2) the upload's old-prefix delete is built from the pre-update row (`Target::Update(game)`) (Step 5). Post-fold check: each phrase appears once, at Step 5; the Status line reads approved.

## Closed 2026-11-04T01:17:51+09:00
Plan approved. Nothing was built: Steps 1–2 (Naver setup, school gate) come first; the operator has created the sub-account (Object Storage only, console access dropped) and is writing `storage.json`.

## Post-approval amendment (2026-11-04, Author; operator decision)
Step 2's gate changed from "school within 15 s" to "a phone on mobile data within 15 s" because the operator can't reach school for a week; the school load stays as the final Completion Criteria item. Results: Step 1 (Mac, office KT line): `content-encoding: br` and `application/wasm` served on the virtual-host origin, `Range` → 206, puts 0.04–0.38 s each, Unity started in 0.6 s and 0.5 s. Step 2 (operator's phone, mobile data): ~2 s. Gate met.
