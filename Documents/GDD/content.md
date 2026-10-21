# Content, Readability & Accessibility
**Last updated:** v0.03

## Art Direction

- A **default art pack** ships in the starter sample: tiles, the player, a few enemies, props, so students can build immediately. To start: a licensed **CC0** pack, mapped to the auto-tile layouts.
- Tile size: **16 px** (pixel art, smaller assets).
- Auto-tiling (systems.md § Tile System) is how simple student art ends up looking clean; blank template PNGs per layout let students draw terrain straight into the right slots.
- Students can import their own tilesets and sprite sheets (systems.md § Asset Import). Good default art and animation fallbacks are the mitigation for sprite-import confusion.

## Narrative

None in v1: no dialogue, NPCs or story systems.

## Audio

**Fixed default sound effects** in v1. Students can't import or edit sound.

## Readability

- Two readers: the **student** in the Unity Editor (can they find the tool, understand the setting?) and the **classmate** playing the result.
- Every student-facing setting is an Inspector slider, toggle or dropdown with a good default, a plain label and a tooltip (CLAUDE.md Architecture rule 8).
- Feedback in play: hit flash, knockback, i-frame flicker, hit-stop.

## Accessibility

The Unity Editor's own accessibility is Unity's. To be set during `editor-tools`: plain-language labels and tooltips, a keyboard shortcut for play-from-here, and colour-independent cues for validation warnings.

## Student Content

Students share games as WebGL builds. Moderation / takedown for shared student content is a pre-launch gate (Roadmap §2), shaped by the hosting decision.
