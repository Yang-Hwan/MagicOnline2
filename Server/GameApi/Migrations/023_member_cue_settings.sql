ALTER TABLE shop_user_inventory ADD COLUMN cue_ability jsonb;
UPDATE shop_user_inventory i SET cue_ability=c.ability FROM shop_cues c WHERE c.product_id=i.product_id;
ALTER TABLE shop_user_inventory ADD CONSTRAINT inventory_cue_ability_valid CHECK(cue_ability IS NULL OR valid_cue_ability(cue_ability));
CREATE FUNCTION snapshot_inventory_cue() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NEW.cue_ability IS NULL THEN
  SELECT ability INTO NEW.cue_ability FROM shop_cues WHERE product_id=NEW.product_id;
 END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER inventory_cue_snapshot BEFORE INSERT ON shop_user_inventory FOR EACH ROW EXECUTE FUNCTION snapshot_inventory_cue();
