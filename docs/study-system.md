# Study System

The study system presents vocabulary in context through sentence-based flashcards
with spaced repetition scheduling.

## Study Cards

The learning unit is a **sentence with a gap** (EntryContext), not an isolated word.
Each card presents a complete learning context:

```
┌─────────────────────────────────────────────┐
│                                             │
│  She ____ the room yesterday.               │
│                                             │
│  ─────────────────────────────────────────  │
│  Она забронировала комнату вчера.            │
│                                             │
│  Translations: забронировать                │
│  Form: past simple                          │
│  Difficulty: B1                             │
│                                             │
│  Your answer: [          ]                  │
│                                             │
└─────────────────────────────────────────────┘
```

### Card Content

| Field | Source | Description |
|-------|--------|-------------|
| Cloze | EntryContext.Cloze | Sentence with `____` gap |
| Sentence translation | Paired EntryContext.Text (via Translations navigation) | Full sentence in the user's language |
| Translations | DictionaryEntry.Translations → target language base entries | Word-level glosses |
| Grammar hint | DictionaryEntry.PartOfSpeech + GrammarLabel | Expected grammatical form (e.g., "verb, past simple") |
| Expected answer | DictionaryEntry.Text (linked from EntryContext) | The exact form to type |
| Difficulty | EntryContext.Difficulty | Per-context difficulty (A1–B2) |

The grammar hint and expected answer are not stored on the context — they are
derived from the linked DictionaryEntry (the specific form being tested).

### Context Rotation

Each base entry can have multiple EntryContexts across its forms. The study system
rotates through them so the learner sees the word in different contexts and
grammatical forms.

## Card Selection

### Studyable Pool

A user's study pool consists of:

1. **Public DictionaryEntries** (base forms) — the curated base dictionary.
2. **User's enriched custom entries** — DictionaryEntries linked from the user's
   UserDictionaryEntries where `DictionaryEntryId` is not null.

Items excluded from the pool:
- UserDictionaryEntries with `EnrichmentStatus = Pending` or `Failed`
- DictionaryEntries with no EntryContexts

### Selection Algorithm

1. Build the studyable pool (public + user's enriched custom base entries).
2. Join with UserProgress for scheduling data.
3. Create UserProgress lazily for entries encountered for the first time.
4. Order by `DueAtUtc` ascending, then `SuccessCount` ascending (newer items first).
5. Bias toward custom items when they are underrepresented in the pool.
6. Pick a base entry, then pick an EntryContext for one of its forms (rotating).
7. Optionally include UserEntryContexts if the user has added their own.

## Answer Evaluation

The user's answer is compared against the expected answer — the `Text` field of
the DictionaryEntry linked to the EntryContext.

### Verdicts

| Verdict | When | FeedbackCode |
|---------|------|-------------|
| Correct | Exact match or very close | ExactMatch |
| AlmostCorrect | Missing article (a/an/the) | MissingArticle |
| AlmostCorrect | Inflection variant | InflectionMismatch |
| AlmostCorrect | Minor typo (Levenshtein distance 1–2) | MinorTypo |
| Incorrect | Substantially wrong answer | MeaningMismatch |

Evaluation uses `TextNormalizer` for:
- Case-insensitive comparison
- Whitespace and punctuation normalization
- Levenshtein distance for typo tolerance (threshold: 1 char for short words,
  2 chars for 7+ character words)
- Article stripping (a/an/the)

### Tracking

Each answer is recorded as a `StudyEvent` with the `EntryContextId` that was
tested. This enables per-context analytics — identifying which contexts or
grammatical forms are particularly difficult.

## Spaced Repetition

### UserProgress

Tracks study state per (user, base DictionaryEntry). Created lazily on first
encounter.

| Field | Description |
|-------|-------------|
| Stability | Affects interval length. Increases on correct, decreases on incorrect. |
| DueAtUtc | When the entry is next due for review. |
| LapseCount | Total incorrect answers. |
| SuccessCount | Total correct/almost-correct answers. |
| LastSeenAtUtc | Timestamp of last study attempt. |

### Current Scheduler

The scheduler uses fixed stability increments:

| Verdict | Stability Change | Next Interval |
|---------|-----------------|---------------|
| Correct | +0.15 (max 0.95) | 12h x SuccessCount |
| AlmostCorrect | +0.08 (max 0.85) | 8h x SuccessCount |
| Incorrect | -0.12 (min 0.20) | 10 minutes |

This is a placeholder scheduler. M3 plans adoption of FSRS (Free Spaced
Repetition Scheduler) for more sophisticated interval calculation.

## Dashboard

| Metric | Calculation |
|--------|-------------|
| Total items | Public base entries + user's enriched entries |
| Due now | UserProgress rows where DueAtUtc <= now |
| New items | Entries in pool with no UserProgress row |
| Studied today | StudyEvents in the last 24 hours |
