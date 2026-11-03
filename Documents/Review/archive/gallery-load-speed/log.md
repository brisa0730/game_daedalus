# Handshake log: gallery-load-speed

## Opened 2026-11-03T23:43:39+09:00 (Author)
Doc: `Documents/Plans/gallery-load-speed.md`. Proposes: (1) test the class gallery's Cloudflare tunnel on HTTP/2 against QUIC in four alternating rounds and keep HTTP/2 only if the median 7.3 MB wasm download halves; (2) a Korean loading label + progress bar on the games host's player page, driven by Unity's progress callback. Diagnosis: the "black frame" (ISS-002) is a slow tunnel (~50–200 KB/s; Orcarin's QUIC tunnel on the same Mac shows the same ~160 KB/s ceiling), not a broken game. Already critic-reviewed; findings folded in (see the plan's Notes). Nothing is built yet.

## Round 1 — approved (2026-11-03T23:47:51+09:00, Author close fold)
Verdict `approved`, 0 blocking, 5 non-blocking, 0 recommendations. All folded on close; each re-verified:
- **F1 accepted.** Re-derived from `gallery.css:36` (`main` padding 24px 16px) and `gallery.css:120` (`.frame` min(100%, (100dvh−220px)·16/9)): at 390 × 844, 390−32 = 358 px wide, 358·9/16 = 201.4 px tall; the height term is 1109 px so width wins; no narrow-width override of `main` padding (`@media (max-width:700px)` only touches tables). Step 3 now says 358 px (bar ≈ 286 px at 80vw inside the frame); Step 5 says 358 × ~201.
- **F2 accepted.** Overview "How you'll know" made conditional: the bar always; seconds-not-minutes only if HTTP/2 wins.
- **F3 accepted** (the second remedy). Keep rule now: HTTP/2 wins both paired rounds, overall median ≥ 25 % lower, Chrome-start guard kept; reason recorded (HTTP/2 has no cost a 2× bar protects against; TCP 7844 egress is proven by the hosts check). The Notes' critic line that named the old half-median rule was updated to match.
- **F4 accepted.** Confirmed: all § Source timings ran on the Mac Mini (curl and CDP Chrome), a hairpin. Source now says so; Completion Criteria adds one load from another network (office PC or a phone off Wi-Fi). The operator independently raised testing from the office PC in-session.
- **F5 accepted.** Step 5 now confirms the throttle reached the frame (wasm duration ≥ size ÷ rate in CDP Network events; else throttle the frame target too).
Post-fold check: grep found no remaining "219", "350 px" or "at most half"; new text present at Steps 2, 3, 5, Source and Completion Criteria.

## Closed 2026-11-03T23:47:51+09:00
Plan approved and folded. Nothing was built: the plan is a proposal; execution (tunnel experiment, loading bar, deploy) is separate work.
