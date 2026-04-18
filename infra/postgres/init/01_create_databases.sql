-- Create the auxiliary databases needed by the Langoose stack. The
-- Postgres image only auto-creates the user's default database
-- (POSTGRES_USER), so we provision the rest here. This script runs
-- only on first initialisation of the data directory.

CREATE DATABASE langoose_app OWNER langoose;
CREATE DATABASE langoose_auth OWNER langoose;
CREATE DATABASE langoose_corpus OWNER langoose;
