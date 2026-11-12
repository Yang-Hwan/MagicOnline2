CREATE TABLE shop_content_assets (
    asset_key text PRIMARY KEY CHECK (trim(asset_key) <> ''),
    content_hash text, content_type text, width integer, height integer
);
CREATE TABLE shop_products (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    kind text NOT NULL CHECK (kind IN ('GemPack','Cue','Character','MessagePack')),
    name text NOT NULL CHECK (trim(name) <> ''), description text NOT NULL DEFAULT '',
    thumbnail text REFERENCES shop_content_assets, price_gem integer,
    sort_order integer NOT NULL DEFAULT 0,
    sale_starts_at timestamptz, sale_ends_at timestamptz,
    visible boolean NOT NULL DEFAULT false, purchasable boolean NOT NULL DEFAULT false,
    revision integer NOT NULL DEFAULT 1, updated_at timestamptz NOT NULL DEFAULT now(),
    CHECK ((kind='GemPack' AND price_gem IS NULL) OR (kind<>'GemPack' AND price_gem IS NOT NULL AND price_gem>=0)),
    CHECK (sale_ends_at > sale_starts_at)
);
CREATE TABLE shop_gem_packs (
    product_id bigint PRIMARY KEY REFERENCES shop_products,
    amount integer NOT NULL CHECK (amount>0), bonus integer NOT NULL CHECK (bonus>=0)
);
CREATE TABLE shop_cues (
    product_id bigint PRIMARY KEY REFERENCES shop_products, visual text NOT NULL REFERENCES shop_content_assets,
    power_pct integer NOT NULL CHECK (power_pct BETWEEN -40 AND 20),
    follow_pct integer NOT NULL CHECK (follow_pct BETWEEN -40 AND 20),
    tip_pct integer NOT NULL CHECK (tip_pct BETWEEN -10 AND 10),
    shot_seconds integer NOT NULL CHECK (shot_seconds BETWEEN -5 AND 5)
);
CREATE TABLE shop_character_emotions (
    product_id bigint REFERENCES shop_products, emotion text NOT NULL,
    asset_key text NOT NULL REFERENCES shop_content_assets, PRIMARY KEY(product_id,emotion)
);
CREATE TABLE shop_message_packs (
    product_id bigint PRIMARY KEY REFERENCES shop_products, personality text NOT NULL
);
CREATE TABLE shop_messages (
    product_id bigint REFERENCES shop_message_packs, position integer NOT NULL,
    message text NOT NULL CHECK(trim(message)<>''), PRIMARY KEY(product_id,position)
);
CREATE TABLE shop_user_inventory (
    user_id uuid REFERENCES users, product_id bigint REFERENCES shop_products,
    acquired_at timestamptz NOT NULL DEFAULT now(), PRIMARY KEY(user_id,product_id)
);
CREATE TABLE shop_user_equipment (
    user_id uuid, slot text CHECK(slot IN ('Cue','Character')), product_id bigint NOT NULL,
    PRIMARY KEY(user_id,slot), FOREIGN KEY(user_id,product_id) REFERENCES shop_user_inventory
);
CREATE TABLE shop_user_selected_message_packs (
    user_id uuid, product_id bigint REFERENCES shop_message_packs, PRIMARY KEY(user_id,product_id),
    FOREIGN KEY(user_id,product_id) REFERENCES shop_user_inventory
);
CREATE TABLE shop_purchases (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, user_id uuid NOT NULL REFERENCES users,
    product_id bigint NOT NULL REFERENCES shop_products, paid_gem integer NOT NULL CHECK(paid_gem>=0),
    purchased_at timestamptz NOT NULL DEFAULT now(), request_id uuid NOT NULL, UNIQUE(user_id,request_id)
);
