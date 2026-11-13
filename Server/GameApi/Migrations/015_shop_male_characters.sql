DO $$
DECLARE
    player record;
    face record;
    folder text;
    product bigint;
    gem_price integer;
BEGIN
    SELECT COALESCE(min(price_gem),100) INTO gem_price FROM shop_products
    WHERE kind='Character' AND visible AND purchasable AND price_gem>0;

    FOR player IN SELECT * FROM (VALUES
        ('Builder','건설 노동자','노란 안전모를 쓴 현장 반장. 쿠션 각도도 한눈에 잰다.',21),
        ('Gangster','조직 폭력배','버건디 정장과 금목걸이가 트레이드마크인 승부사.',22),
        ('Hitman','킬러','은빛 앞머리 아래로 마지막 한 점을 조용히 노린다.',23),
        ('JobSeeker','백수','민트색 트레이닝복 차림으로 단골 당구장을 찾는다.',24),
        ('Wanderer','노숙자','녹슨 빛깔의 니트 모자를 쓴 거리의 당구 고수.',25),
        ('Gentleman','신사','남색 정장에 아이보리 스카프를 즐겨 매는 클래식 당구 애호가.',26),
        ('MartialArtist','무술 유단자','붉은 머리띠를 단단히 묶고 한 번의 스트로크에 집중한다.',27),
        ('Student','대학생','둥근 안경과 파란 과잠이 어울리는 당구 동아리 회원.',28)
    ) AS roster(key,name,description,position)
    LOOP
        folder := 'Gameplay/ShopCharacters/' || player.key || '/';
        IF EXISTS (SELECT FROM shop_character_emotions WHERE asset_key=folder || 'Neutral') THEN
            CONTINUE;
        END IF;
        INSERT INTO shop_content_assets(asset_key,content_type,width,height)
        VALUES(folder || 'Neutral','image/png',384,416) ON CONFLICT DO NOTHING;
        INSERT INTO shop_products(kind,name,description,thumbnail,price_gem,sort_order,visible,purchasable)
        VALUES('Character',player.name,player.description,folder || 'Neutral',gem_price,player.position,true,true)
        RETURNING id INTO product;
        FOR face IN SELECT * FROM (VALUES
            ('기본','Neutral'),('감사','Thanks'),('미안','Sorry'),('미소','Smile'),('환희','Joy'),
            ('짜증','Annoyed'),('우울','Sad'),('좌절','Frustrated'),('분노','Angry')
        ) AS poses(emotion,key)
        LOOP
            INSERT INTO shop_content_assets(asset_key,content_type,width,height)
            VALUES(folder || face.key,'image/png',384,416) ON CONFLICT DO NOTHING;
            INSERT INTO shop_character_emotions(product_id,emotion,asset_key)
            VALUES(product,face.emotion,folder || face.key);
        END LOOP;
    END LOOP;
END $$;
