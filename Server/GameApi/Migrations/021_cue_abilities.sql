ALTER TABLE shop_cues ADD COLUMN ability jsonb NOT NULL DEFAULT '{"Grade":"A","Initial":[100,100,100,50],"Maximum":[100,100,100,100],"Reach":100}';
CREATE FUNCTION valid_cue_ability(a jsonb) RETURNS boolean LANGUAGE sql IMMUTABLE AS $$
 SELECT COALESCE(a->>'Grade' IN ('A','B','C') AND (a->>'Reach')::int BETWEEN 1 AND 1000000
 AND jsonb_array_length(a->'Initial')=4 AND jsonb_array_length(a->'Maximum')=4
 AND NOT EXISTS(SELECT FROM generate_series(0,3) i WHERE (a->'Initial'->>i)::int<0 OR (a->'Initial'->>i)::int>(a->'Maximum'->>i)::int OR (a->'Maximum'->>i)::int>100),false)
$$;
ALTER TABLE shop_cues ADD CONSTRAINT cue_ability_valid CHECK(valid_cue_ability(ability));
CREATE TABLE shop_cue_progress (
 user_id uuid NOT NULL, product_id bigint NOT NULL, shots bigint NOT NULL DEFAULT 0 CHECK(shots>=0),
 PRIMARY KEY(user_id,product_id), FOREIGN KEY(user_id,product_id) REFERENCES shop_user_inventory(user_id,product_id) ON DELETE CASCADE
);
CREATE TABLE shop_cue_progress_events (
 user_id uuid NOT NULL REFERENCES users ON DELETE CASCADE, event_key text NOT NULL CHECK(length(event_key)<=120),
 product_id bigint NOT NULL REFERENCES shop_cues(product_id), points integer NOT NULL CHECK(points>0), created_at timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY(user_id,event_key)
);
UPDATE charts SET rows=rows || '[{"GrpCd":"Cue","ComCd":"Sec","Ord":1,"ComVal":20,"ComVal2":40,"Info":"큐대 제한시간 최저·최고 (초)","IsUse":"Y"}]'::jsonb
WHERE name='CommonCode' AND NOT EXISTS(SELECT FROM jsonb_array_elements(rows) r WHERE r->>'GrpCd'='Cue' AND r->>'ComCd'='Sec');
