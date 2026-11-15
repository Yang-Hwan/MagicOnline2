DO $$
DECLARE cue record;resource text;product bigint;gem_price integer;
BEGIN
 SELECT COALESCE(min(price_gem),100) INTO gem_price FROM shop_products WHERE kind='Cue' AND visible AND purchasable AND price_gem>0;
 FOR cue IN SELECT * FROM (VALUES
  ('ThunderGod','뇌신 큐','검정·금색 장식과 푸른 보석. 전류와 번개 입자가 흐르는 큐대.',2093,247,21),
  ('Nebula','성운 큐','은빛 궤도 장식과 보라색 보석. 빛의 소용돌이와 별가루가 회전하는 큐대.',2114,257,22),
  ('CrimsonFlame','홍염 큐','붉은 봉황과 금빛 장식. 불꽃과 잿불 입자가 흐르는 큐대.',2116,261,23)
 ) AS designs(key,name,description,width,height,position)
 LOOP
  resource:='Gameplay/PremiumCues/'||cue.key;
  INSERT INTO shop_content_assets(asset_key,content_type,width,height) VALUES(resource,'image/png',cue.width,cue.height) ON CONFLICT DO NOTHING;
  IF NOT EXISTS(SELECT FROM shop_cues WHERE visual=resource) THEN
   INSERT INTO shop_products(kind,name,description,thumbnail,price_gem,sort_order,visible,purchasable)
   VALUES('Cue',cue.name,cue.description,resource,gem_price,cue.position,true,true) RETURNING id INTO product;
   INSERT INTO shop_cues(product_id,visual,power_pct,follow_pct,tip_pct,shot_seconds) VALUES(product,resource,0,0,0,0);
  END IF;
 END LOOP;
END $$;
