ALTER TABLE user_attendance ADD COLUMN term_start date;
ALTER TABLE user_attendance ADD COLUMN attendance boolean NOT NULL DEFAULT false;
ALTER TABLE user_attendance ADD COLUMN take_dt timestamptz;
-- Preserve historical check-ins and rewards already delivered directly to the wallet.
UPDATE user_attendance SET term_start=give_date-(day_num-1),attendance=true;
UPDATE user_attendance a SET take_dt=l.created_at
FROM wallet_ledger l WHERE l.user_id=a.user_id AND l.request_key='Attendance:'||to_char(a.give_date,'YYYY-MM-DD');
ALTER TABLE user_attendance ALTER COLUMN term_start SET NOT NULL;
CREATE INDEX user_attendance_term_day ON user_attendance(user_id,term_start,day_num);
