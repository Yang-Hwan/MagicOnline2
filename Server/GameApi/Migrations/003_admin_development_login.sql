ALTER TABLE admin_users ADD COLUMN development_only boolean NOT NULL DEFAULT false;
ALTER TABLE admin_sessions ADD COLUMN development_only boolean NOT NULL DEFAULT false;
