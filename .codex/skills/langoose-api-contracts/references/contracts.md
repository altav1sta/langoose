# Langoose API Contracts

## Main Boundary Files

- Backend models: `apps/api/Langoose.Api/Models/*.cs`
- Backend entities and persisted domain types: `apps/api/Langoose.Domain/Models/*.cs`
- Controllers: `apps/api/Langoose.Api/Controllers/*.cs`
- Frontend client types and calls: `apps/web/src/api.ts`

## Current Contract Characteristics

- Many request and response DTOs are already records, which matches the repo preference for value-like API models.
- Some API result fields use enum string serialization through `JsonStringEnumConverter`.
- The frontend currently mirrors those values as string unions, especially for `sourceType`, `itemKind`, `status`, and study verdicts.
- The frontend request helper treats `202` and `204` as no-body responses and handles CSV as `text/csv`.

## Review Checklist

- Did the C# DTO shape change?
- Did the controller response code or content type change?
- Did the frontend type or parsing logic change with it?
- Did enum values remain string-compatible?
- Did nullable vs optional semantics stay consistent between C# and TypeScript?

## Recommendations

- Keep one API model type per file under `apps/api/Langoose.Api/Models`.
- Prefer frontend request payload types instead of `Record<string, unknown>` when the payload shape is known.
- Keep transport models separate from persistence entities when behavior starts diverging.
