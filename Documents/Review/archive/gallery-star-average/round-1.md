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
