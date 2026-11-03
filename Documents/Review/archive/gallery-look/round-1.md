---
slug: gallery-look
round: 1
verdict: changes-requested
reviewer: claude-cowork
updated: 2026-11-03T16:58:00+09:00
blocking: 2
recommendations: 0
---

# Review round 1: gallery-look

**What I could run.** Shell over the Mac mini folder (`device_bash`): read `Documents/Plans/gallery-look.md`, the gallery source (`src/pages.rs`, `app.rs`, `guard.rs`, `gallery.rs`, `upload.rs`, `play.rs`, `db.rs`, `config.rs`), `static/gallery.{css,js}`, `static/player.js`, `tests/routing.rs`, `tests/games.rs`, `tests/support/mod.rs`, `.gitattributes`, `.gitignore`, the runtime's `CameraFollow.cs`, and the mockup's `Play.dc.html`. Recomputed the palette's contrast ratios. I did not run `cargo test`, Chrome, or anything else, and could not reach GitHub or a CDN from either shell, so the Galmuri release, its size and its license text are unverified.

**Verified true.** CSP is `style-src 'self'` with no `font-src`, so a same-origin font falls back to `default-src 'self'` and loads (guard.rs:22). `gallery_layer` keeps a handler's own `Cache-Control` (guard.rs:46), so the immutable font header survives and `routing.rs:126`'s no-cache test on the CSS is untouched. `static_file` takes `&'static str` (pages.rs:84), so the font needs its own handler, as Step 1 says. `.gitattributes` tracks `*.otf`/`*.ttf` but not `*.woff2`. `games.created_at`/`updated_at` exist (db.rs:356) and `update` only bumps `updated_at` (db.rs:432), so "an update doesn't make it new" holds. `TestApp` keeps its database in a `TempDir` (tests/support/mod.rs:24, 90), so backdating through sqlx on that file is feasible. `routing.rs:69` only asserts a Public route isn't 401, so a new Public font route passes it. Contrast: muted `#a3acc8` on surface `#1e2742` 6.53:1, accent on surface 10.21:1, danger `#ff8f8f` on surface 6.73:1, input border `#6c7bb0` on surface 3.58:1 (≥ 3), the card line `#34406b` 1.47:1 (correctly not used for inputs).

## Findings

### F1 · blocking · design · Step 3 play view, Step 6 viewports
**The 16:9 frame stops being 16:9 on a school laptop, and neither screenshot size shows it.** Step 3 sizes the frame with `aspect-ratio` plus `max-height: calc(100vh - header)`. With the width fixed (`width: 100%` in the mockup, as in the current `#game`), CSS does not carry a max-height clamp back through the ratio, so a short window keeps the full width and cuts the height. On a 1366 × 768 laptop, Chrome's viewport is about 1366 × 650. The frame is about 1052 px wide (main 1100 − 2 × 24 padding), which needs 592 px of height. The header, ground strip, title row and border take about 150 px, so the clamp gives about 500 px, making a 2.1:1 box. `CameraFollow.cs:38-39` keeps `orthographicSize` and widens `halfWidth = halfHeight * camera.aspect`, so the player sees about 25% more level sideways than the student designed for. That matters most for troll traps. `calc(100vh - header)` also needs a number, but the header wraps and has no fixed height. The look check would miss all of this. At 1280 × 900 the frame fits (≈ 1052 × 592 under ≈ 750 px), and at 390 × 844 it is width-limited. **Fix:** size the frame from both limits, e.g. `width: min(100%, calc((100dvh - <fixed header+bar budget>) * 16 / 9)); aspect-ratio: 16 / 9; margin-inline: auto`, or make the theater a column flex and give the frame `height:auto; max-width:100%; aspect-ratio` with the height as the definite side. Add a **1366 × 768** screenshot of the play page to Step 6, with a measured check (CDP: the iframe's `width / height` within 1% of 16/9).

### F2 · blocking · accuracy · Step 6
**The look check probably renders the fallback font, silently.** Step 6 writes the pages to `TestResults/gallery-look/` and screenshots them with headless Chrome. That loads them as `file://`. Chrome fetches `@font-face` in CORS mode, and `file://` pages have an opaque (`null`) origin, so a font from a sibling `file://` path is blocked ("from origin 'null' has been blocked by CORS policy"). The page then renders with `font-display: swap`'s fallback, with no error the screenshot shows. The critic folded "CSS font url relative so the look check renders the real font" precisely to see Galmuri, and this defeats it. I could not run Chrome, so **Author: please reproduce** (open one snapshot, check the console or `document.fonts.check('16px Galmuri11')`). **Fix:** serve the folder over localhost for the screenshots (e.g. `python -m http.server` in the scratchpad, as `perf_check.py` already serves a build), or pass `--allow-file-access-from-files`. Either way, make the CDP overflow script also assert `document.fonts.check(...)` on every page, so a font that didn't load fails the check instead of passing it.

### F3 · non-blocking · accuracy · Step 1
**The font's source repository looks misnamed.** Step 1 says "the Galmuri GitHub release (quiet-/galmuri)". A GitHub repository search returns `quiple/galmuri` (Galmuri's author is quiple). I found no `quiet-/galmuri` in the search, and I could not open either repository from here. Name the repository that you actually download from, and record it with the release in the CLAUDE.md vendored entry.

### F4 · non-blocking · accuracy · Step 1, File Manifest
**"Vendored unchanged" contradicts the subset branch.** The manifest says the font is "vendored unchanged; the release in the name", and CLAUDE.md lists it as read-only vendored code. But Step 1's no-RFN branch subsets it with fonttools if it is over 400 KB. A subset is a derived file: its name `Galmuri11-<release>.woff2` would then describe a file the release never shipped, and the read-only listing would forbid regenerating it. If you subset, name it as derived (e.g. `Galmuri11-<release>-ksx1001.woff2`), keep the exact `pyftsubset` command in the Changelog or beside `OFL.txt`, and list the derived file in CLAUDE.md as generated rather than vendored.

### F5 · non-blocking · design · Step 3 "내 게임" add-game card
**The add-game card still shows at the 10-game limit.** `games_per_account` is 10 (config.rs:78), and the upload only refuses once the zip has been sent (`too_many_games`, upload.rs). With the card always in "내 게임", a student with 10 games is invited to pick and upload a zip that will be refused. Either hide the card (or swap it for the limit message, `too_many_games`) when `mine.len() >= games_per_account`, or accept it and say so in the plan.

### F6 · non-blocking · design · Step 4 `gallery.js`
**The `picked` class can be pure CSS.** Marking the checked choice row with JS adds a listener and state to the gallery's only script. `label:has(input:checked)` does the same with no script and can't drift from the real radio state. It works in Chrome 105 and later, which every supported desktop Chrome has. Simplicity First suggests the CSS. The file-name span and the percent still need the script.

## Reviewer's view
I would build this once F1 and F2 are fixed. F1 is the play page's main promise ("the game kept at its true shape"), which fails on the most common school-laptop screen and changes how much of a troll level the player sees. F2 is the check meant to prove the new font, which would quietly show the old one. Both fixes are small. F3–F6 are cleanups to fold in. Everything else I checked against the code holds, including the CSP, cache, LFS, data-layer and test-harness claims.
