# Parallel corpora for example sentences

The learning loop's study unit is `EntryContext` — a sentence with a
cloze deletion, paired across languages via the
`entry_contexts_translations` join. Materialising those rows requires a
parallel corpus: pre-aligned sentence pairs across the user's source and
target languages. This doc records how we choose those sources, what
landed first, and what was deferred.

See [`enrichment-guidance.md`](enrichment-guidance.md) for how
`EntryContext` slots into the broader enrichment pipeline, and
[`dictionary-schema-design.md`](dictionary-schema-design.md) for the
sense-aware dictionary schema that contexts attach to.

## Selection criteria

A parallel corpus must clear four bars to land in `langoose_corpus`:

1. **License compatible with redistribution.** CC-BY (any version) and
   CC0 are both fine — attribution adds a single block to
   `ATTRIBUTION.md` and a UI banner under epic
   [#92](https://github.com/altav1sta/langoose/issues/92). NC clauses
   disqualify because the published dump is consumed by anyone running
   Langoose against their own database. CC-BY-SA is technically allowed
   but contagious — every dump that includes SA content is itself
   SA-licensed. We accept this for Wiktionary because the entire
   dictionary side is already CC-BY-SA, but for sentences we prefer
   plain CC-BY so future commercial-friendly variants stay open.
2. **Coverage across the user's L1 and L2.** Russian must be present
   for the project's primary user pair. Most corpora that aren't built
   around the EU institutions do; the ones built around them often
   don't.
3. **Sentence quality at B1+.** The grader evaluates a learner's answer
   against a translated example, so the example needs to be
   well-formed enough that a B1 learner could plausibly produce it.
   Web-scraped corpora fail this; crowd-translated and editorially
   reviewed corpora pass.
4. **Sentence-level alignment, not document-level.** Document-aligned
   corpora (parallel news articles) require an alignment pass before
   they're usable, and the alignment pass is its own quality drag.
   Pre-aligned sentence pairs go directly into `<source>_links`.

## License matrix

| Source | Text license | Audio | Status | Notes |
|--------|--------------|-------|--------|-------|
| Tatoeba | CC-BY 2.0 | CC-BY-NC (excluded) | shipping (#113) | Crowd-translated short sentences, ~12M total, ~400 langs |
| Global Voices | CC-BY 3.0 | n/a | next (#91) | Editorially reviewed news; smaller, formal register |
| ParaCrawl v9 | CC0 | n/a | deferred | Web-scraped, very large, very noisy, B1+ requires aggressive filtering |
| Europarl v7 | open redistribution | n/a | deferred | EU parliament; B1+ formal but no Russian — wrong fit for primary L2 |
| OpenSubtitles | mixed | n/a | rejected | Some sub-corpora are NC; alignment is too noisy |
| CEFR-J | open with citation | n/a | future | Wordlist (vocabulary tagging), not parallel sentences — overlay, not source |
| English Profile | commercial | n/a | rejected | Licence cost |

## Source-first storage

Every corpus follows the source-first principle established by Wiktionary
(see [`enrichment-guidance.md`](enrichment-guidance.md)): tables mirror
the upstream's native shape rather than a unified canonical model. For
sentence corpora that means:

- One source = one pair of tables, e.g. `tatoeba_sentences` +
  `tatoeba_links`. Future Global Voices import lands as
  `globalvoices_sentences` + `globalvoices_links`, not by adding a
  `source` discriminator on a shared "sentences" table.
- Sentence text stored verbatim, no normalisation. Punctuation,
  whitespace, and casing are kept as the source published them — a
  later filtering or normalisation pass at query time can be revised
  without re-importing.
- Link tables are flat (not partitioned). They're already small and
  carry no `lang_code` directly without a join.
- Sentence tables are LIST-partitioned by `lang_code`, mirroring
  `wiktionary_entries`. This makes single-language re-imports cheap
  (only one partition wiped) and a future per-language dump
  straightforward.

## Tatoeba (#113)

- **Schema:** [`004_tatoeba.sql`](../../apps/api/src/Langoose.Corpus.Data/Schema/004_tatoeba.sql).
  `tatoeba_sentences (lang_code, sentence_id, text, source)` partitioned
  by `lang_code`; PK `(lang_code, sentence_id)`. `tatoeba_links
  (source_id, target_id, source)` flat; PK `(source_id, target_id)`,
  index on `target_id` for reverse traversal. No JSONB column — Tatoeba's
  basic exports are flat (`id\tlang\ttext`).
- **Importer:** `TatoebaImporter` in
  [`Langoose.Corpus.DbTool/Importers/`](../../apps/api/src/Langoose.Corpus.DbTool/Importers/TatoebaImporter.cs).
  Takes a directory containing
  `<lang>_sentences.tsv`, `<pair-lang>_sentences.tsv`, and `links.tsv`
  (plain or .gz). COPY BINARY into both partitions in one transaction;
  filters the global links file to rows whose endpoints both span the
  imported pair.
- **Downloader:** [`scripts/download-tatoeba.sh`](../../scripts/download-tatoeba.sh).
  Maps ISO 639-1 codes to Tatoeba's three-letter codes, fetches the
  per-language sentence dumps and the global links file into a shared
  `data/corpus/tatoeba/` directory (multiple pair imports share one
  copy of `links.tsv` and per-language sentence files coexist as
  siblings). Decompresses Tatoeba's `.bz2` archives so the importer
  needs no bzip2 dependency itself.
- **Scope today:** one language pair at a time. Re-importing wipes both
  partitions plus the entire `tatoeba_links` table — multi-pair
  coexistence is explicitly out of scope for #113. A future enhancement
  can add an upsert path if a dump needs to carry multiple pairs.
- **What this enables:** `EntryContext` materialisation can now read
  Tatoeba sentences by `(lang_code, sentence_id)` and follow a link to
  the paired-language sentence. The actual context generator is a
  separate worker job tracked under
  [#115](https://github.com/altav1sta/langoose/issues/115).

### Test-dump filter (TODO: revisit when #114 lands)

`import-tatoeba` accepts `--max-pairs <n>` for mini-dump runs. The
current implementation pre-filters everything in memory before
COPYing — it never loads rows that would be thrown away:

1. **Phase A** — scan both sentence files into in-memory
   `HashSet<long>`s of IDs. No COPY.
2. **Phase B+C** — stream the global `links.tsv`, group cross-pair
   rows by canonical `(min(src, tgt), max(src, tgt))` key (so both
   directions of the same pair go into one bucket), sort canonical
   pairs by `(lo, hi)`, then take the first N while enforcing a
   hardcoded cap of 2 pairs per sentence (keeps the kept set diverse
   rather than dominated by a few "popular" sentences). Each kept
   canonical pair contributes **all** its directional rows (typically
   both `(a→b)` and `(b→a)` since Tatoeba's links file is
   bidirectional). Yields `keptLinks`, `keptSentenceIds`, and
   `keptPairCount`.
3. **Phase D** — re-stream each sentence file, COPY only rows whose
   ID is in `keptSentenceIds`.
4. **Phase E** — COPY the in-memory `keptLinks` list.

This guarantees the dump is fully linked AND bidirectional — every
sentence participates in at least one cross-language pair, and every
kept pair has both `(a→b)` and `(b→a)` rows so a downstream lookup
by source_id finds the translation in either direction without a
UNION.

**Three alternatives were considered and rejected**:

(a) **Naive first-N over each sentence file** — biased to "Hello!"-era
trivia, plus the first-N en sentence_ids barely overlap with first-N
ru sentence_ids in the global links file, leaving thousands of orphan
sentences with only dozens of links.

(b) **Full-import-then-DELETE** — functional but slow; DELETEing ~2M
sentences from a partitioned table generates massive WAL.

(c) **Naive first-N over (source_id, target_id) of cross-pair links** —
better than (a)/(b) but produces unbalanced direction representation:
when ru-source rows happen to sort into the budget window before
en-source rows (or vice versa), the dump ends up containing only
ru→en (or only en→ru) links. The downstream forward lookup
("translate sentence X") then misses for half the dump. The
canonical-pair approach above keeps both directions for each kept
pair.

The pre-filter approach uses ~24 MB of memory for ~1M cross-pair
links (each canonical pair holds ~1–2 link rows) — fine to
materialise. Re-streaming each sentence file once is trivial compared
to the DB cost it avoids.

The `(lo, hi)` ordering is still biased toward low-numbered (early-
registered) sentences. The intended replacement, once
[#114](https://github.com/altav1sta/langoose/issues/114)
(lemmatization spike) lands, is a frequency-based filter analogous
to `import-wiktionary --frequency-filter-top`: select sentences
whose lemmas appear in the top-N of `wordfreq_rankings`, then keep
only the cross-language pairs connecting those sentences. That keeps
the mini-dump representative of words a learner would actually
study.

Until then, `scripts/rebuild-test-corpus-dump.sh` invokes
`import-tatoeba --max-pairs $LIMIT` (reusing the same `LIMIT` knob
the test rebuild already uses for the wiktionary frequency-top).
When #114 lands, replace the `--max-pairs` invocation in that script
with the lemma-frequency equivalent and drop `--max-pairs` from the
importer (or keep it for ad-hoc use). The full-corpus rebuild
imports everything for the chosen pair, so it doesn't need a filter.

### Following a link

Forward direction (lang → pair):

```sql
SELECT pair.text
FROM tatoeba_links l
JOIN tatoeba_sentences pair
  ON pair.lang_code = 'ru' AND pair.sentence_id = l.target_id
WHERE l.source_id = :english_sentence_id;
```

Reverse direction is served by the `target_id` index. Tatoeba's links
file is bidirectional — both directions are stored — so a single
forward lookup is enough in practice; the reverse index is there for
the "what translates *into* this" query that the lemmatizer might use.

## Deferred sources

The deferral notes here matter because the same questions come up every
few months ("why don't we use ParaCrawl, it's huge?"). Keep this
section honest — promote a source from this list when the deferral
reason no longer applies, don't re-add it from scratch.

### ParaCrawl v9

CC0, ~50M sentence pairs across English↔(many EU langs), web-scraped.
Deferred because:

- Quality variance is enormous — boilerplate, broken HTML fragments,
  non-sentence text. Aggressive length filtering (10–80 chars) and
  language-ID re-checking would reject 60–80% before a B1+ bar even
  enters the picture.
- ~10 GB on disk per major pair. Hosting and re-distributing dumps at
  that scale is a bigger ops decision than #113 alone needs to make.

Reconsider after Tatoeba+GV are exhausting their material; the heavy
filter pipeline can be designed around concrete coverage gaps rather
than speculatively.

### Europarl v7

~2M sentence pairs, B1+ register, redistribution-friendly. Rejected for
the primary user pair because Europarl has 21 EU languages but **no
Russian** — the project's primary L2. When future user pairs target
EU↔EU pairs (de↔fr, es↔it, …) Europarl moves back into scope.

### OpenSubtitles

Subtitle alignment is famously noisy, and several sub-corpora carry NC
clauses inherited from their source distributors. Rejected outright.

## Future overlays

- **CEFR-J** (#92 vocabulary). Difficulty annotation per word, not a
  parallel corpus. Will land as a separate `cefr_levels` table that
  joins to `wiktionary_entries.word`, used to colour-grade
  `EntryContext.Difficulty`.
- **Sense-level context attachment.** Today `EntryContext` attaches to
  a `DictionaryEntry` form. Once the lemmatizer (#114) can resolve a
  Tatoeba sentence to specific senses, a follow-up can refine the
  attachment to `DictionarySense` so contexts disambiguate
  multi-meaning lemmas (e.g. English `lead` metal vs. leash).
