ALTER TABLE shop_products DROP CONSTRAINT shop_products_kind_check;
ALTER TABLE shop_products ADD CONSTRAINT shop_products_kind_check CHECK(kind IN ('GemPack','CoinPack','Cue','Character','MessagePack'));
ALTER TABLE shop_products ADD CONSTRAINT shop_coin_price_positive CHECK(kind<>'CoinPack' OR price_gem>0);
CREATE TABLE shop_coin_packs (
 product_id bigint PRIMARY KEY REFERENCES shop_products,
 amount bigint NOT NULL CHECK(amount>0 AND amount<=9000000000000000),
 bonus bigint NOT NULL DEFAULT 0 CHECK(bonus>=0 AND bonus<=9000000000000000),
 CHECK(amount+bonus<=9000000000000000)
);
ALTER TABLE shop_purchases ADD COLUMN credited_coin bigint NOT NULL DEFAULT 0 CHECK(credited_coin>=0);
