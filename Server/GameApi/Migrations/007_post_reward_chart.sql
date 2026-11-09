ALTER TABLE charts DROP CONSTRAINT charts_name_check;
ALTER TABLE charts ADD CONSTRAINT charts_name_check CHECK(name IN ('CommonCode','SkillMatch','Attendance','PostReward'));
