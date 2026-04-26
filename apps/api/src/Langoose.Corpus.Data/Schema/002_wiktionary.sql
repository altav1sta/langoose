-- Wiktionary entries imported from Kaikki.org JSONL extracts.
-- Hybrid schema: structured columns (lang_code, word, pos) for fast indexed
-- lookups, plus a JSONB column preserving the full source entry as-is
-- (forms[], senses[], translations[], etymology_number, sounds, categories,
-- ...). The structured columns duplicate three source fields for query
-- ergonomics and planner statistics; the 2-3% JSONB overhead is the price,
-- and keeping `data` byte-compatible with the source is worth it.
--
-- Read-only bulk table: no surrogate PK, no UNIQUE constraint. Kaikki
-- genuinely publishes multiple entries for the same (word, pos) when
-- Wiktionary splits by etymology (e.g. English "lead" = metal + leash,
-- Russian "замок" = castle + lock), so forcing uniqueness would be lossy.
-- Rows are never updated — the importer replaces by lang_code.
--
-- LIST-partitioned by `lang_code` (#97). Each language gets its own
-- partition `wiktionary_entries_<lang>` with its own indexes, so a
-- single-language re-import only rebuilds one partition's GIN index
-- instead of scanning the whole table. Partitions are created on demand
-- by the importer through corpus_wiktionary_ensure_partition() — the
-- parent ships empty with no up-front list of supported languages.
-- Indexes live on the partitions, not the parent, so the drop-rebuild
-- during import only touches the partition being loaded. Query authors
-- must always filter by `lang_code` (documented in
-- docs/agent/enrichment-guidance.md) so the planner can prune.
CREATE TABLE IF NOT EXISTS wiktionary_entries (
    lang_code VARCHAR(10) NOT NULL,
    word VARCHAR(300) NOT NULL,
    pos VARCHAR(50) NOT NULL,
    source VARCHAR(50) NOT NULL,   -- e.g. 'wiktionary-2026-04-25'
    data JSONB NOT NULL
) PARTITION BY LIST (lang_code);

-- Per-partition DDL helpers. Encapsulating the partition + index naming
-- convention here (rather than templating it in C#) keeps the index
-- structure visible alongside the table definition: a future change like
-- adding a third index lives in one place. The C# importer becomes a
-- thin caller that passes lang_code as a parameter.
--
-- All helpers validate lang_code against the same regex; they're safe
-- to call directly from psql. format() with %I quotes identifiers and
-- %L quotes literals, closing the only DDL injection path.

CREATE OR REPLACE FUNCTION corpus_wiktionary_assert_lang_code(lang_code text)
    RETURNS void
    LANGUAGE plpgsql
    IMMUTABLE
AS $$
BEGIN
    IF lang_code IS NULL OR lang_code !~ '^[a-z][a-z0-9_]*$' THEN
        RAISE EXCEPTION 'Invalid lang_code %: must match [a-z][a-z0-9_]*. '
            'lang_code is interpolated into partition/index DDL where parameters '
            'are not allowed; the regex restricts it to a safe identifier.',
            COALESCE(quote_literal(lang_code), 'NULL');
    END IF;
END
$$;

CREATE OR REPLACE FUNCTION corpus_wiktionary_ensure_partition(lang_code text)
    RETURNS void
    LANGUAGE plpgsql
AS $$
BEGIN
    PERFORM corpus_wiktionary_assert_lang_code(lang_code);
    EXECUTE format(
        'CREATE TABLE IF NOT EXISTS %I PARTITION OF wiktionary_entries FOR VALUES IN (%L)',
        'wiktionary_entries_' || lang_code,
        lang_code);
END
$$;

CREATE OR REPLACE FUNCTION corpus_wiktionary_drop_partition(lang_code text)
    RETURNS void
    LANGUAGE plpgsql
AS $$
BEGIN
    PERFORM corpus_wiktionary_assert_lang_code(lang_code);
    EXECUTE format('DROP TABLE IF EXISTS %I', 'wiktionary_entries_' || lang_code);
END
$$;

CREATE OR REPLACE FUNCTION corpus_wiktionary_truncate_partition(lang_code text)
    RETURNS void
    LANGUAGE plpgsql
AS $$
BEGIN
    PERFORM corpus_wiktionary_assert_lang_code(lang_code);
    EXECUTE format('TRUNCATE TABLE %I', 'wiktionary_entries_' || lang_code);
END
$$;

CREATE OR REPLACE FUNCTION corpus_wiktionary_drop_partition_indexes(lang_code text)
    RETURNS void
    LANGUAGE plpgsql
AS $$
DECLARE
    partition_name text;
BEGIN
    PERFORM corpus_wiktionary_assert_lang_code(lang_code);
    partition_name := 'wiktionary_entries_' || lang_code;
    EXECUTE format('DROP INDEX IF EXISTS %I', 'ix_' || partition_name || '_lookup');
    EXECUTE format('DROP INDEX IF EXISTS %I', 'ix_' || partition_name || '_data');
END
$$;

-- Builds the two indexes that every partition carries. Caller is
-- expected to bracket the call with `SET LOCAL maintenance_work_mem`
-- and `SET LOCAL max_parallel_maintenance_workers` for the GIN bulk
-- build — those settings have no effect when issued inside a function
-- body (they apply to the function's call frame, not the surrounding
-- CREATE INDEX), so they have to live at the calling transaction level.
CREATE OR REPLACE FUNCTION corpus_wiktionary_create_partition_indexes(lang_code text)
    RETURNS void
    LANGUAGE plpgsql
AS $$
DECLARE
    partition_name text;
BEGIN
    PERFORM corpus_wiktionary_assert_lang_code(lang_code);
    partition_name := 'wiktionary_entries_' || lang_code;
    EXECUTE format(
        'CREATE INDEX %I ON %I (lang_code, word, pos)',
        'ix_' || partition_name || '_lookup',
        partition_name);
    EXECUTE format(
        'CREATE INDEX %I ON %I USING GIN (data jsonb_path_ops)',
        'ix_' || partition_name || '_data',
        partition_name);
END
$$;

-- Lists every existing wiktionary partition by its lang_code (the
-- partition-name suffix). Drives reset-wiktionary's bulk drop and
-- rebuild-indexes' bulk index rebuild.
CREATE OR REPLACE FUNCTION corpus_wiktionary_list_partition_lang_codes()
    RETURNS SETOF text
    LANGUAGE sql
    STABLE
AS $$
    SELECT substring(c.relname FROM length('wiktionary_entries_') + 1)
    FROM pg_inherits i
    JOIN pg_class c ON c.oid = i.inhrelid
    JOIN pg_class p ON p.oid = i.inhparent
    JOIN pg_namespace ns ON ns.oid = p.relnamespace
    WHERE p.relname = 'wiktionary_entries'
      AND ns.nspname = 'public'
      AND c.relname LIKE 'wiktionary_entries\_%' ESCAPE '\'
    ORDER BY c.relname
$$;
