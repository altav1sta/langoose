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
CREATE TABLE IF NOT EXISTS wiktionary_entries (
    lang_code VARCHAR(10) NOT NULL,
    word VARCHAR(300) NOT NULL,
    pos VARCHAR(50) NOT NULL,
    source_version VARCHAR(32) NOT NULL,
    data JSONB NOT NULL
);

-- Primary lookup: (lang, word, pos) → entry
CREATE INDEX IF NOT EXISTS ix_wiktionary_entries_lookup
    ON wiktionary_entries (lang_code, word, pos);

-- GIN index on the JSONB column with jsonb_path_ops (smaller, faster for @>
-- containment). Used for runtime lookups like:
--   - "entries where any sense has a translation matching (target_lang, target_text)"
--     data @> '{"senses":[{"translations":[{"code":"ru","word":"книга"}]}]}'::jsonb
--   - "entries whose forms[] contains a given inflected form"
--     data @> '{"forms":[{"form":"бронировал"}]}'::jsonb
-- Pure derivations from the JSONB — no separate form-index table needed at
-- this scale. If the provider ever benchmarks these as too slow, a
-- dedicated form-index table can be materialised as a follow-up step
-- (it's pure derived data, rebuildable without re-importing).
CREATE INDEX IF NOT EXISTS ix_wiktionary_entries_data
    ON wiktionary_entries USING GIN (data jsonb_path_ops);
