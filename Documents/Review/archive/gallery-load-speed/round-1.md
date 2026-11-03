---
slug: gallery-load-speed
round: 1
verdict: approved
reviewer: claude-cowork
updated: 2026-11-03T23:52:00+09:00
blocking: 0
recommendations: 0
---

# Review round 1: gallery-load-speed

## What I could run

Shell over the connected Mac mini folder (`device_bash`): read, grep, sed over the repo. I ran no build, no
`cargo test`, no tunnel, no Chrome, no timing. Every measured number in § Source (157.6 s, 44.6 s, 13.6 s,
~155–160 KB/s on Orcarin, 36 MB/s up, 0 receive-buffer hits, `maxsockbuf` 8 MB, the smooth 0 → 0.9 progress)
is **accepted on trust**: I can't reproduce any of it from here.

## What I verified

- `Deploy/cloudflared/suwon.yml` has no `protocol` key today (tunnel `suwon`, two hosts → `127.0.0.1:8470`), so
  "no protocol set" = cloudflared's default; adding a top-level `protocol: http2` is the right place.
- `Deploy/launchd/com.orcapafea.daedalus.gallery-tunnel.plist`: label `com.orcapafea.daedalus.gallery-tunnel`,
  reads `~/.cloudflared/suwon.yml`, logs to `~/Library/Logs/daedalus-gallery/tunnel.log`; its header documents the
  same copy + `kickstart -k` step Step 2 uses. Matches.
- `play.rs:94-122` `player_page`: canvas, `p #error hidden`, `script src="player.js"`, one inline `style` string;
  `#error` is `position:fixed`. The new `#loading` slots in after the canvas as described.
- `guard.rs:25-28` `PLAY_CSP` has `style-src 'self' 'unsafe-inline'`, so the extra inline rules need no hash change;
  no font source is allowed (`default-src 'none'`), consistent with "no web font".
- `player.js:6-26`: `fail()` is the single failure path and `script.onerror = fail`, so hiding `#loading` in
  `fail()` covers loader-missing, loader-load-error and Unity rejection. `createUnityInstance` currently takes two
  arguments; the third (progress callback) is the documented slot.
- `strings.rs:81` `PLAYER_LOAD_FAILED` exists; `PLAYER_LOADING` goes beside it.
- `tests/play.rs:87-104` `the_player_page_names_exactly_the_stored_files` exists and already asserts on the page's
  inline style (`background:#0b0f1c`), so string-asserting `#loading[hidden]{display:none}` fits its pattern.
  `include_str!("../static/player.js")` from `tests/` resolves to `Deploy/gallery/static/player.js` (the same path
  `play.rs:130` uses from `src/`). `getElementById("loading")` (with the closing quote and paren) does not match
  `getElementById("loading-bar")`, so the two asserts are independent, as intended.
- `gallery.css:145-148`: the `progress` rules (`appearance: none`, `::-webkit-progress-bar`,
  `::-webkit-progress-value`) exist to copy; `#0b0f1c` is the player page's body background, so at 0 % only the
  border shows, which Step 5 checks for.
- The `hidden` override: an author `display:flex` on `#loading` does beat the UA `[hidden]{display:none}`, so the
  critic's HIGH fix is real and needed.
- `play_token.rs:11,14`: enter grant 60 s, gate 2 h. Step 1 must enter fresh per run (it does), and a slow QUIC run
  (3 × ~150 s + a Chrome start) stays well inside the 2 h gate.
- `framework.md:67` is the browser↔edge HTTP/3 line, as Step "Post-Plan Cleanup 3" says.
- ISS-002 (`Issues.md:17-21`) text matches the plan's reading (63 s first load, never-finishing `*.wasm.br`,
  cache-clear recovery, the 60 s watchdog as a next step).

## Findings

### F1 · non-blocking · accuracy · Step 3 / Step 5, phone frame size
At 390 px the play frame is not "~350 px wide" nor "~219 px tall". `main` has 16 px side padding
(`gallery.css:36`) and `.frame` is `min(100%, (100dvh − 220px) · 16/9)` at 16:9 (`gallery.css:120`); at 390 × 844
the second term is ~1109 px, so the frame is 390 − 32 = **358 px wide, ~201 px tall** (219 is 390 · 9/16, the
unpadded width). Inside the frame `80vw` ≈ 286 px, so the 240 px bar fits either way, and label + gap + 16 px bar
fits in 201 px. Failure scenario: the look check is judged against a 219 px height the frame never has. Fix: say
358 × ~201 in both places.

### F2 · non-blocking · accuracy · Overview, "How you'll know it worked"
It promises "starts in seconds instead of minutes" unconditionally, but Step 2 and Open Risks keep QUIC (no speed
change) as a valid outcome, after which only the bar ships. Failure scenario: Step 6 walks the criteria, QUIC was
kept, and the Overview's headline success reads as failed (or gets quietly ignored). Fix: make it conditional
("if HTTP/2 wins, … ; either way, the bar moves while it loads").

### F3 · non-blocking · design · Step 2, keep rule
"Median at most half of QUIC's" discards a consistent but smaller gain. With the rounds alternating, an HTTP/2
that wins both pairs by, say, 40 % (157 s → 95 s) is a real improvement and gets thrown away, and the plan names no
cost of HTTP/2 that a 2× bar would be protecting against (the TCP 7844 egress is checked by the hosts-answer step).
Fix: either state the cost that justifies 2× (e.g. "a switch is only worth an Orcarin conversation if it's big"),
or use "HTTP/2 wins both paired rounds and its median is at least ~25 % lower". The Chrome-start guard can stay.

### F4 · non-blocking · design · Steps 1, 2 and 5, where the client sits
Step 1's script and the CDP Chrome run on the Mac mini itself, so every timing is a hairpin: client → Seoul edge →
tunnel → the same Mac. The tunnel leg is still what's measured, so the comparison stands, but students load from
the school network, and the plan never says where the original 157.6 s / 44.6 s loads ran. Failure scenario:
HTTP/2 wins on the Mac, and a classroom load is still slow for a reason the hairpin can't show (or the reverse).
Fix: state where § Source's numbers came from, and after the decision add one load of the test game from another
network (the office PC or a phone off Wi-Fi) to Completion Criteria, timed by eye or DevTools.

### F5 · non-blocking · accuracy · Step 5, throttled screenshots
The throttled check assumes `Network.emulateNetworkConditions` on the page also slows the frame's requests.
`suwon-play.orcarin.com` and `suwon.orcarin.com` are the same site (`orcarin.com`), so Chrome normally keeps the
frame in-process and the throttle applies, but the plan doesn't check it. If HTTP/2 makes the load fast and the
throttle silently misses the frame, the "part-filled" screenshot is never caught and the step looks blocked for the
wrong reason. Fix: in the script, confirm the throttle took (the wasm request's duration in the CDP Network events
is ≥ size / throttle rate), or attach to the frame's target and throttle it too.

## Reviewer's view

I would build this as written. The code side is small and its wire is tested at both ends; the tunnel experiment
is fair (alternating rounds, log-confirmed protocol, both hosts checked, revert path). F3 is the only one that can
change the outcome of the experiment; F1, F2, F4 and F5 are wording and check hygiene. No blocking findings, so
approved in round 1; fold F1–F5 on close.
