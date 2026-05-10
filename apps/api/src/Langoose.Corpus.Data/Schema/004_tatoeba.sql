-- Tatoeba sentences and translation links — short, crowd-translated example
-- sentences from https://tatoeba.org/. Powers context generation for the
-- learning loop (epic #91): each base dictionary form should attach to
-- one or more EntryContext rows whose text comes from a Tatoeba sentence
-- and whose translation comes from the linked sentence in the paired
-- language. License is CC-BY 2.0 (text); audio is CC-BY-NC and excluded.
-- See docs/agent/parallel-corpora.md for the source-survey rationale.
--
-- Source-first shape, mirroring 002_wiktionary.sql:
--
-- - `tatoeba_sentences` is LIST-partitioned by `lang_code` so a
--   single-language re-import only touches that language's partition. The
--   PK (lang_code, sentence_id) doubles as the lookup index; Tatoeba's
--   sentence_id is the globally unique identifier on tatoeba.org. No
--   JSONB: the per-language exports are flat (id, lang, text); auxiliary
--   metadata (audio URLs, tags) lives in separate dumps and is out of
--   scope for #113.
-- - `tatoeba_links` is flat (not partitioned) — links carry no lang_code
--   without a join. The PK (source_id, target_id) makes re-imports
--   idempotent at the row level; an index on `target_id` supports
--   reverse traversal when materialising EntryContexts from the lemma
--   side ("find every sentence that translates *into* this one").
--
-- Unlike Wiktionary, Tatoeba has no separate user indexes to manage —
-- the per-partition PK index suffices, BTree maintenance during COPY is
-- cheap, and there's no JSONB GIN to bulk-build. The partition helpers
-- below stop at ensure/drop/truncate/list (no drop/create indexes).
CREATE TABLE IF NOT EXISTS tatoeba_sentences (
    lang_code VARCHAR(10) NOT NULL,
    sentence_id BIGINT NOT NULL,
    text TEXT NOT NULL,
    source VARCHAR(50) NOT NULL,         -- e.g. 'tatoeba-2026-05-03'
    PRIMARY KEY (lang_code, sentence_id)
) PARTITION BY LIST (lang_code);

-- Per-partition DDL helpers. Mirrors corpus_wiktionary_* in
-- 002_wiktionary.sql; the C# importer is a thin caller that passes
-- lang_code as a parameter. format() with %I quotes identifiers and
-- %L quotes literals, closing the only DDL injection path.

CREATE OR REPLACE FUNCTION corpus_tatoeba_assert_lang_code(lang_code text)
    RETURNS void
    LANGUAGE plpgsql
    IMMUTABLE
AS $$
BEGIN
    IF lang_code IS NULL OR lang_code !~ '^[a-z][a-z0-9_]*$' THEN
        RAISE EXCEPTION 'Invalid lang_code %: must match [a-z][a-z0-9_]*. '
            'lang_code is interpolated into partition DDL where parameters '
            'are not allowed; the regex restricts it to a safe identifier.',
            COALESCE(quote_literal(lang_code), 'NULL');
    END IF;
END
$$;

CREATE OR REPLACE FUNCTION corpus_tatoeba_ensure_partition(lang_code text)
    RETURNS void
    LANGUAGE plpgsql
AS $$
BEGIN
    PERFORM corpus_tatoeba_assert_lang_code(lang_code);
    EXECUTE format(
        'CREATE TABLE IF NOT EXISTS %I PARTITION OF tatoeba_sentences FOR VALUES IN (%L)',
        'tatoeba_sentences_' || lang_code,
        lang_code);
END
$$;

CREATE OR REPLACE FUNCTION corpus_tatoeba_drop_partition(lang_code text)
    RETURNS void
    LANGUAGE plpgsql
AS $$
BEGIN
    PERFORM corpus_tatoeba_assert_lang_code(lang_code);
    EXECUTE format('DROP TABLE IF EXISTS %I', 'tatoeba_sentences_' || lang_code);
END
$$;

CREATE OR REPLACE FUNCTION corpus_tatoeba_truncate_partition(lang_code text)
    RETURNS void
    LANGUAGE plpgsql
AS $$
BEGIN
    PERFORM corpus_tatoeba_assert_lang_code(lang_code);
    EXECUTE format('TRUNCATE TABLE %I', 'tatoeba_sentences_' || lang_code);
END
$$;

-- Lists every existing tatoeba_sentences partition by its lang_code (the
-- partition-name suffix). Drives reset-tatoeba's bulk drop.
CREATE OR REPLACE FUNCTION corpus_tatoeba_list_partition_lang_codes()
    RETURNS SETOF text
    LANGUAGE sql
    STABLE
AS $$
    SELECT substring(c.relname FROM length('tatoeba_sentences_') + 1)
    FROM pg_inherits i
    JOIN pg_class c ON c.oid = i.inhrelid
    JOIN pg_class p ON p.oid = i.inhparent
    JOIN pg_namespace ns ON ns.oid = p.relnamespace
    WHERE p.relname = 'tatoeba_sentences'
      AND ns.nspname = 'public'
      AND c.relname LIKE 'tatoeba_sentences\_%' ESCAPE '\'
    ORDER BY c.relname
$$;

-- Translation links between Tatoeba sentences. Tatoeba publishes these
-- as a single global file (links.csv inside links.tar.bz2) covering
-- every language pair; the importer filters to the (lang, pair-lang)
-- subset whose endpoints both landed in tatoeba_sentences in the same
-- run. PK (source_id, target_id) is enough on its own — Tatoeba's
-- sentence_id is globally unique across languages, so a link tuple
-- always identifies one specific direction between two specific
-- sentences regardless of language. The `source` column is provenance,
-- not part of the key.
CREATE TABLE IF NOT EXISTS tatoeba_links (
    source_id BIGINT NOT NULL,
    target_id BIGINT NOT NULL,
    source VARCHAR(50) NOT NULL,
    PRIMARY KEY (source_id, target_id)
);

-- Reverse-direction lookup: "find every sentence that translates into
-- sentence X". Forward direction is served by the PK btree.
CREATE INDEX IF NOT EXISTS ix_tatoeba_links_target
    ON tatoeba_links (target_id);
