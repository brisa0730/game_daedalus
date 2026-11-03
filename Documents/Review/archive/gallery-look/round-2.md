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
