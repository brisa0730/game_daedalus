#!/usr/bin/env python3
"""PreToolUse hook: block git commit titles that use disallowed prefix patterns.

Reads the hook JSON on stdin (`tool_input.command`), extracts the first `-m`
message's title line, and exits 2 (blocking) if the title leads with an
`ISS-NNN:` or `[phaseNN]` prefix. Pure stdlib so it runs under bare `python3`.

Convention source: .claude/memory/feedback.md ("Commit title convention") +
CLAUDE.md ("Commit titles"). Stays a *denylist* of the two mechanically-decidable
anti-patterns — the full "version commits must lead with vX.YY:" rule needs commit
intent a hook can't see.

If the commit convention changes, adjust the two regexes below (or delete this
hook and its wiring in settings.json).
"""
import json
import re
import sys

CONVENTION = (
    "Commit-title convention (memory/feedback.md):\n"
    "  - Version-bumping commit: `vX.YY: <subject> - <desc>` where "
    "<subject> is `Phase NN` / `ISS-NNN` / a short topic.\n"
    "  - Non-version commit: area prefix + colon "
    "(`memory:`, `docs:`, `Issues.md:`, `Deferred.md:`, `roadmap:`).\n"
    "`ISS-NNN` is the SUBJECT after the version prefix, never the prefix itself."
)


def main() -> int:
    try:
        data = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return 0  # unparseable stdin — don't block

    cmd = (data.get("tool_input") or {}).get("command", "")
    if not re.search(r"\bgit\s+commit\b", cmd):
        return 0  # not a commit

    # First -m/-am/--message "..." value; title is its first line.
    m = re.search(r"(?:--message|-[a-zA-Z]*m)\s+(['\"])(.*?)\1", cmd, re.DOTALL)
    if not m:
        return 0  # heredoc / -F file / editor — nothing to validate
    parts = m.group(2).splitlines()
    title = parts[0].strip() if parts else ""
    if not title:
        return 0

    if re.match(r"^ISS-\d+\s*:", title):
        print(
            "BLOCKED: commit title leads with `ISS-NNN:` — disallowed.\n"
            f"  Title: {title!r}\n"
            "  Use `vX.YY: ISS-NNN - ...` (version commit) or an area prefix "
            "(`memory:`, `Issues.md:`).\n\n" + CONVENTION,
            file=sys.stderr,
        )
        return 2

    if re.match(r"^\[phase", title, re.IGNORECASE):
        print(
            "BLOCKED: commit title uses a `[phaseNN]` bracket prefix — disallowed.\n"
            f"  Title: {title!r}\n"
            "  Use `vX.YY: Phase NN - ...` (version commit) or `Phase NN: ...` "
            "(non-version).\n\n" + CONVENTION,
            file=sys.stderr,
        )
        return 2

    return 0


if __name__ == "__main__":
    sys.exit(main())
