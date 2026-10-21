# Platforms
**Last updated:** v0.03

## Engine & Targets

- **Unity 6 package** `com.orcapafea.daedalus`, C#. Minimum editor **6000.0** (declared in the package's `package.json`); developed on **6000.6.7f1**; every test also runs on **6000.0.85f1**.
- **Students** work in the Unity Editor on their school laptops (the operator confirmed the hardware). Each machine needs Unity Hub, a Unity 6 editor with **WebGL Build Support**, and a Unity account with a Personal or education license.
- **Played games** are WebGL builds in desktop Chrome. No native builds, no store publishing in v1.

## Distribution

- **Students:** install from **OpenUPM**: a scoped registry added once per project, then the package installs and updates from Package Manager (an Update button showing the version). No Git needed on student machines.
- **Development:** the git URL `https://github.com/brisa0730/game_daedalus.git?path=/Packages/com.orcapafea.daedalus`.
- **Releases** are semver git tags on the public GitHub repo; OpenUPM builds each tag.
- **Update safety:** an update must never break a saved student project (CLAUDE.md Architecture rule 4). Students update when the teacher says so, not mid-project on their own.
- The **starter** (a scene, element prefabs, presets, the default art pack) imports from Package Manager as a package sample.

## Input

- Keyboard and gamepad through the Input System package. No rebinding in v1. No mobile / touch controls in v1.
- Default bindings: set in `movement-prototype` (e.g. down + jump drops through a one-way platform). The play-from-here shortcut: set in `editor-tools`.

## Performance Budget

- 60 fps on a typical school laptop, both in the WebGL build in Chrome and in the Editor's Play mode, with ~200 entities on a map of up to ~500 × 200 tiles.
- Active radius ~1.5 screens around the camera; objects outside it freeze.
- Tilemaps render in Chunk mode.

## Storage

- **Student work:** the Unity project folder, saved by Unity (Ctrl+S) plus the editor tools' scene autosave. Moving a project between machines means copying the folder without `Library/`; the classroom procedure is set in `classroom-test`.
- **Played game:** checkpoint progress (position, unlocked abilities, collected pickups) through `PlayerPrefs`, which a WebGL build keeps in the browser.

## External Services

- None in the package.
- **Sharing WebGL builds** needs hosting. **Open** (Roadmap §4). Decide before `assets-and-sharing`.
- **Student accounts:** none of ours in v1. The Unity account each student needs is Unity's.
