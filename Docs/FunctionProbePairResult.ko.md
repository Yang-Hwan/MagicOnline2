# 두 계정 동시 갱신 실제 검사 — 2026-10-09

Unity 에디터 한 대에서 user02와 user04를 차례로 로그인해 뒤끝 서버에 요청했다. 두 기기의 동시 접속 경쟁 검사는 아직 수행하지 않았다.

1. Tools > Magic Online > Backend Function Probe > Run from Arise 실행.
2. Come 씬에서 user02 로그인 버튼 클릭. Consist 씬의 진단 창에서 Run ID 생성 → 3 Seed → 4 Read self: Counter=0, Version=0 확인.
3. Play 종료 후 같은 메뉴로 재실행. Come에서 user04 로그인. 동일 Run ID 입력 → 3 Seed → 4 Read self: Counter=0, Version=0 확인.
4. 상대 계정과 user02 PrimaryRowId를 지정하고 6 Read peer: Counter=0 확인.
5. user04에서 7 Write pair를 한 번 클릭: elapsedMs=480, HTTP 200, 내부 Status=204, Ok=true.
6. 4 Read self: user04 Counter=1, Version=1, LastRequestId=PairWrite. 6 Read peer: user02 Counter=1.
7. 결과 화면과 로그 저장 후 Play 종료.

검사 테이블: FunctionProbePublic. Run ID: `f71dd56b7df74dd5bafc810117d0b818`.

| 계정 | PrimaryRowId | Counter 전 → 후 | 확인 방법 |
| --- | --- | --- | --- |
| user02 | 2026-10-09T04:31:29.247Z | 0 → 1 | 본인 초기 조회, user04의 상대 조회 |
| user04 | 2026-10-09T04:34:54.885Z | 0 → 1 | 본인 초기 및 최종 조회 |

실행 중 발견한 문제 두 가지를 수정하고 공식 CLI로 재배포했다. 요청 JSON의 ISO 계정 ID가 날짜 객체로 변환되던 문제와 GetV2의 단일 `row` 응답을 `rows` 배열로 처리하던 문제다. 회귀 검사를 추가해 로컬 41개 검증이 통과했다.

판정: 진단 펑션에서 다른 계정의 공개 데이터 조회와 한 트랜잭션으로 두 계정 갱신이 가능함을 실제 확인했다. 실패 시 전체 롤백, 경쟁 요청의 조건부 갱신, 비공개 테이블 접근, 운영 코인·전적 정산은 이 검사로 검증되지 않았다. 운영 정산은 계속 SettlementEnabled=false다.

증거: FunctionProbePairResult.log, FunctionProbePairResult.png.
