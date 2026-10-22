CREATE TABLE admin_users (
    id uuid PRIMARY KEY,
    login_id text NOT NULL,
    normalized_login text NOT NULL UNIQUE,
    password_hash text NOT NULL,
    role text NOT NULL CHECK (role IN ('Owner','Operator','Viewer')),
    disabled boolean NOT NULL DEFAULT false,
    failed_logins integer NOT NULL DEFAULT 0,
    locked_until timestamptz,
    created_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE admin_sessions (
    id uuid PRIMARY KEY,
    admin_id uuid NOT NULL REFERENCES admin_users(id),
    expires_at timestamptz NOT NULL,
    revoked boolean NOT NULL DEFAULT false
);
CREATE INDEX ON admin_sessions(admin_id);
CREATE TABLE admin_audit_logs (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    admin_id uuid REFERENCES admin_users(id),
    action text NOT NULL,
    target text NOT NULL,
    request_id text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ON admin_audit_logs(created_at DESC);
