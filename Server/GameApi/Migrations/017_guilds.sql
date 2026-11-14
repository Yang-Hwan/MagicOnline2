CREATE TABLE guilds (
    id uuid PRIMARY KEY, name text NOT NULL CHECK(char_length(name) BETWEEN 2 AND 20),
    tag text NOT NULL CHECK(tag ~ '^[A-Z0-9]{2,6}$'), description text NOT NULL DEFAULT '' CHECK(char_length(description)<=100),
    join_mode text NOT NULL CHECK(join_mode IN ('Open','Approval')), notice text NOT NULL DEFAULT '' CHECK(char_length(notice)<=300),
    revision integer NOT NULL DEFAULT 1, created_at timestamptz NOT NULL DEFAULT now(), deleted_at timestamptz
);
CREATE UNIQUE INDEX guild_name_unique ON guilds(lower(name)) WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX guild_tag_unique ON guilds(tag) WHERE deleted_at IS NULL;
CREATE TABLE guild_members (
    user_id uuid PRIMARY KEY REFERENCES users(id), guild_id uuid NOT NULL REFERENCES guilds(id),
    role text NOT NULL CHECK(role IN ('Leader','Officer','Member')), joined_at timestamptz NOT NULL DEFAULT now(), last_chat_at timestamptz
);
CREATE INDEX guild_members_guild ON guild_members(guild_id,joined_at);
CREATE UNIQUE INDEX guild_one_leader ON guild_members(guild_id) WHERE role='Leader';
CREATE TABLE guild_creation_requests (
    user_id uuid NOT NULL REFERENCES users(id), request_id uuid NOT NULL, guild_id uuid NOT NULL REFERENCES guilds(id), PRIMARY KEY(user_id,request_id)
);
CREATE TABLE guild_applications (
    guild_id uuid NOT NULL REFERENCES guilds(id), user_id uuid NOT NULL REFERENCES users(id),
    state text NOT NULL CHECK(state IN ('pending','accepted','rejected','cancelled')), updated_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY(guild_id,user_id)
);
CREATE INDEX guild_applications_user ON guild_applications(user_id,state);
CREATE TABLE guild_invitations (
    id uuid PRIMARY KEY, guild_id uuid NOT NULL REFERENCES guilds(id), inviter uuid NOT NULL REFERENCES users(id), recipient uuid NOT NULL REFERENCES users(id),
    state text NOT NULL CHECK(state IN ('pending','accepted','declined','closed')), expires_at timestamptz NOT NULL,
    UNIQUE(guild_id,recipient)
);
CREATE INDEX guild_invitations_inbox ON guild_invitations(recipient,state,expires_at);
CREATE TABLE guild_messages (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, guild_id uuid NOT NULL REFERENCES guilds(id), author uuid NOT NULL REFERENCES users(id),
    request_id uuid NOT NULL, nickname_snapshot text NOT NULL, message text NOT NULL CHECK(char_length(message) BETWEEN 1 AND 250),
    created_at timestamptz NOT NULL DEFAULT now(), UNIQUE(guild_id,author,request_id)
);
CREATE INDEX guild_messages_history ON guild_messages(guild_id,id);
CREATE TABLE guild_audit (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,guild_id uuid NOT NULL REFERENCES guilds(id),actor uuid REFERENCES users(id),
    action text NOT NULL,target uuid REFERENCES users(id),created_at timestamptz NOT NULL DEFAULT now()
);
ALTER TABLE friend_challenges ADD COLUMN guild_id uuid REFERENCES guilds(id);
