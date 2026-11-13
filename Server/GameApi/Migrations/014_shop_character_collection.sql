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
        ('Mint','민트','민트색 단발에 번개 핀을 꽂은 침착한 승부사',11),
        ('Brick','브릭','오렌지 후드가 트레이드마크인 힘 좋은 스트리트 플레이어',12),
        ('Plum','플럼','은빛 올림머리와 자주색 스카프가 어울리는 베테랑 챔피언',13)
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
