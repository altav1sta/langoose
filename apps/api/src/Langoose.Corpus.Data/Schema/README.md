# Corpus schema

SQL schema files for the `langoose_corpus` database. Files are embedded as
resources in `Langoose.Corpus.Data` and applied in lexicographic order by the
DbTool's `init` command.

## Conventions

- Files are named `NNN_<source>.sql` where `NNN` is a zero-padded ordinal.
- Each file is idempotent — every `CREATE` uses `IF NOT EXISTS`.
- Each file targets a single source or concern. Adding a new source means
  adding a new file (e.g. `003_wordfreq.sql`), not modifying existing ones.
- Files are applied in a single transaction by `CorpusInitializer`.

## Source-first principle

Each source's tables mirror its native shape rather than fitting a unified
schema. Wiktionary entries use a hybrid Postgres + JSONB shape (structured
columns for indexed lookups, JSONB for the document body). Wordfreq
rankings use a flat tabular shape — wordfreq is inherently a list of
`(word, rank, zipf_score)` tuples, so no JSONB. Tatoeba sentences use a
LIST-partitioned table by `lang_code` (mirroring Wiktionary's per-lang
partitioning) plus a flat `tatoeba_links` table for translation pairings;
no JSONB because the basic Tatoeba exports are flat (`id, lang, text`).
Future sources (CEFR-J, Global Voices) will use whatever shape best
preserves their native structure.
