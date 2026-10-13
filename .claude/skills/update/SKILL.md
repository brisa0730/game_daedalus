---
name: update
description: Check what code has changed since the last GDD version and update GDD files to reflect the current codebase state.
disable-model-invocation: false
model: opus
effort: high
argument-hint: "[scope hint, e.g. 'enemy AI rework' or 'new inventory system']"
---

# GDD Update: $ARGUMENTS

## Workflow

### Step 1 — Identify Changes

Determine what changed since the last GDD update:
- If inside an active conversation with recent edits, use those changes.
- Otherwise, compare against git history since the last GDD changelog date.

Collect a list of **meaningful** changes (skip formatting, renames, comment-only edits).

### Step 2 — Scan GDD for Stale References

Spawn an **Explore** agent to search all `Documents/GDD/*.md` files for references to the changed systems. For each change, check whether the GDD:
- References removed fields, classes, or settings
- Describes behavior that no longer matches the code
- Is missing documentation for newly added systems

### Step 3 — Present Findings

Show the user a summary table:

```
| GDD File | Line | Issue | Proposed Fix |
|----------|------|-------|--------------|
```

If nothing is stale, report "GDD is up to date" and stop.

### Step 4 — Apply Updates (after user confirms)

1. Read each target GDD file and the conventions file listed in `.claude/CLAUDE.md` GDD section before editing.
2. Edit only the stale sections — preserve surrounding structure.
3. Be precise: use specific numbers, enum values, class names.
4. Bump version in `GDD.md` (increment by 1).
5. Add a changelog entry to `Changelog.md` with date and summary.

### Scope Hint

If `$ARGUMENTS` is provided, focus the scan on that area. If empty, scan broadly.
