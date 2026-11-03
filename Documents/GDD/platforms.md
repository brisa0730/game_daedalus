# Platforms
**Last updated:** v0.19

## Engine & Targets

- **Unity 6 package** `com.orcapafea.daedalus`, C#. Minimum editor **6000.0** (declared in the package's `package.json`); developed on **6000.6.7f1**; every test also runs on **6000.0.85f1**.
- **Students** work in the Unity Editor on their school laptops (the operator confirmed the hardware). Each machine needs Unity Hub, a Unity 6 editor with **WebGL Build Support**, and a Unity account with a Personal or education license.
- **Played games** are WebGL builds in desktop Chrome. No native builds, no store publishing in v1.

## Distribution

- **Students:** install from **OpenUPM**: a scoped registry added once per project, then the package installs and updates from Package Manager (an Update button showing the version). No Git needed on student machines.
- **Development:** the git URL `https://github.com/brisa0730/game_daedalus.git?path=/Packages/com.orcapafea.daedalus`.
- **Releases** are semver git tags on the public GitHub repo; OpenUPM builds each tag.
- **Update safety:** an update must never break a saved student project (CLAUDE.md Architecture rule 4). Students update when the teacher says so, not mid-project on their own.
- The **starter** (a scene, example prefabs, the default art pack) imports from Package Manager as a package sample.

## Input

- Keyboard and gamepad through the Input System package. No rebinding in v1. No mobile / touch controls in v1.
- Default bindings (v0.08): move with the arrows, WASD, the gamepad d-pad or the left stick (dead zone 0.5; the stick's down counts only when it outweighs sideways, so a running diagonal never drops through a platform); jump with Space or gamepad South; Down + Jump drops through a one-way; **Up never jumps**. A press and release within one frame is a tap. Actions are built in code, never the project-wide actions asset. Input System 1.11.2 minimum.
- The play-from-here shortcut: set in `editor-tools`.

## Performance Budget

- 60 fps on a typical school laptop, both in the WebGL build in Chrome and in the Editor's Play mode, with ~200 entities on a map of up to ~500 × 200 tiles.
- Active radius ~1.5 screens around the camera; objects outside it freeze.
- Tilemaps render in Chunk mode.

## Storage

- **Student work:** the Unity project folder, saved by Unity (Ctrl+S) plus the editor tools' scene autosave. Moving a project between machines means copying the folder without `Library/`; the classroom procedure is set in `classroom-test`.
- **Played game:** checkpoint progress (position, unlocked abilities, collected pickups) through `PlayerPrefs`, which a WebGL build keeps in the browser's IndexedDB under the page's address (`/idbfs/<hash of the URL up to its last />`): saves survive a reload, stay apart between games, and are lost if a game moves to a new address.

## External Services

- None in the package: the editor and the runtime never go online. Share my game only writes a zip; the student uploads it in the browser.
- **Class gallery** at `suwon.orcarin.com` (live since v0.16): our web app on the operator's Mac Mini, reachable only through a Cloudflare Tunnel. Server files live in `Deploy/`. Its pages load only their own files (CSP `'self'`): the heading font, Galmuri11 Bold (SIL OFL 1.1, license beside it), is built into the binary and served with a long-lived cache under a path that carries its release.
  - **Server:** one Rust binary, [Deploy/gallery/](../../Deploy/gallery/) (Axum, SQLite in one file): `gallery serve` on `127.0.0.1:8470`, routing by `Host` (only the two public hostnames; anything else is an empty 404). `gallery teacher create|reset` (operator only, in Terminal) and `gallery backup`.
  - **Data folder:** `~/Library/Application Support/daedalus-gallery/` (`gallery.db`, `games/<id>/v<n>/`, `staging/`). An upload unpacks into a new version folder, then one database update switches the game to it; game ids are random and never reused.
  - **launchd:** three user LaunchAgents in [Deploy/launchd/](../../Deploy/launchd/): the server, the `suwon` tunnel, and a 03:30 backup (`VACUUM INTO`, 7 copies, plus a games mirror that keeps gone folders 7 days, in `~/Library/Application Support/daedalus-gallery-backups/`; proposal). The backup restores any of the last 7 nights (steps in `backup.rs`) but sits on the same disk.
  - **Availability:** user LaunchAgents run once the operator has logged in; FileVault is on and auto-login off, so after a reboot or power cut the gallery waits for the operator's login.
  - **Tunnel config:** the tunnel job reads `~/.cloudflared/suwon.yml`, a copy of [Deploy/cloudflared/suwon.yml](../../Deploy/cloudflared/suwon.yml) installed at deploy (a launchd job may not read `~/Documents`; a `git pull` must not change the live tunnel).
  - **No secrets:** the play key is generated per process, so a restart ends open games' file access and reopening from the gallery fixes it. Sessions live in SQLite: browser-session cookie, 1 h idle, 12 h absolute (proposal); gate cookie 2 h (proposal). Sign-out can't clear the games host's cookies (another host), so a game opened on a browser stays playable from its history for up to 2 h; accepted.
  - **Access (v0.20):** every gallery route has one rule in one table (`app.rs`): public (static files, sign-in), **open** (the list, screenshots and play view: anyone, with the signed-in header when a session exists), signed in (upload, remove, password, sign-out) or teacher. The 404 fallback reads the session too, so a signed-in student's mistyped address keeps their header. The users table holds the public nickname (`display_name`) and a teacher-only `real_name` (migration `0002_real_name`; a binary from before it refuses to open a database it has run on, so rolling back means the older binary plus a backup from before).
  - **Headers:** every gallery page: `Content-Security-Policy: default-src 'self'` with `frame-src` the games host, `frame-ancestors 'self'`, `form-action 'self'`; `X-Frame-Options: SAMEORIGIN`; `no-store`; `X-Robots-Tag: noindex, nofollow` (on the games host too, since v0.20; a header, not `robots.txt`, which would hide the noindex from crawlers). Screenshots are served from the gallery as `image/png` with `nosniff` and `CSP: sandbox`, cached by their `?v=<version>` address.
- **Games host:** games are served from `suwon-play.orcarin.com` (one level under `orcarin.com`, covered by Cloudflare's free certificate) and shown in a frame with `sandbox="allow-scripts allow-same-origin allow-pointer-lock"` and `allow="fullscreen; gamepad; autoplay"`. A 60 s signed grant in the frame's address becomes a per-game `__Host-` cookie on the games host; `/enter` works only as a frame load (`Sec-Fetch-Dest: iframe`, same-site). The gallery's own cookie is `__Host-` too, and the gallery refuses every state-changing request that isn't same-origin and every framing by another site. The games host serves its **own player page** (the zip's `index.html` is never used) and only the four build files stored for the live version. Its CSP: `default-src 'none'`, `script-src 'self' blob: 'wasm-unsafe-eval'`, `connect-src`/`img-src`/`media-src 'self' blob: data:`, `form-action 'none'`, `frame-ancestors` the gallery, plus `sandbox allow-scripts allow-same-origin allow-pointer-lock` on its HTML so a page opened outside the frame is sandboxed too; Unity needed no relaxation (proposal). Cloudflare's Web Analytics beacon is blocked by both CSPs (console noise). Settled by the spike ([plan](../Plans/archive/class-gallery-spike.md)) and Phase 5.
- **Builds:** Brotli compression, Decompression Fallback off; the server sends `Content-Encoding` with each `.br` file and `Cache-Control: private, no-cache, no-transform`, so Cloudflare passes files through and never caches a gated one. Each build file carries `ETag: "<id>-v<version>"` (a reload costs a 304; a re-upload changes it) and `Accept-Ranges: none`; a range request always gets the whole file (ISS-002 safeguard). The trap bench downloads 7.4 MB and zips to 9.9 MB; uploads are capped at 50,000,000 bytes (Cloudflare's free plan caps a request at 100 MB).
- **Accounts:** the class gallery's own, needed only to upload and manage (browsing and playing are public since v0.20), one ID + password per student plus the teacher's, created by the teacher (systems.md § Share). Passwords are Argon2id (64 MiB × 3, ~66 ms on the Mac Mini, 2 at once); only existing IDs are ever hashed. The Unity account each student needs is Unity's.

## Share zip

The one file Daedalus → Share my game makes and the class gallery accepts (checked by [share_zip.rs](../../Deploy/gallery/src/share_zip.rs)). Share my game produces exactly this (v0.17): its build is always named `game`, and every entry is Deflated (Unity's zip writer never Stores; the contract allows both).
- **At the zip's root** (no wrapper folder): `daedalus.json`: UTF-8 JSON `{"format": 1, "title": "<text>"}`, unknown keys ignored, at most 4 KB; `screenshot.png`: a PNG, at most 1,000,000 bytes and 1280 × 720; `Build/<name>.loader.js`, `Build/<name>.data.br`, `Build/<name>.framework.js.br`, `Build/<name>.wasm.br`: exactly one of each, one shared `<name>` of `[A-Za-z0-9_-]{1,64}` (Unity names it after the output folder), Brotli only.
- Entries Stored or Deflated, no duplicate names, at most 256 entries, at most 120 MB unpacked, at most 50,000,000 bytes zipped.
- Anything else (`index.html`, `TemplateData/`, `StreamingAssets/`, other files) is ignored: never unpacked, never served.
- A symlink, an absolute path, a `..` part, a backslash or a drive-letter name anywhere rejects the whole zip.
- The title is cleaned (control and bidi characters removed, whitespace collapsed, at most 40 characters).
