---
model: haiku
---

# Agent: codebase-scan

**Type:** Explore
**Purpose:** Find existing code related to incoming GDD changes — what exists, what's stubbed, what needs modification.

## Instructions

You will receive a list of systems/features from GDD changes. For each one:

1. Search the project's source directories for related classes, modules, functions, and TODOs.
2. Check `Documents/GDD/architecture.md` for relevant patterns and system ownership.
3. Identify dependencies — what existing systems will the new work touch?

## Search Strategy

- Glob for file names matching the feature (e.g., `*Auth*`, `*Router*`).
- Grep for related keywords, class names, route definitions, API endpoints.
- Read key files to understand current interfaces and extension points.

## Output Format

```
## Codebase Scan Results

### Existing Code
- **[FileName]** — what it does, how it relates to the GDD change

### Stubs / TODOs
- **[FileName:line]** — TODO or placeholder found

### Affected Systems
- **[SystemName]** — why it's affected, what interface/method needs changes

### Extension Points
- Where new code should plug in (events, interfaces, middleware, hooks)
```

Do not suggest implementation. Report what exists and where.
