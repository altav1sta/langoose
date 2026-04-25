-- Frequency rankings imported from wordfreq (web + subtitle + Twitter
-- corpora, CC-BY-SA). Flat tabular shape — wordfreq is inherently a list
-- of (word, rank, zipf_score) tuples, so no JSONB is warranted here.
--
-- The `source` column distinguishes different frequency lists (e.g.
-- 'wordfreq-large-3.1.1', 'wordfreq-2026-04-25', or eventually a
-- SUBTLEX/OpenSubtitles dump layered on the same table). The PK keeps
-- one rank per (lang, word, source) so re-imports replace cleanly.
CREATE TABLE IF NOT EXISTS wordfreq_rankings (
    lang_code VARCHAR(10) NOT NULL,
    word VARCHAR(300) NOT NULL,
    rank INTEGER NOT NULL,             -- 1 = most frequent
    zipf_score NUMERIC(5,2) NOT NULL,  -- wordfreq's Zipf scale, ~0–8
    source VARCHAR(50) NOT NULL,       -- e.g. 'wordfreq-2026-04-25'
    PRIMARY KEY (lang_code, word, source)
);

-- Lookup pattern: "top N words for this language", used by the
-- --frequency-filter-top filter on import-wiktionary and by the future
-- corpus-backed enrichment provider when ranking translation candidates.
CREATE INDEX IF NOT EXISTS ix_wordfreq_rankings_rank
    ON wordfreq_rankings (lang_code, rank);
