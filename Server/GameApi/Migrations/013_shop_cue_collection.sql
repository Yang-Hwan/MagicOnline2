DO $$
DECLARE
    cue record;
    resource text;
    product bigint;
BEGIN
    FOR cue IN SELECT * FROM (VALUES
        ('Seraphim','세라핌','흰 깃털과 금빛 장식의 큐대',1),
        ('Inferno','인페르노','검은 뿔과 붉은 용암 문양의 큐대',2),
        ('Huntsman','헌츠맨','나뭇결과 가죽 손잡이의 큐대',3),
        ('Pulsar','펄서','보랏빛 에너지 코어를 담은 큐대',4)
    ) AS designs(key,name,description,position)
    LOOP
        resource := 'Gameplay/ShopCues/' || cue.key;
        INSERT INTO shop_content_assets(asset_key,content_type,width,height)
        VALUES(resource,'image/png',1080,120) ON CONFLICT DO NOTHING;
        IF NOT EXISTS(SELECT FROM shop_cues WHERE visual=resource) THEN
            INSERT INTO shop_products(kind,name,description,thumbnail,price_gem,sort_order,visible,purchasable)
            VALUES('Cue',cue.name,cue.description,resource,100,cue.position,true,true) RETURNING id INTO product;
            INSERT INTO shop_cues(product_id,visual,power_pct,follow_pct,tip_pct,shot_seconds)
            VALUES(product,resource,0,0,0,0);
        END IF;
    END LOOP;
END $$;
