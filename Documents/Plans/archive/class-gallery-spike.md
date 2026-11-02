# Plan: Class gallery spike

**Date:** 2026-11-02 | **Status:** complete

## Overview

**Where things stand:** We have decided to build a class website where students sign in, upload their games and play each other's. Before we build it, we don't know three things: how the website must send a Unity web game so it loads, where a game's saved progress lives in the browser, and how to stop an uploaded game from acting as the student who is signed in.

**What this plan changes:**
- One of our test levels is built as a web game in the different ways Unity offers.
- A small first version of the website runs on the Mac Mini, with one test login, and puts that game online for a short while.
- A fake "bad game" tries to act as the signed-in student, trick them, or spoil other games, and we record which setup stops it.
- The answers are written into the design, with one recommendation for you to decide.

**How you'll know it worked:** You sign in at the class address, play the test level in Chrome, and see a short table saying which setup loads, keeps saves and blocks the bad game.

**Not in this plan:** real student accounts, uploading, the teacher's page and the editor's Share button. Those are the next phase.

**Why a plan and not a patch:** it spans a Unity build, a new server, the tunnel and a design decision.

**Design choice:** three parts of the spike's server become the start of the real gallery server: sending game files with the right headers, the handoff that lets only signed-in users load a game, and the guard against requests from games. Everything else in it is test-only and is deleted when the spike ends.

---

## Source
- **Trigger:** Roadmap §1, Milestone 3 `class-gallery`: "Starts with a spike"; systems.md § Share, Safety: "how is settled by the `class-gallery` spike".
- **Related issues:** none
- **Scope:** a WebGL build of the trap bench in each compression mode, a minimal loopback-only Rust server with one test login, a temporary Cloudflare Tunnel, and recorded findings on compression, saves and isolation.
- **Depends on:** Milestone 2 `traps` (the trap bench); 6000.6.7f1 with WebGL Build Support on the Mac Mini (installed 2026-11-02); the `tools:` macOS patch (`Tools/unity_tests.py`, `Tools/perf_check.py`, `PerfCheckBuild.cs`), committed before Step 1.

## Questions the spike answers

1. **Compression.** For each of Brotli, Gzip and none (Decompression Fallback off, Data Caching off), what headers must our server send for Chrome to load the game, and does it still load unchanged through the Cloudflare Tunnel? Record each mode's download size and cold load time (fresh profile). Default recommendation: the smallest download that loads without Decompression Fallback.
2. **Saves.** Unity keys a WebGL build's `persistentDataPath` (and `PlayerPrefs`) in IndexedDB by the page URL up to its last `/`, ignoring the query string. Confirm that. Do saves survive a reload? Are two games on one host kept apart? Is a save lost when the same game is served at a new path? Does each isolation setup keep saves working?
3. **Isolation.** Which setup stops an uploaded game from doing any of the following?
   - Act as the signed-in user: read the session cookie; change anything through the gallery with the user's session (delete, upload, change password); read gallery pages; overwrite the session by setting its own `Domain=orcarin.com` cookie ("cookie tossing").
   - Trick the user: navigate the gallery tab to a fake sign-in page, with or without a click; open popups; frame a gallery page and overlay it to steal clicks; `postMessage` the gallery page.
   - Reach other games: read or wipe another game's saves; read its files through the shared gate; register a Service Worker.

   Setups:
   - **A. Games hostname:** games come from a sibling hostname, `suwon-play.orcarin.com`. It is one level under `orcarin.com`, so Cloudflare's free certificate covers it. The gallery page embeds the game in a plain iframe. Both hosts set `__Host-` cookies (`Secure`, `Path=/`, no `Domain`). The games host gates files with its own cookie, set from a signed token the gallery puts in the iframe's **query string**: the games host checks the token, sets the cookie and `302`s to the clean, stable `/g/<id>/`. The two hosts are the *same site*, so the gallery also rejects any state-changing request that isn't same-origin (below).
   - **A+sandbox:** as A, but the iframe carries `sandbox="allow-scripts allow-same-origin allow-pointer-lock"`, with no `allow-top-navigation*` and no `allow-popups`. The frame is cross-origin, so `allow-same-origin` only keeps the game's *own* origin: its IndexedDB and gate cookie still work.
   - **B. Sandboxed iframe, same host:** `sandbox="allow-scripts"` on the gallery's own host. Expected to break IndexedDB (saves) and the cookie gate on game files; the spike confirms or refutes this.
   - **C. No isolation (baseline):** the game is served from the gallery's own host, unsandboxed. This shows that the attack page's checks really work.

## File Manifest

| Action | File | Purpose |
|--------|------|---------|
| NEW | `Assets/Dev/Editor/GallerySpikeBuild.cs` | Builds the trap bench to WebGL into `TestResults/gallery-spike/<mode>/`, with the compression mode it is given, Decompression Fallback off, Data Caching off and the `DAEDALUS_GALLERY_SPIKE` extra scripting define. Restores the player settings and the standalone target afterwards, following PerfCheckBuild's pattern |
| NEW | `Assets/Dev/GallerySpike/SaveProbe.cs` | Dev-only. The whole file sits inside `#if DAEDALUS_GALLERY_SPIKE`, so editor and test compiles never see it. A `static class` with a `[RuntimeInitializeOnLoadMethod]` that increments a `PlayerPrefs` counter, saves, and logs the count and `Application.persistentDataPath` to the browser console. The trap bench scene is not changed |
| NEW | `Deploy/gallery/` (`Cargo.toml`, `Cargo.lock`, `src/`) | The server: Axum, bound to `127.0.0.1` only, routing by `Host`. **Kept for the gallery:** `files` (build files with the right headers), `play_token` (token → cookie handoff), `guard` (same-origin check, framing headers). **Spike-only, deleted at Post-Plan Cleanup:** the environment-variable login, the routes for setups B and C, and the test `delete` endpoint |
| NEW | `Deploy/gallery/spike/attack/index.html` | The "bad game", spike-only. Runs every check in Question 3 and writes each result to the page and the console |
| NEW | `Deploy/cloudflared/suwon.yml` | Tunnel ingress for `suwon.orcarin.com` and `suwon-play.orcarin.com` → the loopback port. No credentials: the credentials file stays in `~/.cloudflared/` |
| MODIFY | `.gitignore` | `Deploy/gallery/target/` |

## Steps

### Step 1: The spike build
- **NEW `SaveProbe.cs`, `GallerySpikeBuild.cs`** as in the manifest. Arguments: `-spikeCompression brotli|gzip|none`, `-spikeOutput DIR`; release (non-development) build.
- Build all three modes with 6000.6.7f1 (`-buildTarget WebGL`). Record each build's size and its zipped size, which inform the gallery's upload limit.
- **Verify:**
  - Each output has `index.html` and `Build/` files with the expected `.br` / `.gz` / plain extensions.
  - The run log shows no errors.
  - The project is back on the standalone target.

### Step 2: The spike server
- **NEW `Deploy/gallery/`** as in the manifest.
- **Headers on the games host:**
  - Types: `.wasm` → `application/wasm`; `.js` → `application/javascript`; `.data` → `application/octet-stream`. A `.br` file is sent with `Content-Encoding: br` and a `.gz` file with `Content-Encoding: gzip`, each under its inner file's type; files are never encoded twice.
  - On every encoded file: `Vary: Accept-Encoding` and `Cache-Control: no-transform`, so Cloudflare does not re-encode.
  - On every file: `X-Content-Type-Options: nosniff`. `Service-Worker-Allowed` is never sent. Game pages also carry `Referrer-Policy: no-referrer`.
- **Gallery guard:**
  - Every state-changing route accepts only `Sec-Fetch-Site: same-origin`. When that header is absent, it falls back to an `Origin` equal to the gallery's own origin. A missing `Origin`, an `Origin` of `null` and a sibling `Origin` are all rejected.
  - No GET route changes state.
  - Every gallery response carries `Content-Security-Policy: frame-ancestors 'self'` and `X-Frame-Options: SAMEORIGIN`.
  - The gallery page has no `message` listener.
- **Play token:** HMAC-SHA256 over the game id and an expiry 60 s ahead, with a key generated at start. It grants one game and carries no user identity.
- **Login:** the test ID comes from an environment variable. The password is generated at start, long and random, printed once and never written to disk. A failed login waits 1 s before answering.
- **Verify:**
  - `cargo test` passes. It covers the header map (types, encodings, `Vary`, never double-encoded), the token (expired, tampered, wrong game), and the guard (same-origin passes; missing `Origin`, `null` and sibling `Origin` are rejected; `Sec-Fetch-Site: same-site` is rejected).
  - `cargo clippy -- -D warnings` is clean.
  - `curl -I` against `127.0.0.1` with each `Host` header shows the expected headers, and a `403` without a cookie.

### Step 3: The tunnel (operator-approved)
- **Operator approval required first:** this creates a Cloudflare tunnel and two DNS records on `orcarin.com`. On approval, Claude runs `cloudflared tunnel create suwon` and `cloudflared tunnel route dns suwon suwon.orcarin.com` (and the same for `suwon-play.orcarin.com`), and writes `Deploy/cloudflared/suwon.yml`.
- The server and tunnel run in the foreground only while the spike is tested; no launchd job yet (that is the gallery phase's).
- **Verify:**
  - `curl -I https://suwon.orcarin.com/` returns the sign-in page.
  - Through the tunnel, a `.br` file comes back with `content-encoding: br` unchanged.
  - The existing `orcarin` and `octopus` tunnels still serve their hostnames.

### Step 4: Run the experiments in Chrome
- Drive headed desktop Chrome on the Mac Mini, so WebGL uses the GPU. The driver is a throwaway script in the scratchpad (not project tooling) that uses the remote debugging protocol. Each run gets its own `--user-data-dir` in the scratchpad, never the operator's profile.
- **Compression (through the tunnel):** for each mode, sign in, open the game page, wait for the trap bench to render, screenshot, and read the console (load errors and the probe's output). Record the cold load time on a fresh profile; record warm times separately.
- **Compression (loopback):** the same, directly on `127.0.0.1`. This covers the compression question only, and its Brotli result is recorded, not assumed, because Chrome advertises `br` only on secure contexts.
- **Saves (through the tunnel):** for each setup, using the best compression mode:
  - The probe's count before and after a reload.
  - The same build served under a second game id, to see whether two games share saves.
  - The same game served at a new path, to see whether its save is lost.
- **Isolation (through the tunnel):** run the attack page under setups A, A+sandbox, B and C. Record each check's result.
- **Verify:**
  - A results table (mode × setup → loads / download size / load time / saves persist / saves shared / save survives a new path / each attack check).
  - Screenshots in `TestResults/gallery-spike/`. Claude looks at every screenshot.

### Step 5: Findings and recommendation
- Fill § Findings below: the table and a recommendation.
- The recommendation covers the compression mode, the isolation setup, and the measured build sizes with a proposed upload limit, under Cloudflare's 100 MB request cap on the free plan.
- It lists the risks no setup fixes, for the operator to accept or not, such as games on one host reaching each other's saves.
- It states the URL rule the gallery must keep: a game's address never changes when it is re-uploaded, or its saves are lost.
- **Stop for the operator:** the isolation setup is the operator's decision, along with the second hostname if A or A+sandbox wins. Claude recommends.

### Step 6: Take the spike down
- Stop the server and the tunnel. The DNS records stay for the gallery; until it runs, they show Cloudflare's error page.
- **Verify:**
  - `curl -I https://suwon.orcarin.com/` no longer reaches our server.
  - No process listens on the spike's port.

### Step 7: Run Tests
- `python Tools/unity_tests.py`, both editors and both modes. The build script and the define-gated probe must leave every suite green with no warnings.
- `cargo test` in `Deploy/gallery/`.

### Step 8: Verify
- Walk the Completion Criteria.

### Step 9 (Final): Code Review
- Run the code-review agent on the build script, the probe and the crate.
- Fix any HIGH/MED issues.

---

## Findings

Measured 2026-11-02 on the Mac Mini: Chrome 157, headed, a fresh profile per run, through the `suwon` tunnel unless the row says loopback. Screenshots and build logs are in `TestResults/gallery-spike/`.

**Blocking side finding: ISS-001.** Every WebGL build draws nothing. URP's resource shaders are null in the player, and the same is true of the perf check, whose gate passed on empty frames. The game still runs, so everything below (loading, headers, saves, isolation) holds. Each "loads" means the save probe ran, not that a level was seen.

### Compression (trap bench, release, Decompression Fallback off, Data Caching off)

| Mode | Build folder | Zip | Downloaded | Cold load, tunnel (setup A) | Loopback |
|---|---|---|---|---|---|
| Brotli | 11.9 MB | 9.9 MB | 7.4 MB | 4.3–8.3 s (four runs) | loads, 0.5 s |
| Gzip | 13.6 MB | 13.1 MB | 10.2 MB | 8.1 s | loads, 0.5 s |
| None | 48.3 MB | 14.1 MB | 38.6 MB | 7.9 s | loads, 0.5 s |

- Cloudflare passes `content-encoding: br` and `gzip` through unchanged (over HTTP/3 on the gallery's games host), and doesn't compress the uncompressed build.
- Chrome accepted Brotli over loopback HTTP: `127.0.0.1` counts as a secure context.
- Each load logs one `net::ERR_ABORTED`, from Unity's loader; it is harmless.
- Mac Mini build times: a cold WebGL build took ~4 min; a warm one took 13–18 s.
- The build's "2 errors" are licensing and network noise ("Access token is unavailable", "Curl error 42") on a freshly set-up machine; the build succeeds.

### Saves

| Setup | Saves survive a reload | Second game at another address | Notes |
|---|---|---|---|
| A | yes (1 → 2) | separate (count 1, other key) | |
| A+sandbox | yes (1 → 2) | separate | |
| C | yes (1 → 2) | separate | |
| B | — | — | the game doesn't load; IndexedDB throws `SecurityError` in the opaque origin, so saves can't work |

Unity keys saves by the page's path: `/idbfs/<md5 of the URL up to its last '/'>`. **A game served at a new address loses its saves**, so the gallery must keep each game at the same address across re-uploads.

### Isolation (the attack page)

| Check | C (baseline) | A | A+sandbox | B |
|---|---|---|---|---|
| Read the session cookie | not visible (`HttpOnly`) | not visible | not visible | blocked |
| Read `whoami` / a gallery page | **read** | blocked (CORS) | blocked (CORS) | blocked |
| Delete through the gallery (fetch / form) | **accepted ×2** | refused by the guard | refused / form blocked by the sandbox | refused / form blocked |
| Toss a cookie onto the gallery's session | not shadowed | `__Host-` can't be tossed; `Domain=orcarin.com` cookie **set** | same as A | blocked |
| Read the gallery page around it | **read** | blocked | blocked | blocked |
| Frame a gallery page | allowed (same origin) | blocked (`frame-ancestors`) | blocked | blocked |
| Navigate the gallery tab to a fake sign-in, no click | — | **navigated** | blocked | — |
| …after a click | — | **navigated** | blocked | — |
| Open a popup, no click / after a click | — | blocked / **opened** | blocked / blocked | — |
| Read another game's files | **200** | 403 (per-game gate) | 403 | blocked |
| Read / write another game's saves | — | **readwrite** | **readwrite** | blocked |
| Register a Service Worker | registered (own path) | registered, scope `/g/attack/` only | same as A | blocked |

The guard's log for the runs: two deletes accepted (setup C) and five refused, from the sibling origin, `null`, `same-site` and `cross-site`.

### Recommendation

1. **Isolation: setup A+sandbox.** Games on `suwon-play.orcarin.com` in a frame with `sandbox="allow-scripts allow-same-origin allow-pointer-lock"`. It blocks everything that would let a game act as the student or trick them, and keeps saves working. Plain A lets a game swap the gallery tab for a fake sign-in page; B breaks saves.
2. **Compression: Brotli**, the smallest download (7.4 MB for the trap bench), with Decompression Fallback off. The gallery sends the headers in `files.rs`.
3. **Upload limit: 50 MB per zip.** The trap bench zips to 9.9 MB, and Cloudflare's free plan caps a request at 100 MB.
4. **Address rule:** a game keeps one address for life (for example its gallery id), re-uploads included, or its saves are lost.
5. **Two risks left for you to accept or not:**
   - **Games can reach each other's saves.** Every game on `suwon-play` shares one browser origin, so a game can read or wipe another game's saves. In a class-only gallery the harm is a lost checkpoint. The fix would be one hostname per game, a bigger build.
   - **A game can set cookies for all of `orcarin.com`.** That includes `game.orcarin.com` and `octopus.orcarin.com`. Our gallery is immune, because its cookies are `__Host-`. Those apps are safe only if their session cookies are `__Host-` or host-only and they ignore cookies they didn't set. The alternative is a separate domain for games, which costs a domain.

---

## Completion Criteria

- [ ] Three WebGL builds of the trap bench (Brotli, Gzip, none) built from the command line, sizes recorded.
- [ ] `cargo test` and `cargo clippy -- -D warnings` green in `Deploy/gallery/`; the server binds only to `127.0.0.1`.
- [ ] Every cell in the results table filled from a real Chrome run, with a screenshot of each run that loads.
- [ ] The attack page's checks *succeed* under setup C, which proves the page works, and the recommended setup blocks every check it claims to block. Any check it doesn't block is listed as a risk in § Findings.
- [ ] `python Tools/unity_tests.py` green on both editors and both modes, and `SaveProbe` never compiled outside the spike build.
- [ ] The spike's server and tunnel are stopped. No test password is committed (the password is never written to disk).
- [ ] The operator's isolation decision is recorded (Post-Plan Cleanup).

---

## Notes to Spec / Open Risks
- **Same-site games host:** `suwon-play.orcarin.com` is cross-origin but same-site with the gallery, so `SameSite` cookies do not stop requests from a game. Other `*.orcarin.com` hosts, including the public `game.orcarin.com`, can toss `Domain=orcarin.com` cookies. The `__Host-` prefix and the same-origin guard are required, not optional. A separate registrable domain for games would make them cross-site. It costs a domain and goes to the operator only if setup A's attack results leave a gap.
- **Games share one origin under A:** every game on `suwon-play` can reach the others' saves and cache. Per-game hostnames would fix it at the cost of a certificate for deeper subdomains; the spike measures it, and the operator weighs it.
- **Public exposure:** the spike is reachable on the internet behind one test login while Step 4 runs. The password is long, random, held only in memory, and failed logins are slowed.

---

## Post-Plan Cleanup
After code review passes:
1. Append execution trailers to this plan file (Approval / Execution / Verification).
2. Move this plan to `Documents/Plans/archive/`.
3. Delete the spike-only parts of `Deploy/gallery/` (the environment-variable login, the setup B and C routes, the test `delete` endpoint, `spike/attack/`). Keep `files`, `play_token` and `guard`.
4. Record the outcomes:
   - systems.md § Share: the Safety line names the chosen setup.
   - platforms.md: hosting (hostnames, headers, upload limit, the game-URL rule).
   - `decisions.md`: ADR rows for compression and isolation.
   - `framework.md` § WebGL: the Mac Mini build times and the Chrome / Cloudflare gotchas found.
   - Roadmap Milestone 3 text: the spike is done, and the Rust server is developed on the Mac Mini, not the Windows laptop.
   - CLAUDE.md: Testing gets a `cargo test` row for `Deploy/`; Project Config gets the macOS editor path.
   - A Changelog entry and a GDD version bump.
5. Deferred.md: file anything the spike left on purpose, each with a trigger.
6. Commit all plan work.

## Workflow
Execute steps sequentially. Proceed to the next step without waiting for user approval, except Step 3 (tunnel and DNS) and Step 5 (the isolation decision). If blocked, stop and report: what step, what was attempted, what failed.

---

**Approval:** User confirmed 2026-11-02 (plan, tunnel and DNS); isolation and the other Step 5 decisions confirmed 2026-11-02, all as recommended.
**Execution:** COMPLETE 2026-11-02.
**Verification:** three WebGL builds; `cargo test` (8) and clippy clean; every Findings cell from a real Chrome run through the tunnel; the attack page's checks succeed under C and A+sandbox blocks all but the two accepted risks; the server and tunnel stopped (`suwon.orcarin.com` → Cloudflare 530, nothing on :8470); both Unity suites green on both editors.
**Version note:** shipped as GDD v0.14. Deviations: setup B never loaded the game, because its "ungated" route still sat behind the sign-in check. Its verdict comes from the attack page instead: IndexedDB throws in the opaque origin. The second-address save test ran for Brotli only. The spike's password was printed to a temporary task log, not kept only in memory; it never reached the repo. The screenshots are blank because of ISS-001, filed from this spike.
