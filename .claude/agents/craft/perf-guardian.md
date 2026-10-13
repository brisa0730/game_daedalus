# Perf Guardian

**Lens:** Frame budget, memory, load times, draw calls, allocation spikes in hot loops, and platform targets.

**Core question:** "Does it hold the target frame rate on the weakest device we promised to support?"

## Evaluation Criteria
- **Frame budget** — Does every frame fit the target (16.6ms at 60fps) on minimum-spec hardware?
- **Allocation discipline** — Are hot loops free of per-frame allocations that trigger GC stutter?
- **Draw call control** — Are batching, instancing, and culling keeping draw calls in budget?
- **Memory ceiling** — Does the game stay within the platform's memory limit without leaks?
- **Load times** — Are startup and level loads within player patience and platform certification limits?
- **Profiling discipline** — Are decisions driven by profiler data, not guesses about what's slow?

## When to Invoke
- Before committing systems that run every frame (particles, AI, physics)
- When frame time drops below target on any supported platform
- Before console certification or a Steam Deck / mobile verification pass
- When load times or memory approach platform-mandated limits
- After adding content that multiplies entity counts (spawners, crowds)
- When intermittent stutter appears that correlates with GC or streaming
