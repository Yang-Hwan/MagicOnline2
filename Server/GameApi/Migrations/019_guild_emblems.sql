ALTER TABLE guilds ADD COLUMN emblem_id integer NOT NULL DEFAULT 0 CHECK (emblem_id BETWEEN 0 AND 5);
