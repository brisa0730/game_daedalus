# Conventions
**Last updated:** v0.07

Mirrors [CLAUDE.md](../../.claude/CLAUDE.md) § Coding Conventions and § Testing. Sections fill in as the code establishes them.

## Naming

## Authored Data & Upgrades

## Events

## Error Handling

## Performance Idioms

- `PlayerStepper.Step` and everything it calls allocate nothing: structs, `for` loops over arrays, no LINQ, closures, boxing or interface `foreach`. Guarded by `StepAllocationTests` (Test Framework `Is.Not.AllocatingGCMemory()`, since `GC.GetAllocatedBytesForCurrentThread` sees nothing in Unity's EditMode tests), with a planted-allocation sanity check.
- The sim has no static mutable state: only `const` and `static readonly` value types or strings, guarded by reflection in `SimPurityTests` (compiler-generated lambda caches excepted; lambdas in `Step` are banned by review). The purity scan also bans `System.Linq` in the sim.

## Testing & Playtesting

- **Scenario tests:** a small map written as ASCII rows (`AsciiLevel`: `.` none, `#` solid, `-` one-way, `C` climbable, `R` rope, `+` one-way + rope, `P` start) and a scripted input sequence, driven through the real `PlayerStepper.Step` by `PlayerScenario`, asserting positions and states tick by tick. Every precondition is asserted (e.g. "the jump fired"); breakpoint tests assert both sides. Run on both 6000.0.85f1 and 6000.6.7f1.
- **Gate tests** (`GateIntegrityTests`) pin the level-design breakpoints at defaults (ledges, gaps, corridors, chimneys, single-wall scaling) and name the defaults they assume, so a retune that moves a breakpoint reads as "gate moved", not "bug". **Matrix tests** (`AbilityMatrixTests`) run one map × several ability sets and check that each surface responds only to its ability.
- **Operator play-check** for movement feel, with the defaults tuned in `movement-prototype`.
