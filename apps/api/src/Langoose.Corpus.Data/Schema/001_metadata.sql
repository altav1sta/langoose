-- Corpus metadata: schema version, per-source snapshot versions, etc.
-- Keys are app-defined. Values are arbitrary text (or JSON serialized to text).
CREATE TABLE IF NOT EXISTS corpus_metadata (
    key VARCHAR(100) PRIMARY KEY,
    value TEXT NOT NULL,
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
