ALTER TABLE wallets ALTER COLUMN coin SET DEFAULT 0,
    ADD COLUMN cash bigint NOT NULL DEFAULT 0 CHECK (cash >= 0),
    ADD COLUMN gold bigint NOT NULL DEFAULT 0 CHECK (gold >= 0);
ALTER TABLE wallet_ledger ADD COLUMN cash_delta bigint NOT NULL DEFAULT 0,
    ADD COLUMN gold_delta bigint NOT NULL DEFAULT 0;
