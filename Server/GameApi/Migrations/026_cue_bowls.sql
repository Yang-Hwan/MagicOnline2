ALTER TABLE shop_cue_progress ADD COLUMN bowls jsonb;
CREATE TABLE shop_cue_bowl_operations (
 user_id uuid NOT NULL REFERENCES users ON DELETE CASCADE,
 operation uuid NOT NULL, product_id bigint NOT NULL REFERENCES shop_cues(product_id),
 created_at timestamptz NOT NULL DEFAULT now(), PRIMARY KEY(user_id,operation)
);
-- Existing progress and owned ability snapshots remain intact.
UPDATE shop_cues SET ability=ability || '{"BowlMaximum":[20,10,30,20,40]}'::jsonb;
CREATE FUNCTION valid_cue_bowls(a jsonb) RETURNS boolean LANGUAGE sql IMMUTABLE AS $$
 SELECT CASE WHEN a IS NULL THEN true WHEN jsonb_typeof(a)<>'array' THEN false
 WHEN jsonb_array_length(a)<>5 THEN false ELSE NOT EXISTS(
 SELECT FROM jsonb_array_elements(a) v WHERE jsonb_typeof(v)<>'number' OR v::text !~ '^[0-9]+$' OR (v::text)::numeric NOT BETWEEN 1 AND 10000) END
$$;
ALTER TABLE shop_cues ADD CONSTRAINT cue_bowls_valid CHECK(valid_cue_bowls(ability->'BowlMaximum'));
