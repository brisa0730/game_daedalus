---
slug: phase7-first-release
round: 2
verdict: approved
reviewer: claude-cowork
updated: 2026-11-09T14:40:00+09:00
blocking: 0
recommendations: 0
---

# Review round 2: Phase 7 First Release (delta)

## What I could run

Shell over the connected folder: `sed`/`grep` over the doc and `log.md`. I didn't run Unity, tests, or `git check-attr`. The Author reports reproducing F3 in a scratch repo, and reading `Sample.ImportOptions` and the template `displayName`s from the editors (F6, F7). I accepted those on trust.

## Round 1 findings

- **F1: closed.** Step 7 gives the Falling Ceiling an explicit local zone from its box down 9 tiles, which matches the bench (trap at y 10, zone y 1 to 10, TrapTestSceneBuilder.cs:66). It says what happens at another height. Step 8 item 7 tells students to check the box and drag its handle. There is a completion criterion. "Already down" and "with their defaults" are gone.
- **F2: closed.** `StarterFrozen.txt` (path, GUID and SHA-256 per piece), the rule "a frozen piece never changes, a new behaviour is a new prefab", and a re-import test that changes a value. Leaving the scene out is right: a saved copy doesn't reference it. One seam remains, filed as F17.
- **F3 to F9: closed** as folded. Step 4's rule now covers images only, with the scene check added. Testables and the fixture reference are stated. The fixture and Share runs use scratch copies under `Tools/compat/`, with a `git status` check afterwards. The re-import test runs both ways on a real 0.1.1. Template parity is recorded, and so is the README line. The sample copy-back now mirrors. Step 2 now says to measure tile size.
- **F10 to F16:** recorded beside Open questions 1 to 7. Nothing further.

## New findings

### F17: The content freeze and the generator collide after the tag
- **Type:** non-blocking · **Category:** design · **Location:** Step 5 (`build_starter.py`), Step 7 (Freeze GUIDs and content)
- The freeze hashes each piece's bytes, and `build_starter.py` re-saves every tile, prefab and sprite on each run (they are "reused by path", but saved again). After the tag, `--refreeze` only adds entries. A later run on a different 6000.0 patch, or one where Unity re-serializes a prefab (a `serializedVersion` bump, a reordered field), changes the bytes without changing any value. The freeze test then fails and can't be refrozen, and the only way out is to rename a piece students already use.
- **Fix:** once `0.1.0` is tagged, the generator never rewrites a frozen piece. It copies the frozen file from the seed untouched and builds only new pieces and the scene. One sentence in Step 5, plus the same as a SelfCheck assertion.

### F18: The fixture can't be made from the registry before the registry exists
- **Type:** non-blocking · **Category:** accuracy · **Location:** Step 9, fixture bullets and item 5
- The committed fixture manifest is "the package at 0.1.0 from the scoped registry". The fixture is made in Step 9, before the OpenUPM build in Step 15, so its creation script has to install from `file:` and then rewrite the manifest. Say so, and say whether its `packages-lock.json` is committed. A lock written during the `file:` install would name a path that exists only on the dev machine.

## Late-pushback test

Asked "what do you think?", I would add only F17 and F18. Both are filed above.

## Reviewer's view

Approved. Both blocking findings are fixed in the text, with tests behind them (F1's completion criterion, F2's frozen list and value-changing re-import test). Fold F17 before Step 5 is built, because it decides whether the freeze is livable after the tag. F18 is a wording fix.
