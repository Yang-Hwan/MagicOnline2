-- Additive, idempotent catalog. No existing products, purchases or inventory are changed.
DO $$
DECLARE cue record;resource text;product bigint;
BEGIN
 FOR cue IN SELECT * FROM (VALUES
('SilverCyborg','실버 사이보그 큐',600,100),
('DarkKnight','다크 나이트 큐',600,101),
('InfernalDemon','인페르노 데몬 큐',650,102),
('OceanSovereign','오션 소버린 큐',650,103),
('OlympianThunder','올림피안 썬더 큐',650,104),
('CelestialGoddess','셀레스티얼 여신 큐',600,105),
('AzureDragon','청룡 큐',650,106),
('GoldenTiger','황금 호랑이 큐',650,107),
('VenusBloom','비너스 블룸 큐',600,108),
('CosmicSovereign','코스믹 소버린 큐',700,109),
('AbyssalSoul','어비스 소울 큐',650,110),
('LunarMystique','루나 미스틱 큐',600,111),
('PhoenixRebirth','불사조 리버스 큐',650,112),
('AurumClassic','아우룸 골드 큐',250,113),
('IonStriker','이온 스트라이커 큐',500,114),
('CheerRed','치어 레드 큐',200,115),
('CheerBlue','치어 블루 큐',200,116),
('CheerYellow','치어 옐로 큐',200,117),
('CheerGreen','치어 그린 큐',200,118),
('CheerPurple','치어 퍼플 큐',200,119),
('SignatureInk','시그니처 만년필 큐',300,120),
('SapphirePrism','사파이어 프리즘 큐',350,121),
('GiftParade','기프트 퍼레이드 큐',200,122),
('RoyalJoust','로열 마상창 큐',400,123),
('RainbowAurora','레인보우 오로라 큐',300,124),
('BlossomGarden','블라썸 가든 큐',450,125),
('HyperExpress','하이퍼 익스프레스 큐',450,126),
('SteamLegend','스팀 레전드 큐',450,127),
('PhotonEdge','포톤 광선검 큐',500,128),
('CobraSovereign','킹 코브라 큐',600,129),
('PearlDawn','펄 던 큐',250,130),
('TropicalJade','트로피컬 제이드 큐',250,131),
('MidnightLapis','미드나잇 라피스 큐',250,132),
('AmberNoir','앰버 느와르 큐',250,133),
('FrostBloom','프로스트 블룸 큐',500,134),
('VioletStorm','바이올렛 스톰 큐',500,135),
('ScarletHeart','스칼렛 하트 큐',500,136),
('EmeraldWyrm','에메랄드 웜 큐',500,137),
('BlossomEntwine','꽃덩굴 큐',350,138)
 ) AS designs(key,name,price,position)
 LOOP
  resource:='Gameplay/DailyCues20261126/'||cue.key;
  INSERT INTO shop_content_assets(asset_key,content_type) VALUES(resource,'image/png') ON CONFLICT DO NOTHING;
  IF NOT EXISTS(SELECT FROM shop_cues WHERE visual=resource) THEN
   INSERT INTO shop_products(kind,name,description,thumbnail,price_gem,sort_order,visible,purchasable)
   VALUES('Cue',cue.name,'2026-11-26 제작 스킨. A등급, 능력치 초기 50 / 최대 100, 진도폭 100. 가격과 성장 설정은 관리자에서 변경 가능합니다.',resource,cue.price,cue.position,true,true) RETURNING id INTO product;
   INSERT INTO shop_cues(product_id,visual,power_pct,follow_pct,tip_pct,shot_seconds,ability)
   VALUES(product,resource,0,0,0,0,'{"Grade":"A","Initial":[50,50,50,50,50],"Maximum":[100,100,100,100,100],"Reach":100,"BowlMaximum":[20,10,30,20,40],"BowlAuto":false}'::jsonb);
  END IF;
 END LOOP;
END $$;
