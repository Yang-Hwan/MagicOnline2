CREATE TABLE users (
    id uuid PRIMARY KEY,
    login_id text NOT NULL,
    normalized_login text NOT NULL UNIQUE,
    password_hash text NOT NULL,
    nickname text NOT NULL,
    avatar_idx integer NOT NULL DEFAULT 1 CHECK(avatar_idx > 0),
    level integer NOT NULL DEFAULT 1 CHECK(level > 0),
    level_progress integer NOT NULL DEFAULT 0 CHECK(level_progress >= 0),
    disabled boolean NOT NULL DEFAULT false,
    failed_logins integer NOT NULL DEFAULT 0,
    locked_until timestamptz,
    created_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE wallets (
    user_id uuid PRIMARY KEY REFERENCES users(id),
    coin bigint NOT NULL DEFAULT 20 CHECK(coin BETWEEN 0 AND 9000000000000000),
    jem integer NOT NULL DEFAULT 0 CHECK(jem >= 0)
);
CREATE TABLE user_stats (
    user_id uuid PRIMARY KEY REFERENCES users(id),
    ball_4_win integer NOT NULL DEFAULT 0 CHECK(ball_4_win >= 0),
    ball_4_lose integer NOT NULL DEFAULT 0 CHECK(ball_4_lose >= 0),
    ball_4_draw integer NOT NULL DEFAULT 0 CHECK(ball_4_draw >= 0),
    ball_3_win integer NOT NULL DEFAULT 0 CHECK(ball_3_win >= 0),
    ball_3_lose integer NOT NULL DEFAULT 0 CHECK(ball_3_lose >= 0),
    ball_3_draw integer NOT NULL DEFAULT 0 CHECK(ball_3_draw >= 0),
    legacy_win integer NOT NULL DEFAULT 0,
    legacy_lose integer NOT NULL DEFAULT 0
);
CREATE TABLE auth_sessions (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES users(id),
    access_hash text NOT NULL UNIQUE,
    refresh_hash text NOT NULL UNIQUE,
    access_expires timestamptz NOT NULL,
    refresh_expires timestamptz NOT NULL,
    revoked boolean NOT NULL DEFAULT false
);
CREATE INDEX ON auth_sessions(user_id);
CREATE TABLE auth_refresh_uses (
    token_hash text PRIMARY KEY,
    session_id uuid NOT NULL REFERENCES auth_sessions(id)
);
CREATE TABLE charts (
    name text PRIMARY KEY CHECK(name IN ('CommonCode','SkillMatch','Attendance')),
    rows jsonb NOT NULL CHECK(jsonb_typeof(rows) = 'array')
);
CREATE TABLE matches (
    id text PRIMARY KEY,
    user_a uuid NOT NULL REFERENCES users(id),
    user_b uuid NOT NULL REFERENCES users(id),
    state jsonb NOT NULL,
    phase integer NOT NULL DEFAULT 0,
    CHECK(user_a <> user_b)
);
CREATE TABLE match_players (
    match_id text NOT NULL REFERENCES matches(id),
    user_id uuid NOT NULL REFERENCES users(id),
    active boolean NOT NULL DEFAULT true,
    PRIMARY KEY(match_id,user_id)
);
CREATE UNIQUE INDEX one_active_match_per_user ON match_players(user_id) WHERE active;
CREATE TABLE settlement_receipts (
    match_id text PRIMARY KEY REFERENCES matches(id),
    data jsonb NOT NULL,
    settled_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE match_history (
    match_id text PRIMARY KEY REFERENCES matches(id),
    data jsonb NOT NULL,
    expires_at timestamptz NOT NULL
);
CREATE TABLE user_match_history (
    match_id text NOT NULL REFERENCES matches(id),
    user_id uuid NOT NULL REFERENCES users(id),
    data jsonb NOT NULL,
    expires_at timestamptz NOT NULL,
    PRIMARY KEY(match_id,user_id)
);
CREATE INDEX ON user_match_history(user_id,expires_at DESC);
CREATE INDEX ON match_history(expires_at);
CREATE TABLE wallet_ledger (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES users(id),
    request_key text NOT NULL,
    coin_delta bigint NOT NULL,
    jem_delta integer NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE(user_id,request_key)
);
CREATE TABLE attendance_terms (
    user_id uuid NOT NULL REFERENCES users(id),
    starts_on date NOT NULL,
    ends_on date NOT NULL,
    PRIMARY KEY(user_id,starts_on)
);
CREATE TABLE user_attendance (
    user_id uuid NOT NULL REFERENCES users(id),
    give_date date NOT NULL,
    day_num integer NOT NULL,
    reward_type integer NOT NULL,
    reward_val integer NOT NULL,
    PRIMARY KEY(user_id,give_date)
);
CREATE TABLE legacy_user_map (
    legacy_id text PRIMARY KEY,
    user_id uuid NOT NULL UNIQUE REFERENCES users(id)
);
