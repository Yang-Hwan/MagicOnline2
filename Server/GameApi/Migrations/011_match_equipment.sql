CREATE TABLE match_equipment_snapshots (
    match_id text PRIMARY KEY,
    user_a uuid NOT NULL REFERENCES users,
    user_b uuid NOT NULL REFERENCES users,
    friendly boolean NOT NULL,
    data jsonb NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    CHECK(user_a<>user_b)
);
CREATE INDEX match_equipment_snapshots_created ON match_equipment_snapshots(created_at);
