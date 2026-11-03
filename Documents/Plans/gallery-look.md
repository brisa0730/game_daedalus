# Plan: Gallery Look

**Date:** 2026-11-03 | **Status:** draft — handshake-approved (2 rounds), awaiting operator approval

## Overview

**Where things stand:** The class gallery works, but it looks like an admin page: beige, plain lists, default buttons. Students open it to show each other games they made, and nothing about it says "games".

**What this plan changes:**
- The whole gallery gets a dark night-sky look, a strip of grass-and-dirt tiles under the header, our player block as the logo and a pixel font for headings.
- Game cards get bigger pictures, a lift when pointed at, and a NEW badge for games added in the last week. The student's own cards say when they were last uploaded.
- The play page becomes a dark "theater" with the game kept at its true shape.
- The upload page becomes three numbered steps with a big area to drop the zip into.
- The sign-in page gets a small pixel scene; the teacher's pages get the same colors and stay plain.

**How you'll know it worked:** The live gallery looks like the approved mockup on a laptop and on a phone-width window, and everything that worked before (sign-in, upload, play, remove, teacher tools, printing slips) still works.

**Not in this plan:** new gallery features (search, likes, comments, sorting) and the real art pack.

**Why a plan and not a patch:** it touches the stylesheet, script, five page files, the data layer, a new font and the tests.

**Design choice:** one self-hosted font (a Korean pixel font, for headings and the logo only) and the computer's own Korean font for everything else. The mockup used three web fonts; the gallery only loads its own files, and a pixel font is tiring for Korean body text.

---

## Source
- **Trigger:** operator: "the web is very crude"; the pixel-arcade mockup was approved 2026-11-03 (https://claude.ai/artifact/MwbiVm2XwYXRom27wmQSwL).
- **Related issues:** none (ISS-002, the black-frame stall, is untouched).
- **Scope:** the class gallery's pages, stylesheet, script and one font; the games host's player page background.
- **Depends on:** v0.18 (change-password page in the shell).

## File Manifest

| Action | File | Purpose |
|--------|------|---------|
| NEW | `Deploy/gallery/static/fonts/Galmuri11-<release>.woff2` (or `…-ksx1001.woff2` if subset) | The heading/logo font (Galmuri, SIL OFL 1.1): the release file unchanged, or a subset derived from it (Step 1); the release in the name |
| NEW (only if subset) | `Deploy/gallery/static/fonts/SUBSET.txt` | The exact `pyftsubset` command that made the derived font |
| NEW | `Deploy/gallery/static/fonts/OFL.txt` | Its license, beside it |
| MODIFY | `Deploy/gallery/static/gallery.css` | Rewritten: tokens, header + ground strip, cards, theater, steps, drop zone, teacher tables, slips (print: white background, black text) |
| MODIFY | `Deploy/gallery/static/gallery.js` | Show the picked zip's name in the drop zone; percent next to the progress bar; fullscreen the frame box (letterboxed 16:9), not the iframe |
| MODIFY | `Deploy/gallery/src/pages.rs` | Shell: logo (player block SVG + name), active nav item, ground strip, `color-scheme` meta; font handler (bytes) |
| MODIFY | `Deploy/gallery/src/app.rs` | One public route for the font |
| MODIFY | `Deploy/gallery/src/gallery.rs` | Cards (NEW badge, "N일 전에 올림" + 삭제 on own cards, add-game card), game count line, theater play view |
| MODIFY | `Deploy/gallery/src/upload.rs` | Upload page as three numbered steps with a drop zone (same form ids and fields) |
| MODIFY | `Deploy/gallery/src/session.rs` | Sign-in page with the pixel scene; password page classes |
| MODIFY | `Deploy/gallery/src/teacher.rs` | Class names only where the new stylesheet needs a hook |
| MODIFY | `Deploy/gallery/src/db.rs` | `Game` carries `created_at` and `updated_at` (already in the table) |
| MODIFY | `Deploy/gallery/src/strings.rs` | New Korean strings (login hint, game count, upload steps, uploaded-ago, NEW) |
| MODIFY | `Deploy/gallery/src/play.rs` | Games host player page background matches the theater |
| NEW | `Deploy/gallery/tests/look.rs` | Renders every gallery page with fixture data into the repo's `TestResults/gallery-look/`; guards no inline styles, the script's ids, badge and uploaded-ago rules |
| MODIFY | `Deploy/gallery/tests/routing.rs` | The font route: public, `font/woff2`, real woff2 bytes, long-lived cache, `nosniff`, CSS url matches the route |
| MODIFY | `Deploy/gallery/tests/games.rs` | The owner-name assertion checks the card, not the header (own cards drop the name) |
| MODIFY | `Deploy/gallery/tests/play.rs` | Player page background color |
| MODIFY | `.gitattributes` | `*.woff2 lfs` (fonts are LFS-tracked; woff2 was missing) |
| MODIFY | `.claude/CLAUDE.md` | Galmuri in the read-only vendored list (or, if subset, listed as generated from the release); the gallery testing row adds the look check |

---

## Steps

### Step 1: Font
- **NEW `static/fonts/<font file>` + `OFL.txt`**: download from the Galmuri GitHub release at `github.com/quiple/galmuri` (latest tag at drafting: v2.40.4); the release goes in the file name, and the repository and release both go in the CLAUDE.md entry. At or under 400 KB the file is shipped unchanged. Over 400 KB: read `OFL.txt` for a Reserved Font Name first; if there is one, ship it whole and note the size. If there is none, subset to KS X 1001 Hangul + ASCII with fonttools (a missing syllable falls back to the system font): the file is then **derived**, named `Galmuri11-<release>-ksx1001.woff2`, the exact `pyftsubset` command is written in a `SUBSET.txt` beside `OFL.txt` and in the Changelog, and CLAUDE.md lists it as generated from the release, not as vendored.
- **MODIFY `.gitattributes`**: `*.woff2 lfs`.
- **MODIFY `src/pages.rs`, `src/app.rs`**: one constant for the served path `/static/fonts/<the font's file name>` (whichever Step 1 shipped; Public route); its own handler over `include_bytes!` (`static_file` takes text), `font/woff2`, `Cache-Control: public, max-age=31536000, immutable` (a new font is a new path). `@font-face` in `gallery.css` with a **relative** url `fonts/<the font's file name>` and `font-display: swap`. Tests: the bytes start with `wOF2` (a clone without LFS would embed a pointer and silently fall back); the CSS url names the route constant.
- **Verify:** `cargo test routing`; `ls -l` size noted.

### Step 2: Shell and stylesheet
- **MODIFY `gallery.css`**: rewrite on CSS custom properties from the mockup (ground `#141a2e`, surface `#1e2742`, line `#34406b`, text `#eef1fa`, muted `#a3acc8`, accent `#ffd23f`, grass `#4cc26a`, danger `#ff8f8f` on dark). Body font stack `"Apple SD Gothic Neo", "Malgun Gothic", "Noto Sans KR", system-ui, sans-serif`; headings and the logo in Galmuri. Ground strip from CSS gradients only (no images). Every page still works at 390 px wide (header wraps, cards one column). `@media print`: white background **and black text** on everything that prints (slips, headings, `dd.big`), dashed slip borders, header and strip hidden; browsers drop backgrounds when printing, so light text would vanish. Focus outlines visible (yellow) on links, buttons and inputs; the drop zone shows it with `:focus-within`. Text contrast ≥ 4.5:1; input borders ≥ 3:1 against their surface (`#6c7bb0`, not the card line color). Card lift off under `prefers-reduced-motion`. The stylesheet and script stay `no-cache` as today (no `?v=`).
- **MODIFY `pages.rs` shell**: `<meta name="color-scheme" content="dark">`; logo = inline SVG player block (presentation attributes only) + the site name from `strings.rs`; nav marks the current page with `aria-current="page"`: `page()` and `shell()` take an `Option<Nav>` (`Games`, `Upload`, `Teacher`), `message()`/`not_found()`/`forbidden()` pass `None`; the shared-laptop note stays visible as a slim line. Keep every id the script and tests use. All new user-facing text lives in `strings.rs`.
- **CSP rule:** the gallery's policy is `style-src 'self'`, so no `style="…"` attribute and no `<style>` element anywhere in gallery HTML; SVG `fill`/`width` attributes are fine. Step 7's guard enforces it.
- **Verify:** `cargo test` (shell test still passes).

### Step 3: Game list and play page
- **MODIFY `db.rs`**: `Game` gains `created_at` and `updated_at` (`GAME_COLUMNS` selects them); no migration.
- **MODIFY `gallery.rs`**:
    - Card: picture (`width`/`height` attributes stay 320 × 180; CSS scales), title, owner. **NEW** badge when `created_at` is within 7 × 24 h of now (an update doesn't make it new). On "내 게임" cards: "오늘 올림 / 어제 올림 / N일 전에 올림" from `updated_at`, counted in Korean calendar days (`(t + 9 h) / 86400`, no date crate), and 삭제 as a small button-styled link (owner name dropped there; it's theirs). In "모든 게임" every card is the same as today: owner name, and 삭제 for the owner and the teacher (`can_remove` unchanged).
    - "내 게임": cards plus a dashed "게임 올리기" card linking to `/upload` (replaces the empty-state sentence's link; empty state keeps `MY_GAMES_EMPTY` text in that card). At the limit (`mine.len() >= games_per_account`) the card is replaced by the `too_many_games` line, so nobody is invited to send a zip that will be refused. "모든 게임" heading gets "게임 N개".
    - Play view: theater class on `main`, back link, title + "<name> 만듦", 전체 화면 button with a pixel icon; the keyboard note sits in the title row (wrapping under it when narrow), so nothing the player needs is below the frame. Same iframe attributes and ids. **The frame stays exactly 16:9 from both limits**: `width: min(100%, calc((100dvh - 220px) * 16 / 9)); aspect-ratio: 16 / 9; margin-inline: auto` on the frame box, with the iframe filling it (`width/height: 100%`). A width of 100% plus a max-height would keep the width and cut the height (a 2.1:1 box on a 1366 × 768 laptop), and `CameraFollow` widens the view with the aspect, so players would see more of a troll level than the student designed. The 220 px is the budget above the frame plus a margin below it at desktop width (estimated ≈ 199 px: header 58, strip 16, note 31, padding 20, title row 50, gap 16, border 8); Step 6 measures it and the constant is tuned to the measurement. At narrow widths the width limit wins.
    - **Full screen keeps 16:9 too.** Today `gallery.js` fullscreens the iframe, which then takes the monitor's shape (on a 16:10 screen `CameraFollow` shows about 10% less level sideways). The 전체 화면 button now fullscreens the frame box: `.frame:fullscreen { display: grid; place-items: center; background: #000 }` with the iframe at `aspect-ratio: 16 / 9; width: min(100vw, calc(100vh * 16 / 9))`, letterboxed. `#fullscreen` and the iframe's focus call stay; the box gets an id for the script.
- **MODIFY `strings.rs`**: `NEW_BADGE`, `uploaded_ago(days)`, `game_count(n)`, `made_by(name)`, the logo text, the login hint, the three upload step titles and the step-1 line.
- **MODIFY `play.rs`**: player page background `#0b0f1c` (its own inline style is under the games host's policy, unchanged otherwise); `tests/play.rs` asserts it.
- **Verify:** unit tests for `uploaded_ago` (0, 1, 2, 30 days) and the 7-day badge edge.

### Step 4: Upload page
- **MODIFY `upload.rs`**: three numbered steps: 1 "Unity에서 zip 만들기" (Daedalus → Share my game), 2 the new/update choice as bordered rows (update rows show the game's small picture as now), 3 the drop zone: the existing `#zip` file input stretched invisibly over a dashed box, so clicking or dropping a file uses the browser's own input (no drag-and-drop code). Same form id, field names, data attributes and `#progress`, `#upload-message`, `#upload-button`. The Korean labels the editor quotes (업로드, 새 게임, 내 게임 업데이트하기) are unchanged; `Tools/share_check.py` cross-checks them.
- **MODIFY `gallery.js`**: on `change`, put the file name in a dedicated span in the zone (not the label, which holds the input); during upload, the percent goes in an `aria-hidden` span beside the bar (the `role="status"` message keeps the one "올리는 중…" line, so a screen reader doesn't read every percent); the checked choice row is styled in CSS with `label:has(input:checked)` (no script state). No new network behavior.
- **Verify:** `cargo test upload` (no inline script/handlers test still passes).

### Step 5: Sign-in, password and teacher pages
- **MODIFY `session.rs`**: sign-in page: big logo, one-line welcome, the form on a card, decorative pixel scene (stars, player, spikes, flag on a ground strip) as `aria-hidden` SVG. New hint "아이디와 비밀번호는 선생님께 받아요." Button label stays 로그인. Password page gets the card form style.
- **MODIFY `teacher.rs`**: only class hooks if needed; tables, preview errors (`tr.bad`), slips and confirm pages restyled through CSS.
- **Verify:** `cargo test sign_in password teacher`.

### Step 6: Look check
- **NEW `tests/look.rs`**: one test builds a `TestApp` with a teacher, two students and four games (one backdated by opening the test's database file with sqlx and setting `created_at`/`updated_at`; `Db`'s pool stays private), fetches every gallery page as each role (login, list empty and full, play, upload with and without games, password, teacher, preview with an error, slips, remove confirms, 404) and writes each HTML to `<repo>/TestResults/gallery-look/<name>.html` (from `CARGO_MANIFEST_DIR`; gitignored) with `/static/` and `/shots/` rewritten to relative files copied beside them (CSS, font under `static/fonts/`, screenshots) and the play frame's `src` set to `about:blank` (no request to the live games host). Asserts for every page: no ` style=` attribute and no `<style` element; the ids the script needs on their pages (`#game`, `#fullscreen`, `#print`, `#upload` with its `data-*` messages, `#zip`, `#progress`, `#upload-message`, `#upload-button`); the NEW badge only on the new games; uploaded-ago text on own cards only.
- Serve `TestResults/gallery-look/` over localhost (`python -m http.server` from the scratchpad, pointed at that folder), not `file://`: Chrome blocks `@font-face` from a `file://` page (opaque origin) and would silently render the fallback font. Screenshot each page with headless Chrome at 1280 × 900 and 390 × 844, the play page also at **1366 × 768** (`--headless=new --screenshot`), and print the slips page to PDF (`--print-to-pdf`), into `TestResults/gallery-look/`; Claude looks at every one against the mockup. A CDP script in the scratchpad measures what a screenshot can't: on every page at 390 px `scrollWidth <= innerWidth`; on every page `document.fonts.check('16px Galmuri11')` is true after `document.fonts.ready`; on the play page at 1366 × 768 and 1280 × 900 the iframe's width ÷ height is within 1% of 16/9 and its bottom is inside the window.
- **Verify:** pngs and pdf present and inspected; the CDP checks pass on every page.

### Step 7: Run Tests
- `cargo test` and `cargo clippy --all-targets -- -D warnings` in `Deploy/gallery/`.
- `python Tools/share_check.py --editor 6000.6` (it runs the gallery's zip check and label cross-check).

### Step 8: Deploy and live check
- With the operator's OK at that moment: `cargo build --release`, install the binary, `launchctl kickstart -k`. Open the live gallery in Chrome (signed in as a test student and the teacher): list, play a game, press 전체 화면 (the game is letterboxed at 16:9 with black bars on a non-16:9 screen and keeps keyboard focus), upload an update, remove confirm, print preview of slips. Look at the screenshots.
- **Verify:** every page loads with no CSP errors in the console (Cloudflare's beacon excepted).

### Step 9: Verify
- Walk the Completion Criteria.

### Step 10 (Final): Code Review
- Run the code-review agent on all changed files; fix HIGH/MED.

---

## Completion Criteria

- [ ] `cargo test` and `cargo clippy --all-targets -- -D warnings` pass in `Deploy/gallery/`
- [ ] `tests/look.rs` passes: no inline style anywhere in gallery HTML; the script's ids present; badge and uploaded-ago rules hold
- [ ] `TestResults/gallery-look/` has a desktop and a phone screenshot of every page (and the play page at 1366 × 768), served over localhost and inspected against the mockup; the CDP checks pass (no overflow at 390 px, Galmuri loaded on every page, play frame 16:9 within 1% and inside the window); the slips PDF shows black text on white
- [ ] Font route answers real woff2 bytes as `font/woff2` with an immutable cache; the CSS url matches it; `OFL.txt` sits beside the font; `git check-attr filter` on the font says `lfs`
- [ ] `python Tools/share_check.py --editor 6000.6` passes (editor labels still match)
- [ ] Live gallery redeployed; Chrome shows the new look, no CSP console errors from our pages; upload, play, remove and slips still work
- [ ] Operator look-check: the live gallery on a school laptop (or similar) reads well and feels right

---

## Notes to Spec / Open Risks

- **Mockup deviations (deliberate):** one font instead of three (Galmuri for headings/logo; system Korean font for text); the play page keeps the existing keyboard note instead of drawn key caps (keys depend on the student's game); the sign-in button stays 로그인.
- **GDD:** systems.md § Share "Play" bullet: cards add a NEW badge (first 7 days) and, on your own cards, when you last uploaded; platforms.md: the font and its license. Glossary: none.
- **Risk:** Galmuri's size on school Wi-Fi: once per browser thanks to the immutable cache; capped by Step 1's 400 KB rule.
- **Risk:** Malgun Gothic / Apple SD Gothic Neo absent on a Chromebook: falls back to Noto Sans CJK; checked at the operator look-check.

---

**Critic (2026-11-03), folded in:** MED: `?v=` dropped (broke `routing.rs` no-cache test, not needed); print sets black text + PDF check; CSS font url relative so the look check renders the real font; play frame blanked in snapshots; overflow measured by script; script ids asserted; woff2 bytes checked (LFS pointer guard). LOW: filename span, aria-hidden percent, `:focus-within`, backdating via sqlx on the test DB, KST day counting, all text in `strings.rs`, own cards in 모든 게임 unchanged, `Option<Nav>` through `page()`, `games.rs` owner assertion, output folder named, RFN check before subsetting, input border contrast, `color-scheme` + reduced motion, `play.rs` color test.

---

## Post-Plan Cleanup
After code review passes:
1. Append execution trailers to this plan file:
   - `**Approval:** User confirmed YYYY-MM-DD.`
   - `**Execution:** COMPLETE YYYY-MM-DD.`
   - `**Verification:** <one-line summary of what passed>`
2. Move this plan to `Documents/Plans/archive/`.
3. Changelog v0.19, GDD version bump, systems.md / platforms.md notes above.
4. Deferred items, if any, to `Documents/Operations/Deferred.md`.
5. Commit all plan work.

## Workflow
Execute steps sequentially. Proceed to the next step without waiting for user approval. Only stop if blocked. If blocked, stop and report: what step, what was attempted, what failed.

---

## Review round 1

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

## Review round 2

---
slug: gallery-look
round: 2
verdict: approved
reviewer: claude-cowork
updated: 2026-11-03T16:55:00+09:00
blocking: 0
recommendations: 0
---

# Review round 2: gallery-look (delta)

**What I could run.** I used the shell over the Mac mini folder to read `log.md`'s round 1 dispositions and the changed parts of the plan (File Manifest, Steps 1, 3, 4 and 6, Completion Criteria). This was a delta review against round 1, not a full re-read. I recomputed the F1 numbers. I ran no build, tests or Chrome.

## Round 1 findings

- **F1 (blocking) is closed.** Step 3 now sizes the frame from both limits: `width: min(100%, calc((100dvh - 220px) * 16 / 9)); aspect-ratio: 16/9`. I recomputed the budget: 58 + 16 + 31 + 20 + 50 + 16 + 8 = 199 px, so 220 px leaves a margin. On a 1366 × 768 laptop (viewport about 650 px tall) the frame comes out about 764 × 430, which is 16:9 and fits the window. At 390 px wide the width limit wins (358 × 201). Moving the keyboard note into the title row removes the only thing that sat below the frame. Step 6 adds the 1366 × 768 shot and a measured ratio and in-window check, and the Completion Criteria match.
- **F2 (blocking) is closed.** The snapshots are now served over localhost, and `document.fonts.check` runs after `document.fonts.ready` on every page. A font that fails to load now fails the check instead of passing silently.
- **F3 is closed.** The Author confirmed `github.com/quiple/galmuri` with `git ls-remote` (tags up to v2.40.4), which corrects my search-only evidence.
- **F4 is partly folded (still non-blocking, see below).**
- **F5 and F6 are closed.** At the game limit, the `too_many_games` line replaces the add-game card. The checked row is styled with `label:has(input:checked)`, and the manifest's `gallery.js` row has dropped the toggle.

## Findings

### F4 · non-blocking · accuracy · File Manifest, Step 1 (residual)
**The subset branch is folded into Step 1, but three spots still describe only the unchanged file.**
- The manifest row still says "`Galmuri11-<release>.woff2` … vendored unchanged".
- The manifest's CLAUDE.md row still says "Galmuri in the read-only vendored list".
- Step 1 still says "Unchanged file." just before the branch.

Two other gaps:
- `SUBSET.txt` has no manifest row.
- The route constant and the CSS url in Step 1 hard-code `Galmuri11-<release>.woff2`. Phrase them as "the font's file name" so the `-ksx1001` name carries through.

Also, "the repository and release go in the file name" should read "the release in the file name, both in the CLAUDE.md entry". Fold these on close.

### F7 · non-blocking · design · Step 3 play view (fullscreen)
**Full screen still shows the game at the screen's shape, not 16:9.** This is new this round and it was not caused by the fold. It is the same mechanism as F1, so I'm filing it under the late-pushback test. `gallery.js` calls `game.requestFullscreen()` on the iframe, so in full screen the iframe takes the monitor's shape and the frame box's 16:9 rule doesn't apply. On a 16:10 screen (most Macs, some school laptops) `CameraFollow` then shows about 10% less level sideways. This is today's behavior, not a regression. Either:
- fullscreen the frame box instead, with `:fullscreen { display:grid; place-items:center; background:#000 }` and the iframe kept at `aspect-ratio: 16/9; width: min(100vw, 100vh * 16 / 9)`, which letterboxes it; or
- say in the plan that full screen follows the screen's shape.

Keep `#fullscreen` and the iframe's focus call either way.

## Reviewer's view
I would build this as written. Both blocking findings (F1 and F2) are fixed with measured checks, and the numbers behind the 220 px budget hold. F4's leftover wording and F7's fullscreen shape are small folds for close and don't block. Approval rests on the Author's Step 6 measurements and the operator look-check, which nobody has run yet.
