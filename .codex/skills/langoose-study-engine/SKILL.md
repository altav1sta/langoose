---
name: langoose-study-engine
description: Work on Langoose study-loop behavior. Use when changing card selection, spaced repetition scheduling, tolerant answer grading, normalization, dashboard counts, or tests related to the study engine in apps/api.
---

# Langoose Study Engine

Read [D:\Projects\langoose\AGENTS.md](D:\Projects\langoose\AGENTS.md) first.

Use this skill for study-loop behavior and grading logic.

## Main Rule

- Treat grading and scheduling as product rules, not incidental implementation details.

## Workflow

- Inspect `apps/api/src/Langoose.Api/Services/StudyService.cs`.
- Inspect `apps/api/src/Langoose.Api/Services/TextNormalizer.cs` when matching or tolerance behavior is involved.
- Inspect the discoverable study-related tests in `apps/api/tests/Langoose.Api.Tests`.
- Update tests whenever the grading or scheduling contract changes intentionally.

## Langoose-Specific Guidance

- Preserve tolerant grading unless the task explicitly changes it.
- Preserve deterministic scheduling semantics unless the task explicitly changes intervals or scoring policy.
- Keep dashboard counts aligned with the same visible-item logic used by the study loop.
- Treat card selection balance between base and custom items as intentional behavior.

## Load Additional Detail Only When Needed

- For concrete study rules and review points, read [D:\Projects\langoose\.codex\skills\langoose-study-engine\references\study-rules.md](D:\Projects\langoose\.codex\skills\langoose-study-engine\references\study-rules.md).
