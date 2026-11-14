CREATE TABLE guild_seasons(id integer PRIMARY KEY, starts_at timestamptz NOT NULL, ends_at timestamptz NOT NULL, finalized_at timestamptz);
CREATE TABLE guild_season_scores(season_id integer REFERENCES guild_seasons(id),guild_id uuid REFERENCES guilds(id),points integer NOT NULL DEFAULT 1000,wins integer NOT NULL DEFAULT 0,losses integer NOT NULL DEFAULT 0,draws integer NOT NULL DEFAULT 0,final_rank integer,PRIMARY KEY(season_id,guild_id));
CREATE TABLE guild_war_queue(user_id uuid PRIMARY KEY REFERENCES users(id),guild_id uuid NOT NULL REFERENCES guilds(id),season_id integer NOT NULL REFERENCES guild_seasons(id),hall_id integer NOT NULL,seen_at timestamptz NOT NULL DEFAULT now(),created_at timestamptz NOT NULL DEFAULT now());
CREATE TABLE guild_wars(match_id text PRIMARY KEY,season_id integer NOT NULL REFERENCES guild_seasons(id),user_a uuid NOT NULL REFERENCES users(id),user_b uuid NOT NULL REFERENCES users(id),guild_a uuid NOT NULL REFERENCES guilds(id),guild_b uuid NOT NULL REFERENCES guilds(id),hall_id integer NOT NULL,created_at timestamptz NOT NULL DEFAULT now(),bound boolean NOT NULL DEFAULT false,settled_at timestamptz,result_a text,delta_a integer NOT NULL DEFAULT 0,delta_b integer NOT NULL DEFAULT 0,CHECK(user_a<>user_b AND guild_a<>guild_b));
CREATE INDEX guild_wars_users ON guild_wars(season_id,user_a,user_b,created_at);
CREATE TABLE guild_contributions(season_id integer REFERENCES guild_seasons(id),guild_id uuid REFERENCES guilds(id),user_id uuid REFERENCES users(id),games integer NOT NULL DEFAULT 0,points integer NOT NULL DEFAULT 0,PRIMARY KEY(season_id,guild_id,user_id));
CREATE TABLE guild_season_rewards(season_id integer REFERENCES guild_seasons(id),user_id uuid REFERENCES users(id),guild_id uuid NOT NULL REFERENCES guilds(id),tier text NOT NULL,coin bigint NOT NULL,gem integer NOT NULL,emblem text NOT NULL,claimed_at timestamptz,PRIMARY KEY(season_id,user_id));
CREATE TABLE guild_membership_spans(id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,user_id uuid NOT NULL REFERENCES users(id),guild_id uuid NOT NULL REFERENCES guilds(id),joined_at timestamptz NOT NULL,left_at timestamptz);
CREATE INDEX guild_membership_spans_end ON guild_membership_spans(user_id,guild_id,joined_at,left_at);
INSERT INTO guild_membership_spans(user_id,guild_id,joined_at) SELECT user_id,guild_id,joined_at FROM guild_members;
CREATE FUNCTION guild_membership_span_record() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF TG_OP='INSERT' THEN INSERT INTO guild_membership_spans(user_id,guild_id,joined_at) VALUES(NEW.user_id,NEW.guild_id,NEW.joined_at); RETURN NEW;
 ELSE UPDATE guild_membership_spans SET left_at=now() WHERE user_id=OLD.user_id AND guild_id=OLD.guild_id AND left_at IS NULL; RETURN OLD;
 END IF;
END $$;
CREATE TRIGGER guild_membership_span_changed AFTER INSERT OR DELETE ON guild_members FOR EACH ROW EXECUTE FUNCTION guild_membership_span_record();
