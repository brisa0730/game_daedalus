# Conventions
**Last updated:** v0.01

Mirrors [CLAUDE.md](../../.claude/CLAUDE.md) § Coding Conventions and § Testing. Sections fill in as the code establishes them.

## Naming

## Project Format & Migrations

## Events

## Error Handling

## Performance Idioms

## Testing & Playtesting

- **Scenario tests** (planned): a small hand-built map + a scripted input sequence, stepped headlessly through `step()`, asserting positions and states tick by tick. One scenario per collision edge case and per ability gate.
- **Operator play-check** for movement feel, with the defaults tuned in `movement-prototype`.
