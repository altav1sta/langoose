---
name: langoose-study-engine
description: Work on Langoose study-loop behavior. Use when changing card selection, spaced repetition scheduling, tolerant answer grading, normalization, dashboard counts, or tests related to the study engine in apps/api.
---

# Langoose Study Engine

Read [AGENTS.md](../../../AGENTS.md) first.

Use this skill for study-loop behavior and grading logic.

## Use When

- The task changes grading, normalization, scheduling, or card selection.
- The task changes dashboard counts or study response behavior.
- The task changes tests around study-loop rules.

## Primary Doc

- [study-engine.md](../../../docs/agent/study-engine.md)

## Related Docs

- [dotnet-testing.md](../../../docs/agent/dotnet-testing.md)

## Critical Reminders

- Treat grading and scheduling as product rules, not incidental implementation details.
- Inspect `apps/api/src/Langoose.Core/Services/StudyService.cs`.
- Inspect `apps/api/src/Langoose.Core/Utilities/TextNormalizer.cs` when matching or tolerance behavior is involved.
- Inspect the relevant tests under `apps/api/tests`, especially the unit and integration coverage around study behavior.
- Update tests whenever the grading or scheduling contract changes intentionally.
