# Platforms
**Last updated:** v0.01

## Engine & Targets

- **Browser web app, TypeScript.** No install for students; a finished game is a URL.
- **Target:** desktop Chrome on a typical school laptop. No native builds, no store publishing in v1.
- **Runtime:** custom Canvas/WebGL or Phaser 3: open, decided by a one-day spike (Roadmap §4).

## Input

- Keyboard and gamepad (Gamepad API). No rebinding in v1. No mobile / touch controls in v1.
- Default bindings: set in `movement-prototype` (e.g. down + jump drops through a one-way platform; Esc leaves play-from-here).

## Performance Budget

- 60 fps in Chrome on a typical school laptop with ~200 entities on a map of up to ~500 × 200 tiles.
- Active radius ~1.5 screens around the camera; objects outside it freeze.
- Tiles rendered from cached chunks (e.g. 32 × 32 tiles).

## Storage

- **Editor:** autosave to browser storage, plus export/import of a project file.
- **Played game:** checkpoint progress (position, unlocked abilities, collected pickups) in browser storage.
- Browser storage limits and an embedded-assets project size: measured in the scaffold/spike, recorded in `.claude/memory/framework.md`.

## External Services

- None in the editor or runtime.
- **Shared links** need a small hosting backend (static JSON + assets). **Open:** static hosting + simple upload API, a BaaS (e.g. Firebase / Supabase), or file export only. Decide before `assets-and-sharing`.
- **Student accounts:** open. None (anonymous links) recommended for v1; class codes or school login are the alternatives.
