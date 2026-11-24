ALTER TABLE shop_cues ADD CONSTRAINT cue_bowl_auto_valid
CHECK(NOT (ability ? 'BowlAuto') OR jsonb_typeof(ability->'BowlAuto')='boolean');
