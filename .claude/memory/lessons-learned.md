# Lessons Learned

Each entry leads with a **bolded one-line rule**, then as much context (the why, the tell, the source issue/version) as the lesson needs. Full incident narratives live in `Documents/Operations/Changelog.md`; durable architectural decisions live in `decisions.md`. When a lesson recurs 3+ times, promote it into `CLAUDE.md`.

## Universal Rules
- **Do NOT over-implement beyond requested scope.** Stubs mean stubs.
- **"Computed/produced but never consumed/enforced" is a defect class: trace BOTH ends of every decision-influencing wire.** When adding a producer, grep for the consumer first. The most dangerous variant is a safety/validation check that fails OPEN on missing input.
- **A value that lives in more than one place (design table > data file > code default) drifts.** Keep one source of truth, or a fresh build silently behaves differently from the shipped one.
- **A silent no-op API is a fail-open wire; resolve the key first.** A lookup that quietly does nothing on an unknown id hides the bug; check the key exists and fail loud if not.
- **An operator's yes to a scope is not a yes to the content Claude filled it with.** In a plan or phase prompt, split "operator decided" from "Claude proposes, needs sign-off", and never let the second slide into the first. Every term written into the GDD as a decision must have been discussed; a definition Claude supplies to fill a gap is a proposal and is marked so.
- **When deferring a design decision, record its costs and trigger, never a preferred answer.** A preferred answer narrows the revisit before it begins.

## Testing & verification
- **A test runner that has never been observed FAILING is not a gate, and an exit code is not a result.** A run that executes nothing can exit 0. Assert the executed count is non-zero, and watch the suite go red on a deliberately broken assertion before trusting a green one.
- **A property test that never meets its precondition passes vacuously: count the cases it actually checked.** Assert the checked count (or the set of cases used) next to the property.
- **A test with a fallback branch can pass without ever running the path it is named for.** Probe which branch each input takes; pin an input per branch and assert each branch strictly.
- **An oracle that only asserts upper bounds doesn't prove a mechanic fired; assert the mechanic's effect too.** A feature that silently goes dark often makes things *faster* and passes every bound.
- **A test that fabricates a state the production path never produces will pin a defect as though it were a contract.** Assert through the real entry point.
- **A test that hand-arms its fixture proves the behaviour, never the authoring,** and its failure message must not claim otherwise.
- **A green suite says nothing about an artifact the tests cannot see, and "I edited the tool" is not "I ran it".** Run the generator / build and check its output.
- **Write event-ordering tests in the order events arrive live.** Applying events in a convenient order can hide a bug the live order exposes.
- **A test that fails "now and then" after data changes may be data-dependent, not flaky.** Compute the expectation from the state instead of a hard-coded moment.
- **Layout checks assert the position computed for the current size, not a range.** A range passes even when the layout never received the new size; look at the real pixels when a page screenshot can't be trusted.
- **Do not detect a condition by grepping for a message the tool never emits; check the STATE instead.**
- **A grep HIT is evidence even when its display is truncated.** A display limit is not a content limit.
- **Review fixes get the full test run (including e2e) before anything is measured or committed on top.** A "fail loud" fix can fire on a path the unit tests don't reach.
- **When a measurement disagrees with the record, prove which one moved.** Do not assume it was the code; the environment or the measurement may have changed.
- **Measure performance against a bare control before trusting a frame cap or an fps number.** Framework internals (a library's own ticker, vsync wake-ups) can cost more than your loop.
- **Stopping a background test run means confirming every process in it is gone.** Kill the runner itself, list again, and report only what the listing shows.
- **A planted failure must change the expectation only, never a value the code under test also reads.** Changing a shared constant moves both sides and the test still passes (scaffold: the PlayMode colour plant first changed `SpriteColor`, which also drew the sprite).
- **A compiler-warning gate only sees what was compiled in that run.** Unity prints warnings only when it builds the assembly; on a warm cache the gate checks nothing. Force the build and assert it happened before trusting "no warnings" (scaffold code review).

## Data & state
- **A save/project validator must not depend on tunable data.** Comparing stored values with current defaults turns every retune into a rejected (lost) save. Validate shape, invariants and content only; re-derive caches on load.
- **View state that accumulates across sim events must decay on render time, not on capped frame time.** Otherwise a hidden tab or a slow frame lets it pile up; a dt cap is for visual continuity only.
- **Check balance / tuning targets against each other by arithmetic before a tuning pass.** Targets can be mutually impossible; a back-of-envelope over the target table catches it before the work starts.

## Play-checks
- **Write the harsh clause into a play-check.** A "does it look right" check passes on anything not obviously wrong; say what must be absent.
- **A play-check whose correct answer is written in the GDD measures the operator's memory, not the build.** Ask for what they observe, not whether it matches.
- **When a phase's verification is play-checks only, say so per step.**
- **A deferred manual play-check decays against the code it verifies, and nothing is watching.** The phase that churns a check's subject must re-run it or restate it.

## Planning & documents
- **Never assign an index (phase number) to work that has not started.**
- **When a decision renames an identifier or moves a threshold, grep the WHOLE document for the old form before calling the edit done.** Stale copies survive in the checklists and Verify lines an executor actually works from.
- **Rewriting a step does not rewrite its summaries,** and the Completion Criteria copy is the commit gate, so it ships the drift. Re-read the gate lines after every edit.
- **A step that deletes a RANGE while promising to preserve something inside it is a defect: relocate first, then delete.**
- **Re-derive every line number you assert, at the moment you assert it.** A cited `file:NN` is a measurement, not a memory; re-derive ranges by grep at execution.
- **Never close a `Deferred.md` entry on a step that is planned but not yet done.**
- **The orientation file (`MEMORY.md`) must never carry content.** It is read before anything can correct it.
- **A handshake fold-in is new text with the same defect rate as any other: re-verify your own fix before handing it back.** Corrections are written fast under the authority of a finding and skip scrutiny; the residue concentrates in whatever was rewritten most.
- **`/scope` → `/new-phase` writes the phase prompt as a real file and asks approval by showing it; do NOT run it inside plan mode.** Plan mode blocks the write. Exit plan mode before invoking `/new-phase`.
- **When `/scope` routes to a plan, follow its plan path; don't also enter Claude Code's plan mode.** Plan mode can only write a private file, so it blocks `Documents/Plans/` and the handshake files and puts an approval prompt in front of the operator that `/scope` doesn't have (operator correction, 2026-10-21).

## Tooling / environment
- **Never put a PowerShell here-string (`@'…'@`) in a Bash tool call.** Bash parses it as a literal `@` + quoted string + `@`, silently corrupting the argument. For a multi-line commit message in Bash use `git commit -F <file>` or a POSIX heredoc; keep `@'…'@` for the PowerShell tool.
