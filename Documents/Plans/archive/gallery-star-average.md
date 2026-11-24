# Plan: Gallery Star Average

**Date:** 2026-11-24 | **Status:** complete

## Overview

**Where things stand:** A rated game shows its average as text, a star sign followed by a number and a count. Students read stars faster than numbers, and the page already shows five stars for rating, so the text form looks out of place.

**What this plan changes:**
- The average shows as five small pixel stars, gold up to the average and grey after it, then the count of raters.
- The number itself leaves the screen but stays in the hover text and in what a screen reader says.
- On the play page the average stars are smaller than the rating stars and don't react to the pointer, so nobody tries to click them.
- After a vote, the average stars redraw at once, without reloading the page.

**How you'll know it worked:** Game cards and the play page show a row of stars filled to the average (a game at 4.3 shows four gold stars and a sliver of the fifth; one at 4.7 shows most of the fifth), with the count after it, at laptop and phone widths.

**Not in this plan:** deploying to the live gallery, and sorting or filtering by rating.

**Why a plan and not a patch:** it changes the server's page parts, the stylesheet, the script, three test files and the design docs.

**Design choice:** the gold part follows the average itself, not rounded to half stars, so 4.3 and 4.7 still look different with the number hidden (operator, 2026-11-24). The server draws the stars and the script swaps in what the server sends back, the same way new comments arrive, so the stars are drawn in one place only.

---

## Source
- **Trigger:** operator request, 2026-11-24 (the text average looks worse than a row of stars).
- **Related issues:** none.
- **Scope:** how the gallery shows a game's rating average, on game cards and on the play page.
- **Depends on:** Phase 9 ratings (v0.31/v0.32) and Phase 10 comments (v0.35, committed `c578419`).

## File Manifest

| Action | File | Purpose |
|--------|------|---------|
| MODIFY | `Deploy/gallery/src/strings.rs` | `rating_summary` (`★ 4.3 (12명)`) becomes `rating_label` (`5점 만점에 4.3점, 12명`) and `rating_count` (`(12명)`) |
| MODIFY | `Deploy/gallery/src/ratings.rs` | `summary()` returns markup: the star image and the count; an integer function for where the gold stops; the rate answer's `summary` carries that markup; `play_parts` uses it; unit tests |
| MODIFY | `Deploy/gallery/src/gallery.rs` | The card's rating line takes the markup (only if `unwrap_or_default()` on `Option<Markup>` doesn't compile; maud's `PreEscaped` derives `Default`, so likely untouched) |
| MODIFY | `Deploy/gallery/static/gallery.css` | Average stars: colors, `[hidden]` rule, no pointer feedback, alignment in the title row |
| MODIFY | `Deploy/gallery/static/gallery.js` | After a vote, put the server's markup into `#rating-avg` (it never holds user text) |
| MODIFY | `Deploy/gallery/tests/ratings.rs` | Assertions on the text summary move to the label and the star markup |
| MODIFY | `Deploy/gallery/tests/look.rs` | Same in the look check, plus star-markup checks |
| MODIFY | `Deploy/gallery/tests/comments.rs` | The "no star on a comment" check (`!html.contains('★')`, line 355) would never fail once the average is an svg; point it at the new markup |

---

## Steps

### Step 1: Strings
- **MODIFY `strings.rs`**:
    - Remove `rating_summary`. Add `rating_label(tenths, count)` → `5점 만점에 4.3점, 12명` and `rating_count(count)` → `(12명)`.
- **Verify:** `grep -rn rating_summary Deploy/gallery` returns nothing once Steps 2–4 are done.

### Step 2: The star image
- **MODIFY `ratings.rs`**:
    - **Geometry:** five 7 × 7 pixel stars 1 unit apart make a 39 × 7 image, drawn at exactly `width="78" height="14"`. That's 2 CSS px per unit, so it's smaller than the 20 px button stars and the pixels stay even.
    - **`gold_halves(tenths) -> i64`**, integers only, no floats: where the gold stops, in half units (one CSS px each). `whole = tenths / 10`, `part = tenths % 10`. In tenths of a unit, the edge is `whole × 80 + part × 7`. It is measured on the star itself and not on the gap, and clamped to 390. Convert to half units rounded to the nearest whole: `(t + 2) / 5`. That puts the clip edge on a whole CSS px, so no blurry column. It is rendered as `{h/2}` or `{h/2}.5`.
    - **Expected values:** 1.0 → 16 (8 units: the gap after a full star is empty, so it looks the same as 7). 4.3 → 68 (34). 4.25 → 43 → 68. 4.7 → 74 (37). 5.0 → 78 (39, clamped).
    - **Known effect:** a star's pixel columns are 1/4/5/5/5/4/1 tall, so the fill isn't linear in area: 4.1 looks almost like 4.0 and 4.9 almost like 5.0. After rounding, 4.3 clips at 34 units (2 columns, 5 of 25 pixels, 20% of the fifth star) and 4.7 at 37 (5 columns, 20 of 25, 80%), so the design goal holds.
    - **`summary(game) -> Option<Markup>`:**
        - An outer `span.avg-rating title=(label)` (not `.avg`: the play page's `#rating-avg` already has that class) holds both parts below, so hovering the stars or the visible count shows the number (round 1, F5).
        - `span.avg-stars role="img" aria-label=(label)` holding one `svg viewBox="0 0 39 7" width="78" height="14" shape-rendering="crispEdges" aria-hidden="true"`.
        - The svg draws the five stars with class `avg-off`, then a nested `svg width=(gold) height="7"` (no viewBox: it uses the parent's units and trims its content at its own edge, so it needs no clip ID and many cards can share a page) with the same five stars in class `avg-on`.
        - Then a visible `span.avg-count aria-hidden="true"` with `rating_count`: the label already says the count.
        - **Class names never start with `star`**: `look.rs:179` counts `class="star` prefixes and `gallery.css:128-132` styles `.star`.
        - Sizes are presentation attributes, never `style="…"`: the CSP (`guard.rs:21`) is `style-src 'self'`.
    - **Star path:** one five-star path, built once from the existing 7 × 7 one shifted by 8 per star. `star_icon` stays for the buttons.
    - **`rate`'s JSON:** `"summary"` becomes the rendered markup (`summary(&game).map(Markup::into_string)`).
    - **`play_parts`:** `#rating-avg.avg` wraps the markup, still `hidden` until the first rating.
    - **Unit tests:** remove the `rating_summary` asserts (`ratings.rs:220-221`); add `gold_halves` for the values above, plus `rating_label(43, 12)` and `rating_count(12)`.
- **Verify:** `cargo test --lib ratings`.

### Step 3: Look and script
- **MODIFY `gallery.css`**:
    - `.avg-on { fill: var(--accent) }`, `.avg-off { fill: #6a76a3 }`: at least 3:1 against the card (`--surface`, 3.32:1), the play page (`--deep`, 4.31:1) and the gold (3.07:1). `--line` would be 1.47:1 on a card, effectively invisible (round 1, F1). Confirm in the screenshots.
    - `.avg-stars` and `.avg-count` sit on one line. `.avg-count` keeps today's muted color.
    - `#rating-avg[hidden] { display: none }`, because any display rule on `.avg` would override `hidden` (the same rule as `gallery.css:125,127`).
    - `#rating-avg { cursor: default }`, with no hover change.
    - `.play-title` aligns on the baseline (`gallery.css:117`), so lining the stars up with the h1 is a flex alignment (`align-self`), not `vertical-align`. Check it in the screenshots.
    - Cards keep their fixed rating-line height (`min-height`), so a card without ratings is still as tall as one with.
- **MODIFY `gallery.js`**: after a saved vote, `avg.innerHTML = r.answer.summary || ""`, which replaces `textContent`. Update the header comment: the average is markup the server rendered, never user text.

### Step 4: Tests
- **MODIFY `tests/ratings.rs`**: every `rating_summary(…)` assertion checks `aria-label="<rating_label(…)>"` instead. The label also sits in `title`, so a bare label match would count twice. Line 140's `matches(…).count() == 2` must match the `aria-label="…"` form. The rate answer's `summary` contains that `aria-label` and `(N명)`.
- **MODIFY `tests/look.rs`**: the same swap at lines 219–227. Add:
    - in `list-public`, the number of `class="avg-stars"` equals the number of rated games shown;
    - each page that lists ratings has one inside `#rating-avg`;
    - the 4.3 game's nested svg has `width="34"`.
- **MODIFY `tests/comments.rs`** (line 355): `!html.contains("avg-stars")` and `!html.contains("aria-label=\"5점")` in place of the `★` check.
- **Verify:** `cargo test` in `Deploy/gallery/`.

### Step 5: Run Tests
- `cargo test` and `cargo clippy --all-targets -- -D warnings` in `Deploy/gallery/`.
- The look check: `cargo test --test look` writes `TestResults/gallery-look/`. Serve that folder on localhost (never `file://`) and screenshot:
    - `list-public`, `list-mine`, `play-rated-one`, `play-rated-avg` and `play-own` at 1280 × 900 and 390 × 844;
    - the play pages also at 1366 × 768.
- Check that the font loaded, there's no overflow at 390 px and the play frame stays 16:9.

### Step 6: Verify
- Walk the Completion Criteria. Look at the screenshots:
    - the 4.3 game's fifth star shows a gold sliver; the 3.0 game has two clearly visible grey stars (no fixture game averages 5.0; that case rests on the `gold_halves` unit test, round 1, F2);
    - pixel edges are crisp, with no blurry column at the clip;
    - the average stars are visibly smaller than the rating buttons and sit level with the title.

### Step 7 (Final): Code Review
- Run the code-review agent (`.claude/agents/workflow/code-review.md`) on the changed files. Fix HIGH/MED issues.

---

## Completion Criteria
- [ ] `grep -rn "rating_summary\|★" Deploy/gallery/src Deploy/gallery/tests` returns nothing.
- [ ] `cargo test` and `cargo clippy --all-targets -- -D warnings` pass in `Deploy/gallery/`.
- [ ] A rated card and a rated play page hold `role="img"` with `aria-label="5점 만점에 X.Y점, N명"`, a matching `title` on the element that holds both the stars and the visible `(N명)`. An unrated card has no stars and keeps its height.
- [ ] `gold_halves` uses only integer maths and passes its unit tests (1.0, 4.25, 4.3, 4.7, 5.0).
- [ ] The rate answer's `summary` is the star markup, and `gallery.js` puts it into `#rating-avg` with `innerHTML`.
- [ ] `tests/comments.rs` still proves a comment carries no rating, against the new markup.
- [ ] Screenshots at the three sizes show the fill, smaller non-clickable average stars on the play page, the font loaded, and no overflow at 390 px.

---

## Notes to Spec / Open Risks
- **Phones have no hover.** With the number hidden, a student on a phone sees the stars and the count but not "4.3". The operator chose this (2026-11-24); the screen-reader label still has it.
- **Screen readers** may also announce the outer `title` as a description of the group, repeating the label. Accepted, so the hover text stays.
- **GDD wording.** GDD `systems.md:221` (the average "shows to one decimal … (★ 4.3 (12명))") and the Glossary's Rating row describe the text form. They change in Post-Plan Cleanup.
- **How the average redraws.** Earlier I told the operator the rate endpoint would send the average and the count for the script to draw. It sends the server's rendered markup instead (see Design choice). The visible result is the same.
- **Deploying.** The live gallery runs on the Mac Mini, and deploying it (build, install, restart) is the operator's routine after this commit. The stylesheet and script addresses carry a content hash (`pages.rs:32-33`), so browsers fetch the new files.

---

## Post-Plan Cleanup
After code review passes:
1. Append execution trailers to this plan file:
   - `**Approval:** User confirmed YYYY-MM-DD.`
   - `**Execution:** COMPLETE YYYY-MM-DD.`
   - `**Verification:** <one-line summary of what passed>`
2. Move this plan to `Documents/Plans/archive/`.
3. Update GDD `systems.md` § Ratings (stars filled to the average plus the count; the number in the hover text and the screen-reader label) and the Glossary's Rating row. Add Changelog v0.36 and the GDD version line v0.36.
4. Deferred items: file any leftovers in `Documents/Operations/Deferred.md`, and delete any entry whose trigger fired.
5. Commit all plan work as `v0.36: gallery — rating average shown as stars`.

## Workflow
Execute steps sequentially. Proceed to the next step without waiting for user approval. Only stop if blocked. If blocked, stop and report: what step, what was attempted, what failed.


## Review round 1

---
slug: gallery-star-average
round: 1
verdict: approved
reviewer: claude-cowork
updated: 2026-11-24T15:58:00+09:00
blocking: 0
recommendations: 0
---

# Review round 1: gallery-star-average

## What I could run

I had shell access to the laptop's daedalus folder (grep, sed and git over the repo). I also had a cloud shell, where I
downloaded the maud 0.27.0 crate and checked its source, and computed WCAG contrast ratios. I did **not** compile
anything, run `cargo test` or clippy, or take screenshots. Every claim that the code compiles, the tests pass or the
pages look right is still the Author's to run.

## What I verified

- **Geometry and `gold_halves`, recomputed.** 5 × 7 + 4 gaps = 39 units. The edge in tenths of a unit is
  `80·whole + 7·part`, and `(t + 2) / 5` rounds to the nearest half unit (t is an integer and 5 is odd, so there are no
  ties). 1.0 → 80 → 16. 4.3 → 341 → 68 (34). 4.7 → 369 → 74 (37). 5.0 → 400, clamped to 390 → 78 (39). 4.25 gives
  `average_tenths(17,4)` = 43 (`ratings.rs:30-32`), so 68. All match the plan.
- **The pixel column heights**, from the path at `ratings.rs:203`: 1/4/5/5/5/4/1, 25 pixels in all. Correct.
- **The nested-svg clip.** An inner `<svg>` with no viewBox uses its parent's user units. The UA rule `overflow: hidden`
  for non-root svg clips it at its own width. `gallery.css` has no `svg` rule that would override the presentation
  attributes (the only one is `.scene svg`, line 226). This works without clip IDs, as the plan says.
- **`Option<Markup>::unwrap_or_default()`** (`gallery.rs:53`) still compiles. maud 0.27.0 has
  `impl<T: Default> Default for PreEscaped<T>` (`lib.rs:250`). That is a hand-written impl, not a derive (the derive at
  line 223 is only `Debug, Clone, Copy`), but the conclusion is the same: `gallery.rs` stays untouched.
- **CSP** (`guard.rs:21`): `style-src 'self'`, so presentation attributes are the right call. There is no
  `require-trusted-types-for`, so `innerHTML` is allowed, and `gallery.js` already uses `insertAdjacentHTML` for
  comment markup from the server (lines 187, 240, 313).
- **Citations.** `ratings.rs:220-221`, `comments.rs:355`, `tests/ratings.rs:140` (`matches(&summary).count() == 2`),
  `gallery.css:117` (baseline), `:125,127` (`[hidden]` rules), `:128-132` (`.star`), `pages.rs:32-33` (hashed URLs),
  `systems.md:221` and the Glossary's Rating row all match. One line number is off, see F4.
- **The `★` / `rating_summary` sweep.** Every use in `src`, `static` and `tests` is in the manifest. The only ones
  are `gallery.rs:53`, `ratings.rs:34-37,147,154-155,220-221`, `strings.rs:103-105`, `gallery.js:117-118`,
  `comments.rs:355`, `look.rs:219,222` and `tests/ratings.rs:123-142,223,239`. The Completion Criteria grep will
  hold.
- **The card's height.** `.card .rating` is already `display:block; min-height:1.5em; line-height:1.5` at 0.9rem
  (21.6 px), so a 14 px svg fits the existing line.

## Findings

### F1 · non-blocking · design · Step 3, `gallery.css` `.avg-off`
The first choice of grey, `var(--line)` (#34406b), is nearly invisible against the backgrounds it sits on:
**1.47:1 on the card's `--surface` (#1e2742)** and 1.9:1 on the play page (`body:has(.theater)` is `--deep`,
#0b0f1c). The plan's own check, "a 3.0 game has two grey stars", depends on those stars being visible. With
`--line` a 3.0 game reads as three stars on a card, and the hidden number makes that worse. `#4a5788` (the dashed
add-card border) is still only 2.11:1. `--muted` (#a3acc8) is clearly visible (6.5:1) but only 1.56:1 against the
gold, so the edge of the fill would blur. **Fix:** name a concrete grey that meets 3:1 against both backgrounds and
against the gold. For example `#6a76a3`, which is 3.32:1 on the card, 4.31:1 on the play page and 3.07:1 against
`--accent`. Then confirm it in the screenshots.

### F2 · non-blocking · accuracy · Step 6, "a 5.0 game is fully gold"
The look fixture (`tests/look.rs:86-96`) rates only `old` (5, 4, 4 → 4.3) and `traps` (3 → 3.0). No screenshot will
show a 5.0 game, so this check can't be done as written. **Fix:** either have the 5.0 case rest on the
`gold_halves` unit test and drop it from Step 6, or add a 5.0 rating to a fixture game that has none. Before choosing
the second, check the counts that game already feeds in `look.rs` (comments, list cards).

### F3 · non-blocking · accuracy · Step 2, "Known effect"
"4.3 against 4.7 is about 22% against 78% of the fifth star" uses the unrounded edge. After `gold_halves` rounds,
4.3 clips at 34.0 units, which is exactly 2 columns (1 + 4 = 5 of 25 pixels, **20%**). 4.7 clips at 37.0, which is
5 columns (20 of 25, **80%**). The conclusion holds and the gap is a little wider than stated. Correct the numbers.

### F4 · non-blocking · accuracy · Step 2, class-name note
"`look.rs:177` counts `class="star` prefixes": the count is at **`look.rs:179`** (`assert_eq!(html.matches(r#"class="star"#).count(), 5, …)`).
Line 177 is the `data-…` assert.

### F5 · non-blocking · design · Step 2, where `title` sits
`title` sits on `span.avg-stars` only, and `span.avg-count` is its sibling. Hovering the visible `(12명)`, the part
that reads as text and invites a hover, shows no tooltip, so the one place a mouse user can still see "4.3" is
smaller than it looks. **Fix:** put `title` on an element that holds both: on the card, on `span.rating`, or on
a wrapper returned by `summary()`. Keep `role="img"` and `aria-label` on the stars. Or state that stars-only hover
is intended.

## Reviewer's view

I would build this as written. The integer clip math, the nested-svg approach, the CSP and `innerHTML` reasoning,
and the test sweep all check out against the code. Nothing blocks. Fold in F1 (a concrete grey that meets 3:1;
the plan's first choice is effectively invisible), F2 (the 5.0 screenshot check has no fixture behind it), and
the F3/F4 corrections. F5 is a small usability call. Compiling, the tests, clippy and the screenshots are still
unrun and are the Author's to do.


---

**Approval:** User confirmed 2026-11-24 (handshake approved in round 1).
**Execution:** COMPLETE 2026-11-24.
**Verification:** `cargo test` 220 green (+2) and clippy clean in the Linux container; look check over CDP at 1280 × 900, 390 × 844 and 1366 × 768 (no overflow, font loaded, exact fill with crisp edges). Code review: PASS, no HIGH/MED; LOW 1, 2 and 6 folded (both layers and the half-pixel width tested, the `[hidden]` rule commented), 4 (the five-star path rebuilt per card) left as negligible.
**Version note:** `cursor: default` sits on `.avg-rating` (cards and play page), not only `#rating-avg` as Step 3 says.
