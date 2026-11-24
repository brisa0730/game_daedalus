# Phase 9 log: `gallery-ratings` (v0.31, 2026-11-24)

Prompt: [phase9-gallery-ratings.md](../../../../Documents/Phases/archive/phase9-gallery-ratings.md) (design panel; handshake approved round 2: [record](../../../../Documents/Review/archive/phase9-gallery-ratings/)).

## Decisions on the way (operator, this session)
- Students asked for 5-star ratings. Operator: public average (a); anyone rates, keyed by a browser cookie, not accounts; teacher doesn't see who rated; duplicates via incognito accepted; 3 minutes (not 5) after first open, time open not played; average from 3 ratings; stars first, comments later.
- Dropped during the design review: a reset-ratings option on update ("opens unnecessary issues"), a fresh cookie at sign-in/out for shared laptops, an operator clear command. Kept: DB open records + 30-day prune.

## What was built
1. `0004_ratings.sql`: `game_opens` (first open per browser per game) and `ratings`, both keyed by `(game_id, rater_hash)`, no user column.
2. `db.rs`: rating totals in `GAME_COLUMNS` (two correlated subqueries; every card gets them without a second query); `record_open` and `rate` check the game is live inside the statement; `rate` checks the gate (`opened_at <= now - gate`) in the same upsert; `tombstone_game` is now a transaction that deletes ratings and opens (games are never deleted, so no cascade fires).
3. `ratings.rs`: two Open POSTs. `rate` takes `Bytes` last so the game check runs before the body is read (the routing test POSTs `text=x` to game `1` and expects 404, not 422). `parse_stars` accepts exactly `stars=1..5`.
4. Play page: the GET only reads (`play_parts`); the script records the open. Countdown from `data-wait` at once, reset by the open's answer; a 409 re-sends the open (handshake F1).
5. Prune at server start only (a backup process that deletes rows would surprise a restore).

## Verification
- The crate is Unix-only; from the Windows laptop it ran in Docker (`rust:1`, 1.100). 190 tests (+19). Clippy: one pre-existing `result_large_err` in `play.rs` on the newer toolchain → Deferred.md.
- Look check: 58 screenshots, automated overflow / font / 16:9 / frame-in-view / equal-card checks, all looked at. The ★ glyph comes from a fallback font (Galmuri11 lacks it); it looks fine.
- Script check in headless Chrome against a fake server: 18 checks pass. Stars appear up to ~1 s after the deadline (1 s tick); fine.
- Code review: MED (failure note green: `#rate-note` id rule beat `.error`) fixed with `#rate-note.error`; LOWs fixed (stale saved note on 409, unlogged read errors, missing `#rate-buttons[hidden]`, duplicate `live`/`answer` helpers).

## Not done here
- Deploy to the Mac Mini (no SSH from the laptop) and the operator check on the live gallery (prompt Step 11).
