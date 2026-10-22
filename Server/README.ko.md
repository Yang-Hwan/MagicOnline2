# 경기 정산 서버 구현 상태

## 현재 경로: 로컬 PostgreSQL API (2026-10-15)

뒤끝 펑션 대신 `GameApi`의 ASP.NET Core 서버와 PostgreSQL 저장소를 구현했다. [실행 안내](GameApi/README.ko.md)와 [현재 검증 범위](../Docs/LocalDatabaseMigrationProgress.ko.md)를 따른다. HTTP·실제 DB 통합 검사 43개 및 Unity HTTP 스모크 검사를 통과했다. 두 게임 클라이언트의 실제 경기 종합 검증과 Railway 배포는 아직 완료하지 않았다.

아래 내용은 뒤끝 펑션을 검토하던 당시의 기록이며 현재 경로의 구현 상태로 해석하지 않는다.

## 이전 뒤끝 펑션 검토 기록

2026-10-07 단계별 진행 계획과 실제 실행 상태는 [대전 경기 결과 작업 문서](../Docs/MatchResultImplementationPlan.ko.md)를 참고한다. 현재 검사 합계는 정산 규칙 28개와 펑션 진입·진단 10개를 합친 38개다. 아래 28개는 정산 규칙 검사만 가리킨다.

2026-10-06 기준 뒤끝 펑션은 활성화되었다. 실제 정산 함수의 배포와 게임 호출 연결은 아직 완료되지 않았다. `MatchSettlement`는 저장소와 독립적인 .NET 8 정산 로직이며, `MatchSettlement.Tests`의 저장소는 검사용 메모리 구현이다. 메모리 구현을 운영 펑션에 사용하면 안 된다.

## 확정한 코인 정책

양쪽이 준비를 승인하면 입장료를 각각 차감한다. 승자에게 양쪽 입장료의 합계를 지급하고, 무승부는 각각 반환한다. 시작 전 취소는 차감하지 않는다. 경기 시작 후 양쪽 연결이 모두 만료되면 반환하고 승패·무승부 전적은 올리지 않는다.

입장료 100인 예: 시작 때 A/B 각각 -100, A 승리 때 A에게 200 지급. 경기 전체 증감은 A +100, B -100이다. 무승부는 각자 100을 돌려받아 전체 증감이 0이다.

## 구현한 명령

| 명령 | 동작 |
| --- | --- |
| Register | 인증·상대 동의·서버 차트 검증을 끝낸 경기 등록. MatchId의 영구 고유성은 저장소가 보장 |
| Ready | 두 참가자 승인 후 한 번만 입장료 차감 및 서버 시작 시각 기록 |
| Heartbeat | 인증한 본인만 서버의 현재 시각으로 생존 신호 갱신 |
| ConfirmResult | 양쪽의 동일한 점수·승자 보고를 확인한 뒤 정산. 보고 불일치는 보류 |
| Surrender | 인증한 나간 참가자 패배, 상대 승리. 시작 전이면 취소 |
| EvaluateDisconnect | 생존 신호 중단 15초와 복귀 유예 30초가 지난 참가자 패배. 상대 신호는 15초 이내여야 함 |

정상 결과의 양쪽 합의는 서버 물리 시뮬레이션 검증을 대신하지 않는다. 15/30초는 생성자에서 변경 가능한 현재 기본값이다. 양쪽이 오프라인이면 별도 예약 작업이 만료 평가를 수행해야 하며, 해당 예약 작업은 아직 구현하지 않았다.

3구는 `Ball_3_Win/Lose/Draw`, 4구는 `Ball_4_Win/Lose/Draw`만 증가시킨다. 기존 Win/Lose는 보존한다. 클라이언트 프로필 갱신 요청에서는 Coin과 여섯 전적 필드를 제외하여 오래된 로컬 값으로 서버 결과를 덮어쓰지 않도록 했다. 이는 서버의 쓰기 권한 제한을 대신하지 않는다.

## 실제 저장소 연결 조건

`ISettlementStore` 운영 구현에는 다음이 필요하다.

1. 고유 MatchId와 두 사용자에 대한 인증·서버 규칙 검증.
2. 두 사용자 잔액·전적, 경기 상태, 정산 원장을 같은 트랜잭션에서 처리. 일부 실패 시 전체 취소.
3. 같은 경기의 동시 재시도와 다른 경기에서 같은 사용자를 갱신하는 경쟁을 모두 방지하는 조건부 갱신 또는 데이터베이스 잠금.
4. 영구 정산 원장과 6개월 상세 기록의 분리. 상세 기록을 삭제해도 완료 요청을 다시 지급하지 않음.
5. 양쪽 개인 기록에 대한 적절한 서버 권한과 서버 전용 재화·전적 쓰기 경로.

펑션 프로세스 안의 lock, 조회 후 Insert, 단순 일괄 쓰기로 이 조건을 충족했다고 판단하지 않는다. 기존 클라이언트 MatchHistory는 참가자가 보낸 보고이며 서버 등록의 권위 있는 자료로 그대로 사용하지 않는다.

뒤끝 공식 문서에서 GameData UpdateV2는 타인의 Private 수정이 불가능하다고 명시한다. 펑션 일부 유저 데이터 API도 자기 데이터로 제한되어 있다. 일반 펑션 SDK가 관리자 권한을 제공한다고 가정하지 않고 운영 저장소 및 권한 경로를 확인해야 한다.

- [GameData UpdateV2 권한](https://docs.backnd.com/sdk-docs/backend/base/game-information/update/using-indate/)
- [펑션 SDK 지원 범위](https://docs.backnd.com/sdk-docs/function/request/request/)
- [TransactionWriteV2: 최대 10개 쓰기의 전체 성공/실패](https://docs.backnd.com/sdk-docs/backend/base/game-information/transaction/write-v2/)

## 재현 가능한 검사

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Data/DotNetSdk/dotnet.exe' run --project Server/MatchSettlement.Tests/MatchSettlement.Tests.csproj --configuration Release
```

28개 검사 통과: 시작 차감, 중복 요청, 3/4구 전적 분리, 무승부 반환, 연결 만료·복귀·양쪽 단절, 기권, 비참가자 거부, 보고 충돌, 트랜잭션 실패 롤백, 잔액 부족, 숫자 범위, 서로 다른 경기의 동일 사용자 갱신, 상세 기록 삭제 후 재지급 방지. 이는 검사용 저장소에서 확인한 결과이며 실제 뒤끝 권한·동시성·두 기기 왕복 검증은 아니다.

## 아직 연결하지 않은 부분

운영 `ISettlementStore`, 인증을 검증하는 펑션 진입점, 서버 차트 조회, 클라이언트 명령·heartbeat 호출, 예약 만료/삭제, 배포 후 두 계정 검증. 현재 실제 경기에서 이 코드를 통한 입장료 차감과 보상 지급은 실행되지 않는다.
