# Langoose API Contracts

## Main Boundary Files

- Request/response DTOs: `apps/api/src/Langoose.Api/Models/*.cs`
- Domain entities: `apps/api/src/Langoose.Domain/Models/*.cs`
- Controllers: `apps/api/src/Langoose.Api/Controllers/*.cs`
- Frontend client types and calls: `apps/web/src/api.ts`

## DTO Mapping Pattern

Controllers own all DTO ↔ domain model mapping. Services in Core accept and return
domain models only — they never see request/response DTOs.

```
Controller receives request DTO
  → maps to domain model
  → calls service interface (from Domain)
  → receives domain model result
  → maps to response DTO
  → returns to client
```

Auth-specific DTOs (SignInRequest, SignUpRequest, etc.) stay in Api/Models since auth
controllers don't go through Core services.

## Current Characteristics

- Request and response DTOs are records.
- Enum fields use `JsonStringEnumConverter`.
- The frontend mirrors API enums as string unions.
- The frontend request helper treats `202` and `204` as no-body responses and handles
  CSV as `text/csv`.
- Controllers return flat DTOs (not raw domain models). The frontend never needs to
  understand the DictionaryEntry/UserDictionaryEntry/Translations split — the API
  flattens it.

## Key Response Shapes

- **Dictionary items**: flat DTO combining DictionaryEntry + UserDictionaryEntry +
  translation data. Includes `enrichmentStatus` (pending, enriched, invalidSource,
  invalidTarget, invalidLink, providerError) and `partOfSpeech`.
- **Study cards**: includes `cloze` (from EntryContext), sentence translation
  (from paired context via Translations), `translations` (from
  Translations navigation), `grammarHint` (from PartOfSpeech + GrammarLabel),
  `difficulty` (from EntryContext).
- **Import response**: includes `pendingCount`.
- **Study answer result**: includes `entryContextId` for context tracking.
- When pending items exist, poll the dictionary endpoint on an interval to refresh
  status. Stop polling when no items are pending.

## Review Checklist

- Did the C# DTO shape change?
- Did the controller response code or content type change?
- Did the frontend type or parsing logic change too?
- Did enum values remain string-compatible?
- Did nullable vs optional semantics stay consistent between C# and TypeScript?
- Are controllers doing DTO ↔ domain mapping (not services)?
