---
name: langoose-workflows
description: Use the canonical Langoose build, run, startup, and migration commands. Use when you need the right repo command, startup path, container flow, or migration command for work in this repository.
---

# Langoose Workflows

Read [AGENTS.md](../../../AGENTS.md) first.

Use this skill when the task is primarily about repo commands, startup flow, container flow, or migration commands.

## Use When

- You need the right build, test, run, Docker, or E2E command.
- You need the right local or container command to execute a change safely.
- You need the correct EF migration command or startup project.

## Primary Doc

- [workflows.md](../../../docs/agent/workflows.md)

## Related Docs

- [quality-gates.md](../../../docs/agent/quality-gates.md)
- [efcore-structure.md](../../../docs/agent/efcore-structure.md)
- [docker-guidance.md](../../../docs/agent/docker-guidance.md)

## Critical Reminders

- This skill answers "what command or runtime path should I use?" not "what must be proven before merge?"
- Prefer whole-app container commands when the change affects startup, persistence, auth, or cross-app behavior.
- If a command path is blocked, report the gap plainly instead of inferring success.
- When a change affects migrations, use `Langoose.DbTool` as the startup project for EF commands.
