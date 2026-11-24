-- Keep existing four-stat inventory/match snapshots unchanged; their guide remains 100%.
CREATE OR REPLACE FUNCTION valid_cue_ability(a jsonb) RETURNS boolean LANGUAGE sql IMMUTABLE AS $$
 SELECT COALESCE(a->>'Grade' IN ('A','B','C') AND (a->>'Reach')::int BETWEEN 1 AND 1000000
 AND jsonb_array_length(a->'Initial') IN (4,5)
 AND jsonb_array_length(a->'Maximum')=jsonb_array_length(a->'Initial')
 AND NOT EXISTS(SELECT FROM generate_series(0,jsonb_array_length(a->'Initial')-1) i
 WHERE (a->'Initial'->>i) IS NULL OR (a->'Maximum'->>i) IS NULL
 OR (a->'Initial'->>i)::int<0 OR (a->'Initial'->>i)::int>(a->'Maximum'->>i)::int
 OR (a->'Maximum'->>i)::int>100),false)
$$;
ALTER TABLE shop_cues ALTER COLUMN ability SET DEFAULT '{"Grade":"A","Initial":[100,100,100,50,100],"Maximum":[100,100,100,100,100],"Reach":100}';
