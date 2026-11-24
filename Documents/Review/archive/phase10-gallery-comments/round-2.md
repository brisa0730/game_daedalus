---
slug: phase10-gallery-comments
round: 2
verdict: approved
reviewer: claude-cowork
updated: 2026-11-24T15:12:00+09:00
blocking: 0
recommendations: 1
---

# Round 2: Phase 10, gallery comments (delta review)

## What I could run, what I verified, what I could not

**Ran:** `device_bash` on the laptop's daedalus folder (grep/sed over the plan, `log.md` and `Deploy/gallery/`). No cargo, no Chrome.

**Delta checked against round 1 and the log's dispositions:**
- **F1 closed.** Step 8 now guards `player.js`'s start-up and `started()` focus with `document.hasFocus()` and keeps the click and window-focus listeners; the manifest lists `player.js`; Step 16 adds typing while the game loads. Verified: `play.rs` 146 embeds the file with `include_str!` and 149 builds `PLAYER_JS_URL` with `pages::versioned("player.js", PLAYER_JS)`, so the plan's "reaches browsers through its fingerprinted address" and "`play.rs` still doesn't change" both hold (the Deferred `result_large_err` trigger stays unfired).
- **F2 closed.** 16.3 copies the printed backup to `daedalus-gallery-backups/pre-v0.35.db`; 16.4 and 16.7 use it; Step 13 adds the rule to platforms.md and `backup.rs`'s note. Verified: `prune_copies` (backup.rs) only selects names starting `gallery-` and ending `.db` in `<to>/db/`, and `pre-v0.35.db` sits one level up, so neither the nightly rename nor the 7-copy prune can touch it.
- **F3–F7 closed** as described in the log (lines 171, 172, 202, 264, 272, 311 of the plan).
- **F8 closed**, with the seam noted in F9 below.
- **R1** is written beside settled item 8 as an open question for the operator, and the plan builds the settled version meanwhile. That's how the protocol says to handle it.
- The plan now includes round 1 verbatim under `## Review round 1`, which is what protocol.md line 164 says to do.

## Non-blocking

### F9 (non-blocking, design): with no local length check, a long paste gets "try again"
**Location:** Step 8 counter (line 305, the F8 remedy) and Step 6 (form rejection → `COMMENT_FAILED`).
**Defect:** this is a seam from F8. Before, the page refused a raw draft over 300 characters, so a request never got near the 4096-byte limit. Now the page sends any length. A student who pastes about 1,400 Korean characters (9 encoded bytes each) goes over 4096 bytes, the `Form` rejection is a 413, and the plan answers it with `COMMENT_FAILED` "남기지 못했어요. 다시 눌러 보세요." Pressing again can never work. Map a 413 rejection to `COMMENT_TOO_LONG` (and say so in test 6's 5000-byte case), or keep a coarse local refusal when the raw count is well over the max (for example, more than twice it).

## Recommendation
R1 stays open for the operator (no change).

## Reviewer's view
Both blocking findings are fixed, and I checked the fixes against `play.rs` and `backup.rs`. All of round 1's non-blocking findings are folded in. F9 is one line in the rejection mapping. I'd build this as written once F9 is folded in. Approved.
