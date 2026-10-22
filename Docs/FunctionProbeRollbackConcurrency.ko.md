# 롤백·조건부 경쟁 요청 실제 검사 — 2026-10-09

Unity 한 대에서 진단 펑션 MatchSettlementProbe를 호출했다. 운영 코인·전적 정산은 비활성 상태다.

## 실행 순서

Tools > Magic Online > Backend Function Probe > Run from Arise → Come 씬 user02 로그인 → Consist 씬 진단 창에서 1 Generate Run ID → 3 Seed → 8 Rollback → 9 Concurrent requests → 4 Read self → 10 Retry old version → 4 Read self.

공개 진단 테이블 FunctionProbePublic, Run ID `c6835e6179af4ee3af45c5ef3358b3f6`.
PrimaryRowId `2026-10-09T04:44:16.463Z`, AuditRowId `2026-10-09T04:44:16.580Z`.

## 확인 결과

| 검사 | 실제 응답 및 저장 값 | 판정 |
| --- | --- | --- |
| 두 정상 행 + 존재하지 않는 세 번째 행 갱신 | 내부 404, Primary 0→0, Audit 0→0, RollbackObserved=true | 전체 롤백 관찰 |
| ExpectedVersion=0, 같은 RequestId로 4개 요청 | 내부 204 성공 1개, 내부 404 실패 3개 | 이번 경쟁 실행에서 한 번만 성공 |
| 경쟁 요청 후 본인 조회 | Counter=1, Version=1 | 중복 증가 없음 |
| ExpectedVersion=0, 다른 RequestId로 재시도 | 내부 404, 다시 조회해 Counter=1, Version=1 및 LastRequestId 유지 | 지난 버전 요청 거절 |

HTTP 200은 펑션 호출 성공이며, 실제 저장 성공 여부는 응답 내부 Ok/Status로 판단했다. 마지막 두 조회의 LastRequestId는 `9d0c90315bc840d4830b460701c82c67`로 같았다.

롤백 선행 검사에서 GetV2의 없는 행 응답은 404가 아니라 성공 응답의 빈 row임을 반영해 코드를 수정하고 CLI 빌드·배포했다. 두 계정 갱신 검사에서 이미 확인한 단일 row 파싱과 같은 규칙을 사용했다.

이 검사는 단일 행의 버전 조건 경쟁과 같은 계정의 두 행 롤백을 실제 관찰한 결과다. 여러 경기에서 공유되는 양쪽 지갑·경기 상태·영구 정산 영수증 전체의 조건을 한 트랜잭션으로 묶는 운영 저장소 검증은 별도로 필요하다. 여기서 운영 정산을 활성화하거나 모든 경쟁 상황의 안전성을 확정하지 않는다.

다음 작업: 두 계정 지갑과 경기 상태에 버전 조건을 함께 적용하는 진단 트랜잭션을 구성하고, 하나의 조건이 실패하면 양쪽 변경이 모두 취소되는지 검사한다. 비공개 기록 쓰기 권한도 확인한 후 운영 저장소 구현을 진행한다.

증거: FunctionProbeRollbackConcurrency.log, FunctionProbeRollbackConcurrency.png. 검사 완료 후 Play를 종료했다.
