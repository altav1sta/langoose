---
name: langoose-study-engine
description: Work on Langoose study-loop behavior. Use when changing card selection, spaced repetition scheduling, tolerant answer grading, normalization, dashboard counts, or tests related to the study engine in apps/api.
---

# Langoose Study Engine

Read [AGENTS.md](../../../AGENTS.md) first.

Use this skill for study-loop behavior and grading logic.

## Main Rule

- Treat grading and scheduling as product rules, not incidental implementation details.

## Workflow

- Inspect `apps/api/src/Langoose.Api/Services/StudyService.cs`.
- Inspect `apps/api/src/Langoose.Api/Services/TextNormalizer.cs` when matching or tolerance behavior is involved.
- Inspect the relevant tests under `apps/api/tests`, especially the unit and integration coverage around study behavior.
- Update tests whenever the grading or scheduling contract changes intentionally.

## Load Additional Detail Only When Needed

- For concrete study rules and review points, read [study-engine.md](../../../docs/agent/study-engine.md).
