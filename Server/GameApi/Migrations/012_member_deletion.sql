ALTER TABLE users ADD COLUMN deleted_at timestamptz;
CREATE INDEX users_current_members ON users(created_at DESC,id) WHERE deleted_at IS NULL;
