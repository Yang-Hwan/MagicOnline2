CREATE TABLE friend_links (
    sender uuid NOT NULL REFERENCES users(id),
    recipient uuid NOT NULL REFERENCES users(id),
    accepted boolean NOT NULL DEFAULT false,
    CHECK (sender <> recipient)
);
CREATE UNIQUE INDEX friend_pair ON friend_links(least(sender,recipient),greatest(sender,recipient));
CREATE INDEX friend_inbox ON friend_links(recipient,accepted);
CREATE INDEX friend_outbox ON friend_links(sender,accepted);

CREATE TABLE friend_presence (
    user_id uuid PRIMARY KEY REFERENCES users(id),
    available boolean NOT NULL,
    seen_at timestamptz NOT NULL
);
CREATE TABLE friend_challenges (
    id uuid PRIMARY KEY,
    sender uuid NOT NULL REFERENCES users(id),
    recipient uuid NOT NULL REFERENCES users(id),
    hall_id integer NOT NULL,
    state text NOT NULL CHECK(state IN ('pending','accepted','closed')),
    expires_at timestamptz NOT NULL,
    CHECK(sender <> recipient)
);
CREATE INDEX challenge_sender ON friend_challenges(sender,expires_at);
CREATE INDEX challenge_recipient ON friend_challenges(recipient,expires_at);
