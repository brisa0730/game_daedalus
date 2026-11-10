# Conventions
**Last updated:** v0.26

Mirrors [CLAUDE.md](../../.claude/CLAUDE.md) § Coding Conventions and § Testing. Sections fill in as the code establishes them.

## Naming

## Authored Data & Upgrades

- Every component and asset of ours has a hidden `dataVersion` (starts at 1) for upgrade steps.
- Enum values are explicit and never renumbered (`TileCollisionKind`); serialized field names are fixed from the first release.

## Events

## Error Handling

- Play-start problems are `Debug.LogWarning(text, context)` in plain words, logged once, with the object to fix as context (clicking selects it). Every text lives in `PlayerWarnings` (inspector help texts in `HelpTexts`, shared by the inspector and play start so they can't disagree); a warning says what is wrong and what to do and never names code. Nothing is removed or fixed automatically.

## Performance Idioms

- `LevelStepper.Step` and `PlayerStepper.Step` and everything they call allocate nothing: structs, `for` loops over arrays, no LINQ, closures, boxing or interface `foreach`. Guarded by `StepAllocationTests` (Test Framework `Is.Not.AllocatingGCMemory()`, since `GC.GetAllocatedBytesForCurrentThread` sees nothing in Unity's EditMode tests), with a planted-allocation sanity check.
- **The frame loop** (`Player.Update` → `RunFrame` and everything it calls) allocates nothing and has no LINQ, closures, boxing, `GetComponent`, `Find` or logging; components are cached in `Awake`, play-start work happens in `Start`, warnings only at play start; HUD numbers come from strings cached at start. Guarded by `PlayerAllocationTests` (the same constraint around direct `RunFrame` calls, with a planted-allocation check).
- The sim has no static mutable state: only `const` and `static readonly` value types or strings, guarded by reflection in `SimPurityTests` (compiler-generated lambda caches excepted; lambdas in `Step` are banned by review). The purity scan also bans `System.Linq` in the sim.

## Testing & Playtesting

- **Scenario tests:** a small map written as ASCII rows (`AsciiLevel`: `.` none, `#` solid, `-` one-way, `C` climbable, `R` rope, `+` one-way + rope, `^` hazard, `P` start; level scenarios with traps, checkpoints and exits go through `LevelScenario`) and a scripted input sequence, driven through the real `PlayerStepper.Step` by `PlayerScenario`, asserting positions and states tick by tick. Every precondition is asserted (e.g. "the jump fired"); breakpoint tests assert both sides. Run on both 6000.0.85f1 and 6000.6.8f1.
- **Gate tests** (`GateIntegrityTests`) pin the level-design breakpoints at defaults (ledges, gaps, corridors, chimneys, single-wall scaling) and name the defaults they assume, so a retune that moves a breakpoint reads as "gate moved", not "bug". **Matrix tests** (`AbilityMatrixTests`) run one map × several ability sets and check that each surface responds only to its ability.
- **PlayMode tests** use the Input System's `InputTestFixture` by composition, send whole keyboard states, and run at `Time.captureFramerate = 60` (one tick per frame). Their levels are built in code (`TestLevel`, the `AsciiLevel` symbols) so both editors run them; screenshots go to `TestResults/<unity version>-movement-*.png` and `-traps-*.png`. Stage tests use real scene files written by a `PrebuildSetup` (`StageScenes`), additively, because a Single scene there would replace the test runner's bootstrap scene; each marks the test runner `DontDestroyOnLoad` so an Exit's Single load doesn't end the run (`-stages-*.png`).
- **The dev scenes** are generated (`MovementTestSceneBuilder` → `Assets/Dev/MovementTest/`, `TrapTestSceneBuilder` → the trap bench `Assets/Dev/TrapTest/`: the Phase 3 sections, then a Falling Ceiling, a Sliding Floor, a Crusher, a Fall Floor, fast spikes in a corridor, a Runaway Exit and adversarial fixtures) and verified by their self-checks and pictures; never hand-edited.
- **Operator play-check** for movement feel; the movement defaults passed it unchanged (Phase 2), and the death, respawn and trap numbers too (Phases 3 and 4).
