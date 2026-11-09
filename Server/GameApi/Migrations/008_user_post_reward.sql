CREATE TABLE user_post_reward (
    post_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES users(id),
    event_key text NOT NULL,
    reward_id integer NOT NULL,
    type_code integer NOT NULL,
    type_name text NOT NULL,
    message text NOT NULL,
    sent_at timestamptz NOT NULL,
    expires_at timestamptz NOT NULL CHECK (expires_at > sent_at),
    claimed_at timestamptz,
    currency_code integer NOT NULL,
    currency_name text NOT NULL,
    reward_val bigint NOT NULL CHECK (reward_val >= 0),
    UNIQUE (user_id,event_key,reward_id)
);
CREATE INDEX user_post_reward_member ON user_post_reward(user_id,sent_at DESC,post_id DESC);
ALTER TABLE user_stats ADD COLUMN win_streak integer NOT NULL DEFAULT 0 CHECK(win_streak>=0);
-- Restore the current run from counted settlements without issuing historical mail.
WITH results AS (
    SELECT p.user_id,p.result,r.settled_at,r.match_id
    FROM settlement_receipts r
    CROSS JOIN LATERAL (VALUES ((r.data->>'UserA')::uuid,r.data->>'ResultA'),
                               ((r.data->>'UserB')::uuid,r.data->>'ResultB')) p(user_id,result)
    WHERE p.result IN ('Win','Lose','Draw')
), runs AS (
    SELECT user_id,result,count(*) FILTER (WHERE result<>'Win') OVER
        (PARTITION BY user_id ORDER BY settled_at DESC,match_id DESC ROWS UNBOUNDED PRECEDING) AS breaks FROM results
)
UPDATE user_stats s SET win_streak=x.wins FROM
    (SELECT user_id,count(*)::int AS wins FROM runs WHERE breaks=0 AND result='Win' GROUP BY user_id) x
WHERE s.user_id=x.user_id;
