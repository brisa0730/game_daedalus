# Handshake log: gallery-star-average

## Opened 2026-11-24T15:44:20+09:00
Doc: `Documents/Plans/gallery-star-average.md`. It proposes showing a gallery game's rating average as five small pixel stars, gold filled to the average (integer clip in whole CSS px), plus the visible rater count, in place of the text `★ 4.3 (12명)`. The number is kept in the screen-reader label and the hover text. The play page's average stars are smaller and not interactive, and they redraw after a vote from server-rendered markup. Touches Deploy/gallery src (strings, ratings), static (css, js) and three test files. A critic sub-agent's 14 findings are already folded in. Nothing is built yet.

## Round 1 dispositions (2026-11-24T15:50:29+09:00) — verdict: approved
- **F1 accepted.** Recomputed WCAG ratios: `--line` #34406b gives 1.47:1 on `--surface` and 1.9:1 on `--deep`; #6a76a3 gives 3.32 / 4.31 / 3.07:1 against the gold. Step 3 now names `.avg-off { fill: #6a76a3 }`.
- **F2 accepted, first remedy.** Confirmed `look.rs:86-96` rates only `old` (5,4,4) and `traps` (3). The 5.0 case rests on the `gold_halves` unit test. Step 6 no longer asks for it, so no fixture counts change.
- **F3 accepted.** Recounted columns 1/4/5/5/5/4/1: 34 units = 2 columns = 5/25 = 20%; 37 units = 5 columns = 20/25 = 80%. Known-effect line corrected.
- **F4 accepted.** `look.rs:179` holds the `class="star` count (177 is the data- assert). Citation corrected.
- **F5 accepted.** An outer `span.avg-rating` carries `title` around the stars and the visible count. `role="img"` and `aria-label` stay on `span.avg-stars`. Self-check of the fold: named it `avg-rating`, not `avg`, because `#rating-avg` already has class `avg` (`ratings.rs:155`, `gallery.css .playbar .avg`). The screen-reader note in Open Risks was updated to match the moved `title`. The Completion Criteria line was reworded to put `title` on the element holding both.
- Close fold re-verified: the plan's numbers (16/68/74/78 halves; 20%/80%) and citations (`look.rs:179`) were re-derived against the code. Nothing has been built; the plan goes to commit, then execution.
