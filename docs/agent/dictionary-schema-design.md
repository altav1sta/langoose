# Dictionary Schema Design

Status: **proposed** (2026-04-25). Spec for the schema rework that unblocks
base-dictionary materialization (#57), the corpus-backed enrichment provider
(#92), and context generation (#91). When the implementation lands the status
moves to **adopted** and this doc becomes the canonical reference.

## Why

The current `DictionaryEntry` model assumes one word, one POS, one shared set
of translations across the whole entry. That breaks against real bilingual
data:

- "bank" (financial) and "bank" (river) share `(text, POS)` so their
  translation sets collapse into one — the corpus already keeps senses, the
  app discards them on materialization.
- "to take a shower" ↔ "принять душ", "look up" ↔ "искать",
  "забронировать" ↔ "to book in advance" have no clean home in a
  single-word, single-POS row.
- "run" → бежать / управлять / работать / течь needs a per-sense
  primary-translation order. `wordfreq_rankings` was imported (#96) for
  exactly this, but there is no app-side column to store the rank.
- Bulk-loading the base dictionary from a corpus needs a quality gate
  (heuristic + AI + manual review) before content becomes public, but
  there is nowhere to stage candidate rows pending review.

This ADR resolves all four problems together because separating them
produces incompatible intermediate schemas.

## Decisions

### 1. Senses

Translations move off `DictionaryEntry` and onto a new `Sense` child.

**Convention (not schema-enforced):** Senses live on **base entries** —
rows where `BaseEntryId IS NULL`. Derived forms (inflections) inherit
senses through their base. The study engine already enforces this
indirectly by selecting only base entries for the studyable pool, so a
derived form never needs its own senses queried directly. We do not add
a CHECK constraint — the cost outweighs the marginal correctness gain.

**Sense identity is per-entry.** A Sense belongs to exactly one
DictionaryEntry (`Sense.DictionaryEntryId` is a single FK). Two
synonymous English words ("couch" and "sofa") get independent Sense
rows even when their glosses overlap; this mirrors how source
dictionaries (Wiktionary etc.) emit data and avoids a fragile
cross-entry sense merge step at import time. Synonym deduplication, if
ever needed, is an additive future concept (`SenseSynonymGroup` or
similar) — not in scope here.

```
Sense
  Id              Guid (v7)
  DictionaryEntryId  Guid -> DictionaryEntry.Id
  SenseIndex      int      -- 0-based stable order within the entry
  Gloss           string   -- short definition in the entry's own language
  Translations    M2M -> Sense (cross-language, sense-scoped)
  CreatedAtUtc    timestamptz
  UpdatedAtUtc    timestamptz

unique (DictionaryEntryId, SenseIndex)
```

Rationale: matches the corpus shape (`wiktionary_senses` is the source of
truth for sense splits), gives the study engine a place to attach
sense-specific contexts and translations, and lets the UI ask the user
"which meaning?" when adding ambiguous words.

`DictionaryEntry.Translations` (existing M2M) is **dropped**. All
cross-language linking goes through `Sense.Translations`.

### 2. Phrases and idioms via permissive POS

Multi-word units are stored as ordinary `DictionaryEntry` rows. `Text` is
already 300 chars wide; the only change is a relaxed `PartOfSpeech`
vocabulary that adds:

- `phrase` — neutral multi-word unit (e.g. "look up", "take a shower")
- `idiom` — figurative fixed expression
- `expression` — catch-all for anything that doesn't fit a single-word POS

POS stays a `string` (no enum), so this is purely a vocabulary update on
the materialization side and a small relaxation in any code that assumed a
fixed word-class list. The corpus emits matching POS values from
Wiktionary directly (`phrase`, `proverb`, `idiom`).

Why not a separate `Expression` entity: it would duplicate Sense,
Translation, Context, and UserProgress structures for negligible gain.
The study engine treats phrases identically to single words — cloze on a
form, grade against expected text. A dedicated entity earns its weight
only if phrase study diverges meaningfully, which it does not in the
foreseeable scope.

### 3. Translation ranking

`Sense.Translations` is a many-to-many; the join row carries a rank.

```
sense_translations
  source_sense_id  Guid
  target_sense_id  Guid
  rank             int       -- 0-based; lower wins for "primary translation"
  source           text      -- enum: 'wiktionary', 'manual', 'import'

primary key (source_sense_id, target_sense_id)
```

Rank is seeded from `wordfreq_rankings` at materialization time: the
target lemma's frequency rank in its language determines order among
candidate translations of the same source sense. Manually entered
translations get rank 0 by default (user intent wins over corpus
frequency).

### 4. Provenance

Provenance lives on the **staging row** that records the approval — not
on the canonical tables. Once content is promoted, it's verified content;
the audit trail (who/what/when produced this) is reachable via
`StagingEntry.PromotedEntryId`.

```
StagingEntry.Source : enum  ('wiktionary', 'manual', 'import', 'user-suggest')
                            -- pipeline routing identifier; part of the row's identity
```

`DictionaryEntry`, `Sense`, and `SenseTranslation` deliberately do **not**
carry `Source`. Reasoning:

- `DictionaryEntry` is a `(text, language, POS)` dedup key — multiple
  senses from multiple sources can attach to it, so an entry-level
  `Source` would be either duplicative or ambiguous.
- `Sense.Source` and `SenseTranslation.Source` would duplicate
  information already captured by the staging row that produced the
  promotion. After approval, the content is canonical — provenance
  matters for the *decision history*, not the *current state*.
- For "selectively re-import refreshed Wiktionary data": the bulk
  pipeline re-stages incoming rows. Dedup against canonical rows happens
  via `(Source, SourceRefId)` on the staging side, not by reading
  provenance off canonical rows.

Future flexibility: if a concrete need shows up (e.g. form-level
provenance for selective inflection refresh), add a *nullable* `Source`
column at that point. Don't speculate.

### 5. Staging tables

Raw imports do **not** write directly into `DictionaryEntry`. They land
in a staging table inside `langoose_app` and pass through quality gates
before promotion.

```
staging_entries
  Id              Guid (v7)
  Source          enum   ('wiktionary', 'wordfreq', 'csv-import', 'user-suggest')
  SourceRefId     text   -- e.g. corpus row id, csv row #, originating user id
  Language        text
  Text            text
  PartOfSpeech    text
  Payload         jsonb  -- the full source-shape blob (senses, translations, forms, …)
  Status          enum   (see below)
  StatusReason    text   -- optional, why the row landed in its current status
  AiConfidence    real   -- nullable, set by the AI validation step
  AiReasoning     text   -- nullable, set by the AI validation step
  ReviewedByUserId Guid  -- nullable, set on manual approve/reject
  ReviewedAtUtc   timestamptz
  PromotedEntryId Guid   -- nullable, set when row promotes to DictionaryEntry
  CreatedAtUtc    timestamptz
  UpdatedAtUtc    timestamptz

index (Status)
index (Source, Status)
```

`Payload` is the **bundle shape** for one candidate dictionary entry —
the entry header plus all of its senses and their cross-language
translations. The reviewer approves or rejects the whole bundle as a
unit; the promotion job parses the payload and creates
`DictionaryEntry + Sense[] + SenseTranslation[]` atomically.

```jsonc
// staging_entries.payload (jsonb)
{
  "entry": { "language": "en", "text": "bank", "pos": "noun", "grammar_label": null },
  "senses": [
    {
      "sense_index": 0,
      "gloss": "a financial institution",
      "translations": [
        { "language": "ru", "text": "банк", "pos": "noun", "rank": 0 }
      ]
    },
    {
      "sense_index": 1,
      "gloss": "the land alongside a river",
      "translations": [
        { "language": "ru", "text": "берег", "pos": "noun", "rank": 0 }
      ]
    }
  ],
  "raw": { /* original source row, preserved for re-runs */ }
}
```

Why bundle, not separate `staging_senses` / `staging_sense_translations`
tables: source dictionaries (Wiktionary especially) emit senses nested
under their entry; reviewers think about a word's meanings together;
splitting the staging schema would force the reviewer to manage
cross-table relationships during review for no real gain.

`raw` keeps the source row so a re-run of any pipeline stage can
reconsult the original without going back to the corpus DB.

#### Status enum

```
Imported            -- just landed from the source, not yet inspected
HeuristicAccepted   -- passed cleanup filter, ready for AI validation
HeuristicRejected   -- terminal: filter rejected (numeric, special chars, …)
AiAccepted          -- AI batch said this looks good
AiRejected          -- terminal: AI batch flagged as bad
Approved            -- manual reviewer approved
Rejected            -- terminal: manual reviewer rejected
Promoted            -- terminal: row written to DictionaryEntry/Sense, FK in PromotedEntryId
```

Forward transitions only. A rejected row stays rejected — re-import
creates a new row. This keeps the audit trail clean.

## Two flows that share the corpus

### Bulk-seed flow (the new pipeline)

```
corpus.wiktionary_entries  ┐
corpus.wordfreq_rankings   ┘  →  app.staging_entries (Imported)
                                       │
                              [heuristic filter]
                                       ├→ HeuristicAccepted
                                       └→ HeuristicRejected (terminal)
                                       │
                              [AI batch validation]
                                       ├→ AiAccepted
                                       └→ AiRejected (terminal)
                                       │
                              [manual review]
                                       ├→ Approved
                                       └→ Rejected (terminal)
                                       │
                              [promotion job]
                                       ↓
                          app.dictionary_entries (IsPublic=true)
                          app.senses
                          app.sense_translations
```

### User-add flow (existing, lightly adapted)

User adds a custom word → `UserDictionaryEntry` (Pending, visible to the
user with status) → enrichment worker looks up the corpus / canonical
tables for an existing public Sense matching the user's term + POS.

- **Match found.** Link `UserDictionaryEntry.SourceEntry` (and
  `TargetEntry`) to the existing public `DictionaryEntry`. Status
  advances to `Enriched`. The user can study it.
- **No match.** Status transitions to a terminal Invalid* status
  (`InvalidSource` / `InvalidLink` / `ProviderError`). The user sees the
  entry in their dictionary list with a status badge but cannot study
  it.

**Validation is required**, not optional:

- Pending / Invalid* user entries are visible in the user's list but
  excluded from the study pool. Only `Enriched` entries become
  studyable.
- The corpus is treated as already vetted; presence in the corpus is
  acceptance. The user-add path does **not** go through the bulk-seed
  pipeline's AI batch + manual review.
- Terms not present anywhere are not silently accepted.

**Senses are public-only in MVP.** Users do not have a private sense
schema. If their term has no public sense:

- For now: the entry stays in a terminal Invalid* status.
- Later (out of scope here): an auto-staging path can create a
  `StagingEntry { Source = 'user-suggest', Payload = {...} }` that joins
  the bulk-seed review queue. Once promoted, the user's entry can be
  retried and linked.

Why no private senses: a parallel schema (`UserSense`,
`UserSenseTranslation`) doubles the model for marginal MVP value.
Defer until users actually hit the gap and complain.

## Pipeline stage details

### Heuristic filter

Runs inline at import. Cheap, deterministic, no external calls.

Rejects entries that:

- Contain digits or non-letter symbols beyond apostrophe / hyphen / space
- Fall outside length bounds (1 < len ≤ 300, configurable)
- Have POS in a hard blocklist (`name` / proper noun, `abbrev`, `symbol`)
- Already exist in `DictionaryEntry` with the same `(Language, Text, PartOfSpeech)`
  → not technically a reject, just skipped from staging

Accepted rows land as `HeuristicAccepted`. Tunable via configuration so
the allow-list can change without re-importing.

### AI batch validation

Worker job that picks `HeuristicAccepted` rows in batches and calls the
Anthropic API in batch mode (50% off) with prompt caching on the
instruction prefix (the schema, the rubric, the examples are static).

The model decides:
- Is this a real word/phrase in the source language?
- Does each sense gloss read coherently?
- For each candidate translation in `Payload.translations[]`: does it
  look like a plausible meaning of the source sense?
- Should this entry be marked an idiom or phrase rather than a base POS?

Output per row: a verdict (`accept` / `reject`), confidence ∈ [0,1], and
short reasoning. Stored in `AiConfidence` / `AiReasoning`. Verdict
advances the status to `AiAccepted` or `AiRejected`.

Vendor-neutral interface (`IStagingValidator`) so a future swap to
another provider — or to a heuristic fallback — is mechanical.

### Manual review and promotion

A small CLI (initially) lists `AiAccepted` rows, optionally filtered by
language / POS / confidence. The reviewer can:

- Approve → status `Approved`, queued for promotion
- Reject → status `Rejected`, terminal
- Edit-and-approve → mutate `Text` / `Payload` then approve

The promotion job reads `Approved` rows, writes `DictionaryEntry` +
`Sense` + `sense_translations`, sets `PromotedEntryId`, marks the
staging row `Promoted`. Promotion is idempotent on `(Source, SourceRefId)`.

A web UI for review is out of scope for this ADR; the CLI is enough to
run the initial seed. #65 (admin tooling) can pick up the UI later.

## Migration path

The existing `DictionaryEntry` table holds ~10 seeded entries. Three
viable strategies:

1. **Drop and re-seed.** Acceptable because the seed is small and
   replaceable. Cleanest schema. Loses no production user data
   (UserDictionaryEntry is empty in practice for early users).
2. **Backfill senses 1:1.** Each existing `DictionaryEntry` gets one
   `Sense` row with `SenseIndex=0` and the existing translations move
   into `sense_translations`. Then drop the entry-level translation join.
3. **Keep the old `dictionary_entries_translations` join in parallel
   for a release.** Higher complexity, no real upside given the data
   volume.

Recommended: **(1) drop and re-seed**. The seed loader is replaced by
the new bulk pipeline anyway. The migration is a single EF migration
that adds tables and drops the old translation join.

## Out of scope for this ADR

- The shape of the AI validation prompt — that's an implementation
  decision in the AI batch validation issue.
- Web admin UI for review — tracked separately under #65.
- Real-time enrichment for user adds — `UserDictionaryEntry` keeps its
  current async lifecycle.
- Per-user crowdsourcing of public entries — possible later via
  `Source = 'user-suggest'`, not in this scope.
- Sense disambiguation in the study engine UI (asking the user "which
  meaning?") — that's a product feature on top of the schema, tracked
  separately.

## Implementation issues

The ADR is the spec; the work splits as:

- **#94** — Schema implementation (this doc → migrations + entities).
- **#57 child A** — Bulk import from corpus into staging with heuristic filter.
- **#57 child B** — AI batch validation pass.
- **#57 child C** — Admin review CLI + promotion job.
- **#57 child D** — Initial bulk seed run, ship the resulting dump.
- **#91** — Context generation, after schema lands.
