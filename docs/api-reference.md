# API Reference

The API serves the React SPA and handles authentication, dictionary management,
and study sessions. All endpoints are under `http://localhost:5000` locally or
`/api` through the Vercel proxy in staging.

## Authentication

All mutating endpoints require an antiforgery token. The SPA fetches it on
bootstrap and includes it in subsequent requests.

| Method | Path | Description |
|--------|------|-------------|
| GET | `/auth/antiforgery` | Get CSRF token |
| POST | `/auth/sign-up` | Register a new account |
| POST | `/auth/sign-in` | Sign in |
| POST | `/auth/sign-out` | Sign out |
| GET | `/auth/me` | Get current user (or 401) |

Auth uses cookie-based sessions with ASP.NET Core Identity + OpenIddict.
See `docs/auth-mvp-decision.md` for the design rationale.

## Dictionary

| Method | Path | Description |
|--------|------|-------------|
| GET | `/dictionary/items` | List user's visible dictionary items |
| POST | `/dictionary/items` | Add a word to the dictionary |
| PATCH | `/dictionary/items/{id}` | Update a dictionary item |
| POST | `/dictionary/import` | Import words from CSV |
| GET | `/dictionary/export` | Export custom words as CSV |
| DELETE | `/dictionary/custom-data` | Clear all custom data |

### GET /dictionary/items

Returns a flat array of dictionary items combining DictionaryEntry +
UserDictionaryEntry data. Each item includes:

- `id` — the entry ID
- `text` — the word or phrase
- `translations` — array of canonical translations in the user's language
  (from EntryTranslation → target language base entries)
- `partOfSpeech`, `difficulty`
- `isCustom` — whether this is a user-added item
- `enrichmentStatus` — "pending", "enriched", or "failed"
- `notes`, `tags`

### POST /dictionary/items

Add a word. Request body:

```json
{
  "text": "book",
  "translation": "книгу",
  "language": "ru",
  "notes": "",
  "tags": []
}
```

The system looks up the translation as a DictionaryEntry form, follows
`BaseEntryId`, checks EntryTranslation links. If a match is found, the user
entry links to the existing DictionaryEntry immediately. Otherwise, a pending
UserDictionaryEntry is created for background enrichment.

### POST /dictionary/import

Upload CSV content. Required columns: English term, translation(s), type.
Optional: Notes, Tags. Returns import statistics including pending enrichment count.

## Study

| Method | Path | Description |
|--------|------|-------------|
| GET | `/study/next` | Get the next study card |
| POST | `/study/answer` | Submit an answer |
| GET | `/study/dashboard` | Get progress dashboard |

### GET /study/next

Returns the next due study card or 404 if nothing is due:

```json
{
  "entryId": "...",
  "contextId": "...",
  "cloze": "She ____ the room yesterday.",
  "sentenceTranslation": "Она забронировала комнату вчера.",
  "translations": ["забронировать"],
  "grammarHint": "past simple",
  "difficulty": "B1",
  "sourceType": "base"
}
```

Fields derived at query time:
- `cloze` — from EntryContext.Cloze
- `sentenceTranslation` — from the paired EntryContext via ContextTranslation
- `translations` — from EntryTranslation (target language base entries)
- `grammarHint` — from the linked DictionaryEntry.GrammarLabel
- Expected answer — from the linked DictionaryEntry.Text (not sent to client)

### POST /study/answer

Submit an answer for evaluation:

```json
{
  "entryId": "...",
  "contextId": "...",
  "submittedAnswer": "booked"
}
```

Returns the verdict with feedback:

```json
{
  "verdict": "correct",
  "normalizedAnswer": "booked",
  "expectedAnswer": "booked",
  "feedbackCode": "exact_match",
  "nextDueAtUtc": "2026-04-12T10:00:00Z"
}
```

### GET /study/dashboard

Returns study progress:

```json
{
  "totalItems": 150,
  "dueNow": 12,
  "newItems": 5,
  "baseItems": 120,
  "customItems": 30,
  "studiedToday": 8
}
```

## Content

| Method | Path | Description |
|--------|------|-------------|
| POST | `/content/flag` | Report a content quality issue |

## Health

| Method | Path | Description |
|--------|------|-------------|
| GET | `/health` | Health check endpoint |

## Notes

- Enum values are serialized as snake_case strings (e.g., `"exact_match"`,
  `"almost_correct"`).
- The frontend client types in `apps/web/src/api.ts` mirror these contracts.
- CSV export uses `text/csv` content type.
- 202 and 204 responses have no body.
