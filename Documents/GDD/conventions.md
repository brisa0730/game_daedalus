# Conventions
**Last updated:** v0.03

Mirrors [CLAUDE.md](../../.claude/CLAUDE.md) § Coding Conventions and § Testing. Sections fill in as the code establishes them.

## Naming

## Authored Data & Upgrades

## Events

## Error Handling

## Performance Idioms

## Testing & Playtesting

- **Scenario tests** (planned): a small hand-built map + a scripted input sequence, stepped through `Step()` in EditMode with no scene, asserting positions and states tick by tick. One scenario per collision edge case and per ability gate. Run on both 6000.0.85f1 and 6000.6.7f1.
- **Operator play-check** for movement feel, with the defaults tuned in `movement-prototype`.
