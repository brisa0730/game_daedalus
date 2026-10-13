---
model: opus
---

# Agent: code-review

**Type:** Explore
**Purpose:** Review recently changed code for correctness, convention compliance, and GDD alignment.

## Instructions

1. Identify which files were changed (provided by caller or via recent context).
2. Read each changed file fully.
3. Read `Documents/Operations/Issues.md` — check if changes relate to any active issues.
4. **Read the phase prompt** (provided by caller) including the **Notes to GDD** section. Any feature explicitly listed as deferred or out-of-scope in the prompt is NOT a review issue — do not flag it. Only flag deviations from what the prompt *intended* to implement.
5. Check against every rule below.

### Checklist

**Code Integrity:**
- [ ] No references to removed, renamed, or non-existent fields, methods, or classes
- [ ] No missing imports or module resolution issues
- [ ] No broken method signatures (wrong parameter count/type after refactors)
- [ ] No null/undefined dereferences on paths that can realistically be null
- [ ] No off-by-one errors in array/list indexing
- [ ] No dead code paths (unreachable branches, unused variables)
- [ ] No race conditions or order-of-initialization issues between systems

**Architecture (from CLAUDE.md):**
- [ ] Separation of concerns: Input -> Logic -> Presentation boundaries respected
- [ ] System ownership: no system owned by two services/managers
- [ ] Inter-system communication via project's designated pattern only, no tight coupling
- [ ] No N+1 queries or unbounded data fetches
- [ ] Error handling at system boundaries (external APIs, user input), not internally

**Security:**
- [ ] No SQL injection, XSS, or command injection vectors
- [ ] User input validated and sanitized at entry points
- [ ] No secrets or credentials hardcoded
- [ ] Auth/authz checks in place where required

**Testing:**
- [ ] New/changed behavior has corresponding tests
- [ ] Tests are meaningful (verify behavior, not just that code runs)
- [ ] Test suite passes — run the test command from CLAUDE.md `Testing` section
- [ ] No tests disabled or skipped without a documented reason

**Dependencies:**
- [ ] No unused dependencies added
- [ ] No dependency added when a standard library solution exists
- [ ] Lock file updated if dependencies changed
- [ ] No pinned versions with known security vulnerabilities
- [ ] Dependencies use correct scope (dev vs prod)

**Coding Conventions (check against the actual rules in CLAUDE.md — the items below are the recurring ones; add project-specific rules from CLAUDE.md as they're defined):**
- [ ] Import boundaries respected — any library/subsystem CLAUDE.md restricts to a single module is not imported elsewhere
- [ ] No module imports from another module's internals (only interfaces + shared infrastructure)
- [ ] Structured logging used per CLAUDE.md (no bare prints / raw stdlib logging); mandatory log fields present
- [ ] Error handling discipline: expected unavailability → typed `Unavailable` return; unexpected → raise with ERROR log
- [ ] Typed dataclasses/structs for all inter-module data — no raw dicts crossing boundaries
- [ ] Constructor injection — no global singletons
- [ ] Naming + import-ordering conventions from CLAUDE.md followed
- [ ] Wiring integrity: any new producer has its consumer wired + tested in the same change (no "computed but never consumed"; no safety check that fails OPEN on missing input)

**GDD Alignment:**
- [ ] Implementation matches the GDD specifications (mechanics, data types, behaviors)
- [ ] No features added beyond what the GDD specifies
- [ ] Naming matches GDD terminology

**Read-Only Boundaries (from CLAUDE.md):**
- [ ] Any separate build target (dedicated server, tools build) is not imported by the main game code
- [ ] Generated / vendored directories (build output, engine import caches, `Library/`, `.godot/`, third-party asset packs) untouched
- [ ] Main game code does not couple itself to a tooling/editor-only script's internals

## Output Format

```
## Code Review: [Phase/Step Name]

### Result: [PASS / FAIL / PASS WITH FLAGS]

### Issues Found
- **[SEVERITY: HIGH/MED/LOW]** [FileName:line] — description of the issue

### Verified
- List of checks that passed cleanly

### Recommendations
- Optional suggestions (not blockers)
```
