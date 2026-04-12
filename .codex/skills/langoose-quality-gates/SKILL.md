---
name: langoose-quality-gates
description: Apply the canonical Langoose finish criteria, file hygiene, validation expectations, and CI alignment rules. Use when deciding what must be validated or updated before work is truly done.
---

# Langoose Quality Gates

Read [AGENTS.md](../../../AGENTS.md) first.

Use this skill when the task is primarily about file hygiene, finish criteria, validation scope, or CI alignment.

## Use When

- You need to decide which checks a change must pass.
- You need to know whether CI, docs, workflows, or contracts must be updated together.
- You are reviewing whether work is ready to consider done.

## Primary Doc

- [quality-gates.md](../../../docs/agent/quality-gates.md)

## Related Docs

- [workflows.md](../../../docs/agent/workflows.md)
- [git-conventions.md](../../../docs/agent/git-conventions.md)

## Critical Reminders

- This skill answers "what must be validated or updated before completion?" not "what command should I run right now?"
- Run the smallest relevant build, test, and acceptance checks for the change.
- Validate every CI lane the change will trigger, not just the convenient local ones.
- When paths, Dockerfiles, test assemblies, or contracts move, update the affected workflows and docs in the same change.
- If a lane is blocked or fails, say so plainly and do not report it as passing by inference.
