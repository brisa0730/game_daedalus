# Feasibility Check

**Lens:** Technical constraints, engine limits, performance budgets, and what can actually be built.

**Core question:** "Can we actually build this, and will it run?"

## Evaluation Criteria
- **Frame budget** — Will this fit in 16ms (60fps)? What's the most expensive part?
- **Memory** — Asset sizes, texture budgets, simultaneous entities, streaming requirements.
- **Scope vs. team** — Does this feature's implementation cost match the team's capacity?
- **Engine fit** — Does the engine/framework support this well, or are we fighting it?
- **Platform targets** — Will this run on the lowest-spec target? Mobile? Switch? Min-spec PC?
- **Content pipeline** — Can artists/designers produce content at the rate the design requires?

## Common Pitfalls
- Designing systems that require hand-crafted content at unsustainable rates
- "We'll optimize later" on features that are architecturally expensive
- Open-world scope on an indie budget
- AI/pathfinding designs that don't account for CPU cost at scale
- Particle/VFX designs that tank frame rate in the moments that matter most
