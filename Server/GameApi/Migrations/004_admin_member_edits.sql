ALTER TABLE admin_audit_logs ADD COLUMN before_value jsonb;
ALTER TABLE admin_audit_logs ADD COLUMN after_value jsonb;
ALTER TABLE admin_audit_logs ADD COLUMN reason text;
