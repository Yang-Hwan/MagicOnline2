ALTER TABLE shop_cue_progress ADD COLUMN steps bigint NOT NULL DEFAULT 0 CHECK(steps>=0);
ALTER TABLE shop_cue_progress ADD COLUMN progress bigint NOT NULL DEFAULT 0 CHECK(progress>=0);
UPDATE shop_cue_progress p SET steps=p.shots/(c.ability->>'Reach')::int,progress=p.shots%(c.ability->>'Reach')::int FROM shop_cues c WHERE c.product_id=p.product_id;
