# Plan: Gallery Load Speed

**Date:** 2026-11-03 | **Status:** approved (handshake round 1, 2026-11-03; critic-reviewed) — not yet executed

## Overview

**Where things stand:** Opening a game in the class gallery can show a black box for a minute or more, and sometimes longer than a player will wait. The game is not broken: its files reach the browser very slowly through the connection that carries the gallery to the internet, and the black box gives no sign that anything is happening.

**What this plan changes:**
- The gallery's internet connection is tried on the same kind of connection the computer's fast speed test used, and kept that way only if side-by-side timings show it is clearly faster.
- While a game loads, players see "게임을 불러오는 중…" with a bar that fills up, instead of a black box.

**How you'll know it worked:** While a game loads, you see the bar move instead of a black box. If the new connection wins the timings, a game also starts in seconds instead of minutes, including when opened from another network.

**Not in this plan:** making the games themselves smaller, a time-out message for a load that never finishes, or the operator's other website on the same computer (it has the same slow connection but small files).

**Why a plan and not a patch:** it changes the live connection's settings and the gallery server, and restarts both.

**Design choice:** switch the transport rather than add caching at the network's edge. Game files are behind a per-visit pass, so the edge can't keep them, and the connection itself is the measured bottleneck.

---

## Source
- **Trigger:** operator report 2026-11-03: a game uploaded to the live gallery showed only a black frame. Investigation: Unity's loader was still downloading. The gallery server sends the 7.3 MB code file in 12 ms locally, but it took 157.6 s and later 44.6 s through the `suwon` tunnel (cloudflared 2026.6.1, `protocol=quic`), and the 2.7 MB data file took 13.6 s. The Mac Mini's own line measured ~36 MB/s up over TCP (the tunnel uses QUIC over UDP). The tunnel's edge is Seoul (icn01/06/07). The usual macOS cause, quic-go's "failed to sufficiently increase receive buffer size", is absent from `tunnel.log` (0 hits) and `kern.ipc.maxsockbuf` is 8 MB, so the UDP-side cause is unproven: HTTP/2 is a test, not a known fix. **Control:** Orcarin's own tunnel on the same Mac (`orcarin`, also `protocol=quic`, no protocol set) delivers its largest file (227 KB) at ~155–160 KB/s (1.4 s, 3 runs; 4 ms locally): the same ceiling. Its pages never felt slow only because its files are small; a Unity build is ~40× larger. So the ceiling is the Mac's QUIC tunnels in general, not anything in the gallery. Served from localhost, the same build starts and draws the level. Through the tunnel, Unity's progress callback rose smoothly from 0 to 0.9 in Chrome (the length is known), so a bar can track it. **Where the client sat:** every timing above (curl and Chrome) ran on the Mac Mini itself, a hairpin: Mac → Seoul edge → tunnel → the same Mac. The browser leg is TCP and the TCP speed test was fast, so the slow leg is the tunnel, but students load from the school network, so Completion Criteria adds one load from another network.
- **Related issues:** ISS-002 (its "stall" is this slow download: the 63 s first load and the never-finishing `*.wasm.br` fit a ~50–200 KB/s transfer).
- **Scope:** the `suwon` tunnel's protocol; the games host's player page shows load progress.
- **Depends on:** v0.21 (current gallery and player page).

## File Manifest

| Action | File | Purpose |
|--------|------|---------|
| MODIFY | `Deploy/cloudflared/suwon.yml` | `protocol: http2`, with a comment naming the measurement (kept only if Step 2 shows the gain) |
| MODIFY | `Deploy/gallery/src/play.rs` | Player page: a centered `#loading` box (Korean label + `<progress #loading-bar>`) over the canvas; styles in its existing inline `<style>` |
| MODIFY | `Deploy/gallery/static/player.js` | Pass Unity's progress callback to drive `#loading-bar`; hide `#loading` when Unity starts or fails |
| MODIFY | `Deploy/gallery/src/strings.rs` | `PLAYER_LOADING` = "게임을 불러오는 중…" |
| MODIFY | `Deploy/gallery/tests/play.rs` | The player page carries `#loading`, `#loading-bar` and the label; `player.js` reads those ids (both ends of the wire) |

Live, outside the repo: `~/.cloudflared/suwon.yml` (the copy the tunnel job reads) and the installed gallery binary.

---

## Steps

### Step 1: Timing script
- In the scratchpad: a script that opens the test game's frame as a browser would (`/enter` with `Sec-Fetch-Dest: iframe`, keeping the gate cookie), then times `Build/game.wasm.br` with `Accept-Encoding: br` through `https://suwon-play.orcarin.com` **3 times** and prints each time. Plus the CDP Chrome load (fresh profile, headed): seconds until Unity starts (the canvas leaves 300 × 150).
- **Verify:** one run prints three times and a start time.

### Step 2: QUIC vs HTTP/2, side by side
- **MODIFY `Deploy/cloudflared/suwon.yml`**: top-level `protocol: http2`, with a one-line comment (the measured QUIC vs HTTP/2 medians; see the Changelog entry). HTTP/2 uses outbound TCP 7844.
- Alternate the live tunnel **QUIC → HTTP/2 → QUIC → HTTP/2** (4 rounds, back to back, so time-of-day noise hits both). Each switch: copy the file (with or without the `protocol` line) to `~/.cloudflared/suwon.yml`, `launchctl kickstart -k gui/$(id -u)/com.orcapafea.daedalus.gallery-tunnel`, wait for `tunnel.log` to show the new registrations with the expected `protocol=`, and confirm `https://suwon.orcarin.com/` answers 200 and `https://suwon-play.orcarin.com/` answers (any 403/404 page is fine). Then run Step 1's script.
- **Keep HTTP/2** if it wins **both** paired rounds (each HTTP/2 round's median wasm time below the QUIC round before it), its overall median (6 downloads) is at least **25 % lower** than QUIC's, and no HTTP/2 Chrome start is slower than the slower QUIC one. HTTP/2 has no cost a stricter bar would protect against (its TCP 7844 egress is proven by the hosts-answer check), so a consistent smaller gain is still kept. **Otherwise** leave the live copy without the `protocol` line (QUIC), restore the repo file, and record the numbers for ISS-002.
- **Verify:** `grep protocol ~/.cloudflared/suwon.yml` matches the decision; the latest registrations in the log match it; the repo file matches the live copy.

### Step 3: Loading bar on the player page
- **MODIFY `strings.rs`**: `PLAYER_LOADING: &str = "게임을 불러오는 중…"` next to `PLAYER_LOAD_FAILED`.
- **MODIFY `play.rs` `player_page`**: after the canvas, `div #loading { p { (s::PLAYER_LOADING) } progress #loading-bar max="1" value="0" {} }`. In the existing inline style: `#loading` fixed and centered, column, gap; text `#c9d0e8`, 16px sans-serif; the bar 240 px × 16 px with the gallery's colors (track `#0b0f1c`, border `#34406b`, fill `#4cc26a`), `appearance: none` plus the `::-webkit-progress-*` rules as in `gallery.css`; **`#loading[hidden]{display:none}`** (the box's own `display:flex` would otherwise beat the `hidden` attribute and leave the label over the running game). The bar is at most `80vw` wide (≈ 286 px inside the frame) so it fits the 358 px-wide frame on a phone. No web font: the games host's policy loads none.
- **MODIFY `player.js`**: `createUnityInstance(canvas, config, function (p) { bar.value = p; })`; on resolve set `#loading`'s `hidden` then focus; `fail()` sets it before showing `#error` (`script.onerror` goes through `fail` too).
- **MODIFY `tests/play.rs`**: in `the_player_page_names_exactly_the_stored_files`, assert `<div id="loading">` (rendered without `hidden`), `id="loading-bar"`, `#loading[hidden]{display:none}` and `s::PLAYER_LOADING` are in the page; a new test asserts `include_str!("../static/player.js")` contains `getElementById("loading")` and `getElementById("loading-bar")` exactly (both ends of the wire).
- **Verify:** `cargo test --test play`.

### Step 4: Run Tests
- In `Deploy/gallery/`: `cargo test` and `cargo clippy --all-targets -- -D warnings`.

### Step 5: Deploy and look
- `cargo build --release`, `install -m 0755` into `~/Library/Application Support/daedalus-gallery/bin/`, `launchctl kickstart -k gui/$(id -u)/com.orcapafea.daedalus.gallery`.
- **Look check (replaces the Testing table's `cargo test --test look` row for this change):** the player page lives on the games host, which `tests/look.rs` never renders (its frame is `about:blank`), so the check is live Chrome screenshots instead. In headed Chrome with a fresh profile (CDP script in the scratchpad), open the test game's play page and screenshot at ~1 s intervals until Unity starts. Claude looks for the label and an empty bar visible at 0% (its border against the same-colored background), then a bar that fills, then the level with the label gone. Then throttle the network (CDP `Network.emulateNetworkConditions`, ~1 Mbps) and screenshot mid-load at **1366 × 768** and **390 × 844**: the bar part-filled, and at 390 px the label and bar fit inside the 358 × ~201 px frame (`main`'s 16 px side padding, 16:9) without overflow. Confirm the throttle reached the frame: the wasm request's duration in the CDP Network events is at least its size ÷ the throttle rate; if not, attach to the frame's target and throttle it too. If the bar sits at 0 or full while the download runs, switch to an indeterminate `<progress>` (no `value`) and repeat.
- Check the console for CSP violations from the player page (the Cloudflare beacon's are known and not ours).
- **Verify:** the screenshots show label + filling bar, then the game with the label gone, at both sizes; no new console errors.

### Step 6: Verify
- Walk the Completion Criteria.

### Step 7: Code Review
- Run the code-review agent (`.claude/agents/workflow/code-review.md`) on the changed files. Fix HIGH/MED.

---

## Completion Criteria

- [ ] Step 2's four alternating rounds recorded (3 wasm downloads and a Chrome start each); the protocol kept matches its rule, in both the repo file and the live copy, and the tunnel log confirms it; both hosts answer
- [ ] `cargo test` and `cargo clippy --all-targets -- -D warnings` pass in `Deploy/gallery/`
- [ ] The test asserts both ends of the loading wire: the page's ids and string, and `player.js`'s lookups
- [ ] The live gallery runs the new binary; screenshots show the label and a bar visible at 0% and filling while loading (throttled, confirmed to reach the frame, at 1366 × 768 and 390 × 844, no overflow), then the game with the label gone
- [ ] One load of the test game from another network (the operator's office PC, or a phone off Wi-Fi), its wasm time read from DevTools or by eye, recorded with the decision
- [ ] No new CSP violation from the player page in the console

---

## Notes to Spec / Open Risks
- **HTTP/2 may not help.** If the slowness is on Cloudflare's side rather than UDP, Step 2 keeps QUIC and the bar still ships. ISS-002 then gets the new diagnosis and next options (Cloudflare caching of versioned build URLs, or a smaller build).
- **QUIC can be fast sometimes** (ISS-002's Phase 6 watch saw a 12 s first load), hence the alternating rounds rather than one before/after.
- **Restarts.** Each tunnel restart drops the gallery for a few seconds, and the server restart ends open games' file access (per-process play key; reopening fixes it). The operator confirmed nobody is using the gallery now.
- **ISS-002 stays open either way, narrowed.** A slow download doesn't explain its stall that survived a Chrome restart and cleared only with Chrome's HTTP cache (the cached-partial theory), and a stalled download now shows a frozen bar instead of black. Its Summary is rewritten: the slow-tunnel finding and what this plan did; the remaining unexplained stall; the ~60 s "reopen" watchdog as its next step.
- **Orcarin** (`/Users/brisa/Documents/Applications/Orcarin`, `com.orcarin.cloudflared`) shares the ceiling. If HTTP/2 is kept here, tell the operator; changing Orcarin's tunnel is that project's decision, not this plan's.
- **Critic findings folded in (2026-11-03):** HIGH `#loading[hidden]` rule; MED alternating rounds and a keep rule (later relaxed by handshake F3 to both pairs won + ≥ 25 % lower), progress fallback, ISS-002 kept open and narrowed, live screenshots replace the look row (+390 × 844); LOW exact wire asserts, both hosts checked, 0% bar visible, cleanup items.
- Not ours: the Cloudflare Web Analytics beacon that Cloudflare injects is blocked by our CSP on every page (console noise only).

---

## Post-Plan Cleanup
After code review passes:
1. Append execution trailers to this plan file.
2. Move this plan to `Documents/Plans/archive/`.
3. `.claude/memory/framework.md`: add a note with the tunnel's measured QUIC vs HTTP/2 throughput on the Mac Mini, next to the browser↔edge HTTP/3 line (`framework.md:67`, which stays true).
4. Changelog new version + GDD version bump; ISS-002 rewritten per the disposition above; `decisions.md` row for the tunnel protocol if HTTP/2 is kept; Roadmap checked for anything this changes.
5. Deferred.md: scan for entries whose trigger fired (gallery player, tunnel) and file any leftover with a trigger.
6. Commit all plan work.

## Workflow
Execute steps sequentially. Proceed to the next step without waiting for user approval. Only stop if blocked. If blocked, stop and report: what step, what was attempted, what failed.

## Review round 1


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
