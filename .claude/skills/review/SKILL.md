---
name: review
description: Review recently changed code for architecture compliance, convention adherence, and GDD alignment. Use after completing an implementation step.
disable-model-invocation: false
model: sonnet
effort: medium
argument-hint: "[step-name or file-list]"
---

# Code Review: $ARGUMENTS

## Workflow

**Agent — code-review** (Explore):
Read the directive at `.claude/agents/workflow/code-review.md` and execute it.

Target files: $ARGUMENTS
If no specific files are given, review all files changed during the current implementation step.

## After Review

1. Show the review result (PASS / FAIL / PASS WITH FLAGS)
2. If issues are found, list them with severity and file locations
3. Do NOT auto-fix — present findings and wait for user decision
