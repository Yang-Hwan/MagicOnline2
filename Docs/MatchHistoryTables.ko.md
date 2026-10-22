# 대전 경기 기록 테이블

2026-10-06 magicCushion 뒤끝 콘솔에서 MatchHistory와 UserMatchHistory를 생성했다.
둘 다 Private, 스키마 정의 사용, 활성 상태이며 실제 경기 행이나 테스트 행은 만들지 않았다.
모든 칼럼은 null 허용(누락 허용)으로 구성했다. 저장 후 콘솔에서는 숫자 기본값 0, 문자열 기본값 빈 값으로 표시된다. 시작 기록에는 종료 값이 없을 수 있기 때문이다.
필수값·상태 전이·참가자 검증은 이후 서버 저장 로직에서 강제해야 한다. 명시적 null을 보내지 말고, 아직 없는 필드는 생략한다.

## MatchHistory — 경기당 1행, 18칼럼

| 칼럼 | 뒤끝 타입 | C# 타입 | 의미 |
|---|---|---|---|
| MatchId | string | string | 경기 고유값, 서버에서 중복 생성 방지 |
| SchemaVersion | int | int | 최초 1 |
| MatchMode | string | string | Online 등 경기 구분 |
| MatchBall | int | int | 3구=3, 4구=4. 이전 설계의 GameType 역할 |
| HallIdx | int | int | 경기장 번호 |
| PlayerAUserId | string | string | 시작 당시 자리 0 참가자의 뒤끝 user inDate |
| PlayerBUserId | string | string | 시작 당시 자리 1 참가자의 뒤끝 user inDate |
| StartedAt | int | long | 서버 기준 UTC Unix 밀리초 시작 시각 |
| EndedAt | int | long | 종료 시각 |
| DurationSec | int | int | 실제 경기 시간(초) |
| Status | string | string | Preparing / Playing / Completed / Aborted (클라이언트 보고 상태) |
| EndReason | string | string | TargetScore / MatchTimeout / 중단 사유. 서버 확정 사유는 별도 검증 |
| WinnerUserId | string | string | 승자. 무승부·무효는 빈 문자열 |
| PlayerCount | int | int | 현재 2 |
| RulesSnapshot | string | string | 당시 실제 적용 규칙과 보상·재접속 정책의 JSON 문자열 |
| SettlementStatus | string | string | Pending / Processing / Applied / Failed |
| HistoryExpiresAt | int | long | 서버에서 종료 시각 + 달력 기준 6개월 |
| CleanupStatus | string | string | None / Deleting |

RulesSnapshot은 뒤끝 map의 고정 key/동일 value 타입 제약을 피하기 위해 JSON string으로 정의했다.
MatchRulesSnapshot 모델에는 경기장·종목·실제 목표점수·쿠션·마무리·시간·차트 코인 조건과 실제 입장료·승자 보상·재접속 유예시간을 담는다.
특히 기존 4구 경로의 목표 3점 고정 같은 실제 적용값을 기록해야 하며 차트 값만 복사해서는 안 된다.
PrizeCoin은 기존 입장 조건이다. 승자 보상액은 별도 정책으로 확정해야 한다.

## UserMatchHistory — 참가자당 1행, 20칼럼

| 칼럼 | 뒤끝 타입 | C# 타입 | 의미 |
|---|---|---|---|
| MatchId | string | string | 경기 전체 기록 연결 |
| UserId | string | string | 참가자 뒤끝 user inDate |
| OpponentUserId | string | string | 상대 사용자 |
| NicknameSnapshot | string | string | 당시 본인 닉네임 |
| OpponentNicknameSnapshot | string | string | 당시 상대 닉네임 |
| SeatNo | int | int | 시작 당시 자리 0/1 |
| MatchBall | int | int | 3/4 |
| HallIdx | int | int | 개인 이력 경기장별 조회 |
| EndedAt | int | long | 종료 UTC Unix 밀리초 |
| Result | string | string | Win / Lose / Draw / Void |
| Score | int | int | 본인 종료 점수 |
| OpponentScore | int | int | 상대 종료 점수 |
| TurnCount | int | int | 본인 턴 수 |
| BestRun | int | int | 본인 한 턴 최고 득점 |
| EntryCoin | int | long | 실제 입장 차감액 |
| RewardCoin | int | long | 실제 지급액 |
| CoinDelta | int | long | 해당 경기 총 증감(RewardCoin - EntryCoin) |
| SettlementStatus | string | string | Pending / Processing / Applied / Failed |
| SettledAt | int | long | 정산 완료 UTC Unix 밀리초 |
| HistoryExpiresAt | int | long | 전체 경기와 같은 보관 만료 시각 |

## 저장과 소유권

- 경기 시작 전 서버에 MatchId, 참가자, 종목, 당시 규칙을 등록한다. 서버 시작 승인 이후 Playing으로 전환한다.
- 경기 시작 때 입장료를 이미 차감했다면 종료 때 다시 차감하지 않는다. 시작 실패는 취소·반환 처리한다.
- 정상 종료나 이탈 유예 만료 후 서버가 한 번 확정한 결과로 두 개인 기록과 UserMain의 Ball_3_* 또는 Ball_4_* 전적을 반영한다.
- MatchHistory의 owner_inDate는 경기 등록 소유자이며 참가자 식별과 별개다. 방장 변경으로 참가자 ID를 바꾸지 않는다.
- UserMatchHistory는 참가자 본인 소유로 저장하는 계약이다. 일반 클라이언트는 타인의 private 데이터를 처리할 수 없으므로 양쪽 기록 저장·정산·정리는 권한 있는 서버 경로가 필요하다.
- MatchId / MatchId+UserId는 논리적 중복 판별 기준이다. 콘솔 스키마가 unique 제약이나 트랜잭션을 제공하도록 만든 것은 아니다.
- 오래되거나 등록되지 않은 경기 요청은 거부한다. 상세 이력만으로 영구 중복 방지를 구현하지 않는다.
- 양쪽 정산 Applied인 종료 이력만 6개월 후 삭제한다. UserMain 누적 승패와 잔액은 삭제하지 않는다.
- 경기 전체 기록은 참가자가 직접 읽을 수 있도록 서버가 참가자 권한을 확인해서 제공한다. Private를 Public으로 바꾸어 이를 해결하지 않는다.

## 이번 작업 범위와 검증

완료: 원격 테이블·칼럼 정의, 두 C# 데이터 모델, 규칙 스냅샷 모델, 테이블명 상수. 콘솔에서 저장 후 수정창을 다시 열어 각 스키마의 18/20개 칼럼과 자료형을 확인했다.

2026-10-06 다음 단계에서 `BackendMatchHistory`, `MatchHistoryReporter`를 추가하고 새 대전 실행 경로인 `PracticeMatchSetupTransport`에 연결했다.

- 방장이 Preparing 기록을 자기 소유 MatchHistory에 저장한다. 양쪽 로딩 완료와 시작 기록 저장 확인이 모두 충족되어야 시작 승인을 보낸다. 저장이 지연되면 기존 30초 준비 제한으로 취소될 수 있다.
- 시작 후 공통 기록은 Playing으로 바뀌고 각 참가자는 본인 UserMatchHistory를 저장한다. 정상 종료는 점수·종목·승패 보고·종료 시각을 갱신한다. 서버 검증 전 SettlementStatus는 계속 Pending이다.
- 준비 취소·연결 복구 실패 등은 공통 기록에 Aborted와 사유를 남긴다. 이미 시작한 경기의 개인 점수는 저장하되, 이탈 승패는 빈 값으로 남겨 서버 판정을 기다린다. 종료 완료 후 나가기는 이미 저장한 정상 결과를 바꾸지 않는다.
- 참가자 ID는 PoolPlayer의 뒤끝 user inDate를 사용하며, 수신 측은 자기 ID와 로그인 계정의 일치를 확인한다. 이 확인은 상대 ID의 서버 인증을 대신하지 않는다.
- 실패한 저장은 계정별 로컬 JSON 저널로 남겨 10초 후 재시도한다. 앱 재실행·재로그인에도 재개한다. 저널은 `Application.persistentDataPath/match-history-<계정 ID의 SHA256>.json`에 저장하며 성공한 항목은 제거한다. 파일을 지우거나 다른 기기로 이동하면 미전송 항목을 복구할 수 없다.
- 재시도는 본인 행의 MatchId로 조회한 후 Insert/Update한다. 같은 요청의 재전송은 기존 행을 갱신하며, Applied 등 Pending이 아닌 정산 상태를 발견하면 덮어쓰지 않는다. 여러 기기의 동시 Insert, 조회-Update 사이 서버 정산 경쟁까지 원자적으로 막는 구현은 아니다. 중복 행 발견 시 자동 갱신을 중지하고 재시도 로그를 남긴다.
- 현재 시각과 6개월 만료 시각은 클라이언트 보고 값이다. 서버가 검증·재계산한 후 삭제 판단에 사용해야 한다. 서버가 이력을 확정하기 전 클라이언트는 코인·누적 전적·정산 필드를 반영하지 않는다. 미측정 TurnCount/BestRun 및 미정산 금액은 전송하지 않는다. RulesSnapshot의 EntryCoin/WinRewardCoin 기본값 0은 아직 합의된 보상 정책이 없다는 뜻이며 실제 차감/지급 증거가 아니다.
- 새 테스트 전송 규약은 `practice-match-test-11`이다. 두 클라이언트 모두 새 소스로 실행해야 한다. 이 경로는 에디터의 `MagicOnline2.PracticeMatchTest` 설정 또는 개발 빌드의 `-practice-match-test`에서만 활성화된다. 공개 로비의 연결은 아래 후속 작업에서 추가했다.

검증: Unity가 생성한 Assembly-CSharp 컴파일 설정과 실제 SDK DLL을 사용한 전체 게임 스크립트 컴파일 통과(기존 경고 있음). 실제 서비스/보고 클래스 소스를 임시 .NET 하네스에서 가짜 Unity/뒤끝 어댑터와 연결하여 14개 검사 통과: 실패 후 재시도, 동일 경기 갱신, 정산 완료 후 덮어쓰기 방지, 전송 중 최신 결과 유지, 앱 재실행 저널 재개, 계정 전환 중 타 계정 저장 방지와 원계정 재개, 등록 확인, 정상 종료 점수·승패, 미정산 유지, 종료 후 나가기, 중단 결과 대기, 3/4구 구분, 달력 기준 6개월 계산.

미구현: 권한 있는 서버 등록·정산 함수, 서버의 이탈 유예 만료 승패 확정, 원자적 중복 정산 방지, 실제 정산 연결, 이력 UI, 예약 삭제. 실제 Unity 두 클라이언트와 뒤끝 Insert/Update 왕복은 미검증이다. 앱 강제 종료나 방장 오프라인으로 남은 Playing 기록은 서버의 별도 만료 처리가 필요하다.

## 공개 대전 경로 연결 — 후속 작업

`PunNetwork.MatchHistory.cs`에 기존 공개 대전의 저장 연결을 추가했다. 테스트 옵션이 꺼진 일반 대전에도 적용된다.

1. 손님이 기존 카운트다운을 마치면 새 요청 ID를 방장에게 보낸다. 방장은 현재 상대 actor와 참가자 프로필을 확인하고 새 MatchId로 Preparing 기록을 저장한다.
2. 뒤끝 저장 성공 확인 후 방장이 경기 ID·종목·규칙·참가자를 손님에게 전달한다. 손님은 로그인한 자기 ID, 방장 ID, 경기장과 현재 차트 규칙을 비교하고 자기 개인 기록을 로컬 저널에 남긴 뒤 준비 확인을 보낸다.
3. 방장이 시작 승인을 보내면 양쪽이 같은 경기 ID와 시작 시각을 사용한다. 기존 UI의 StartToPlay 전환은 이 승인 이후 실행된다. 처음 등록 및 준비는 30초 내 완료되어야 하며 제한을 넘으면 중단한다.
4. 실제 공개 게임은 결과 RPC 없이 PnlOutcome를 직접 여는 경우가 있어, 결과 화면 OnEnable에도 저장을 연결했다. 점수와 화면에 표시되는 승패/무승부를 각자 보고하고, 방장이 공통 결과를 갱신한다. 명시적인 BattleResult/BattleResult2 호출도 동일한 경기 ID를 포함하는 결과 메시지를 사용한다.

5. 정상 결과를 저장한 뒤 나가기는 결과를 바꾸지 않는다. 이탈·연결 끊김·방장 변경은 Aborted와 사유를 보고하며 개인 Result는 빈 값, SettlementStatus는 Pending이다. 이 단계는 재접속 유예 후 몰수승을 확정하거나 상대 코인을 정산하지 않는다. 기존 공개 경로의 연결 복구 자체도 새로 구현하지 않았다.
6. 재대전은 기존 양쪽 재대전 동의를 확인하고 새 MatchId를 발급한다. 이전 경기 ID의 늦은 결과, 중복 시작 요청, 시간 초과 뒤 저장 성공 콜백은 현재 경기를 시작/종료시키지 못한다.

규칙 스냅샷의 4구 목표는 현재 공개 게임의 PoolCoach 동작과 동일하게 3점으로 기록한다. 3구는 해당 차트 TargetHit를 사용한다.

공개 매칭 프로토콜을 `pavilion-practice-physics-5-history`로 변경했다. 이전 버전과 혼합 매칭되지 않으며 상대 컴퓨터도 같은 소스로 다시 빌드해야 한다. 시작 승인 과정이 추가되어 기존 카운트다운 뒤 기록 저장·확인 대기 시간이 생길 수 있다. 준비 실패 화면은 기존 입장 실패 상태를 사용한다.

검증: 실제 SDK 참조 및 Unity 컴파일 설정으로 전체 게임 스크립트 컴파일 통과. 실제 `PunNetwork.MatchHistory.cs`를 가짜 Photon/뒤끝/Unity 어댑터로 실행한 11개 검사 통과: 저장 확인 전 시작 차단, 양쪽 동일 경기 ID, 카운트다운 중복 방지, 양쪽 계정의 승패 분리, 완료 후 나가기, 재대전 새 ID, 이전 경기 결과 거부, 연결 중단 Pending 유지, 4구 구분, 양쪽 무승부, 시간 초과 후 지연된 저장 확인 거부. 이는 실제 두 기기의 게임/Photon/뒤끝 왕복 검증을 대신하지 않는다.

서버 측 적용 전 필수 사항: 현재 저장된 결과와 시각은 모두 클라이언트 보고다. 서버가 로그인 토큰으로 두 참가자를 인증하고 서버 규칙·시각으로 시작을 등록해야 한다. 이탈은 서버 heartbeat/연결 증거와 유예 만료로 확정한다. 누적 전적과 코인 갱신은 영구 정산 원장의 MatchId+UserId 단위 중복 방지 및 원자적 처리와 함께 수행하고, 양쪽 개인 기록을 권한 있는 서버 경로로 갱신해야 한다. 현재 저장만 보고 `Ball_3_*`/`Ball_4_*` 또는 상대 코인을 바로 변경하면 안 된다.

공식 API의 숫자/스키마 처리 참고: https://docs.backnd.com/sdk-docs/backend/base/game-information/insert

## 서버 정산 로직 및 펑션 활성화

뒤끝 펑션 활성화를 완료했고, 양쪽 입장료 시작 차감·승자 합계 지급·무승부 반환 정책으로 .NET 8 정산 로직과 28개 검사를 작성했다. 3구/4구 전적 분리, 서버 heartbeat 기반 이탈 판정, 전체 롤백, 영구 정산 원장 계약을 포함한다. 실제 운영 저장소와 함수 배포·클라이언트 연결은 아직 완료되지 않았다. 구현 범위, 권한 제약과 재현 방법은 [서버 정산 문서](../Server/README.ko.md)를 참고한다.
