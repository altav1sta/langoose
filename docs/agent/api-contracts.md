# Langoose API Contracts

## Main Boundary Files

- Backend API models: `apps/api/src/Langoose.Api/Models/*.cs`
- Persisted domain types: `apps/api/src/Langoose.Domain/Models/*.cs`
- Controllers: `apps/api/src/Langoose.Api/Controllers/*.cs`
- Frontend client types and calls: `apps/web/src/api.ts`

## Current Characteristics

- Many request and response DTOs are records.
- Some API result fields use `JsonStringEnumConverter`.
- The frontend mirrors several API enums as string unions.
- The frontend request helper treats `202` and `204` as no-body responses and handles CSV as `text/csv`.

## Review Checklist

- Did the C# DTO shape change?
- Did the controller response code or content type change?
- Did the frontend type or parsing logic change too?
- Did enum values remain string-compatible?
- Did nullable vs optional semantics stay consistent between C# and TypeScript?
