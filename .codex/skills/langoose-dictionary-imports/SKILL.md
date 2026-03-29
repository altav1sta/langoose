---
name: langoose-dictionary-imports
description: Work on Langoose dictionary, CSV import/export, and duplicate-merging behavior. Use when changing dictionary visibility, base-vs-custom merge rules, CSV validation, import/export flow, quick-add behavior, or custom-data clearing in apps/api and apps/web.
---

# Langoose Dictionary Imports

Read [D:\Projects\langoose\AGENTS.md](D:\Projects\langoose\AGENTS.md) first.

Use this skill when dictionary rules or CSV workflows are involved.

## Main Rule

- Preserve dictionary visibility and duplicate-collapsing invariants unless the task explicitly changes them.

## Workflow

- Inspect `apps/api/Services/DictionaryService.cs`.
- Inspect `apps/api/Controllers/DictionaryController.cs`.
- Inspect the corresponding frontend calls in `apps/web/src/api.ts` and UI behavior in `apps/web/src/App.tsx`.
- Inspect the discoverable tests in `tests/Langoose.Api.Tests` before changing behavior.

## Langoose-Specific Guidance

- Visible items should collapse duplicate normalized English terms across base and custom sources.
- Quick add and CSV import should merge duplicate custom items instead of multiplying them.
- Base vocabulary overlap should stay visible as a single base-backed item.
- CSV import should remain strict about header order and optional columns.
- Malformed CSV should not partially import rows.
- Clearing custom data should remove the user's custom content and study traces without revoking active sessions.

## Load Additional Detail Only When Needed

- For concrete repo rules and review points, read [D:\Projects\langoose\.codex\skills\langoose-dictionary-imports\references\dictionary-rules.md](D:\Projects\langoose\.codex\skills\langoose-dictionary-imports\references\dictionary-rules.md).
