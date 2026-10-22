-- Local development DB only. Reserved, short matches for two actual development players.
-- Existing rows are never overwritten. Re-run is safe.
INSERT INTO charts(name,rows) VALUES('SkillMatch','[]') ON CONFLICT DO NOTHING;
UPDATE charts SET rows=rows || '[{"HallIdx":9003,"MatchBall":3,"TargetHit":2,"PrizeCoin":1,"MaxCoin":1000000,"HallName":"로컬 통합 검증 3구","MatchTotMin":1,"MatchCushion":3,"FinishMission":0}]'::jsonb
WHERE name='SkillMatch' AND NOT EXISTS(SELECT 1 FROM jsonb_array_elements(rows) r WHERE (r->>'HallIdx')::int=9003);
UPDATE charts SET rows=rows || '[{"HallIdx":9004,"MatchBall":4,"TargetHit":3,"PrizeCoin":1,"MaxCoin":1000000,"HallName":"로컬 통합 검증 4구","MatchTotMin":1,"MatchCushion":0,"FinishMission":0}]'::jsonb
WHERE name='SkillMatch' AND NOT EXISTS(SELECT 1 FROM jsonb_array_elements(rows) r WHERE (r->>'HallIdx')::int=9004);
