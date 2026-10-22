# 로컬 API와 실제 게임 클라이언트 통합 검증

2026-10-15. 이 문서는 실제 실행 결과와 미완료 범위를 구분해 기록한다.

## 검사 구성

Unity 개발 빌드 두 개를 별도 프로세스로 실행한다. 각 프로세스는 임의 비밀번호로 `e2e_*` 개발 계정을 생성한다. 비밀번호·인증 토큰을 파일·명령 인자·로그에 저장하지 않는다. 운영 빌드에서는 실행할 수 없으며 `-local-api-e2e`와 기존 `-practice-match-test`를 함께 지정해야 한다. API 주소가 루프백이 아니면 검사하지 않는다.

서버 차트에 기존 행을 덮어쓰지 않고 검사용 경기장 9003(3구, 목표 2점)과 9004(4구, 목표 3점)를 추가했다. 두 경기장은 1분 제한·입장료 1이며 동일한 실제 준비·입장료·정산 API를 사용한다. 재현 SQL은 `Server/GameApi.Tests/Seed-ClientE2E.sql`이다.

클라이언트 경로: Arise → Come에서 새 API 인증 → Consist/Photon 로비 → 방 입장 → 기존 준비 프로토콜 → TableSet07 경기장 → 실제 물리 타격 → 결과 보고 → 서버 정산 조회.

점수·승자·공 상태를 테스트 코드가 강제 주입하지 않는다. 실제 `PracticeMatchAdapter.Submit` 경로로 타격하고 경기 시간 만료 또는 명시적 나가기로 종료한다. 공의 정지 스냅샷 및 점수는 실제 게임 프로토콜에서 가져온다. 이 검사는 공개 Pavilion 경로와 구별되는 기존 개발용 대전 경로를 사용한다.

## 실행 방법

1. 로컬 GameApi를 켠다.
2. Unity에서 `Tools > Magic Online > New Match Test > Build Windows Test Player`를 실행한다.
3. 빌드 성공 후 프로젝트 루트 PowerShell에서 아래 중 하나를 실행한다.

```powershell
./Server/GameApi/Run-ClientE2E.ps1 -Mode rematch
./Server/GameApi/Run-ClientE2E.ps1 -Mode disconnect
./Server/GameApi/Run-ClientE2E.ps1 -Mode reconnect
./Server/GameApi/Run-ClientE2E.ps1 -Mode public
./Server/GameApi/Run-ClientE2E.ps1 -Mode public -HallId 9003
./Server/GameApi/Run-ClientE2E.ps1 -Mode restart
```

- rematch: 4구 정상 종료 후 양쪽 재대전, 다음 경기에서 참여자 기권.
- disconnect: 3구 경기 중 참여자 프로세스 종료, 남은 참가자의 서버 판정 대기.
- reconnect: 3구에서 기존 패킷 손실·복구 검사를 통해 중단/복귀를 관찰하고 정상 종료 대기.
- public: 기존 가입 폼의 입력·가입 버튼 이벤트 → Consist 스와이프 대전 메뉴 → 경기장 카드 이벤트 → 대기실 준비 → Pavilion → 시간 종료 → 결과 화면·정산·재대전 버튼 활성화를 확인한다. 일반 경기 경로를 쓰되 Photon 버전은 개발 검사 전용으로 분리한다. 기본은 4구(9004), `-HallId 9003`은 3구다. 비활성 예전 PnlPrime/Hall 메뉴가 아니라 현재 활성 메뉴를 사용한다.
- restart: 4구 실제 경기 종료 후 A의 결과 파일 저장을 확인하고 전송 전 프로세스를 종료한다. 같은 계정으로 새 프로세스에서 로그인하고 파일 큐를 읽어 재전송한 뒤 무승부 반환과 큐 비우기를 확인한다. A 계정 비밀번호는 실행 도구의 메모리와 자식 프로세스 환경 변수에만 보관하며 파일·명령 인자·로그에는 기록하지 않는다.

출력은 `Logs/LocalApiE2E/{모드}-{실행 ID}/A.json`, `B.json`, 각 로그 및 `result.json`이다. 실행 도구는 이번 호출에서 시작한 테스트 프로세스만 종료한다.

## 사전 검사에서 발견한 문제

기존 `MatchResultProtocolValidation`은 4구도 2점 도달 시 종료를 기대했다. 현재 `MatchConfiguration`의 4구 목표는 3점이므로 검사에 실패해 빌드가 차단됐다. 4구 2점에서는 미종료, 3점에서 종료하는지 검증하도록 수정했다. 실제 게임 규칙을 테스트에 맞춰 바꾸거나 검사를 우회하지 않았다.

## 실제 실행 결과

개발 플레이어 빌드 `PracticeMatchTest-20261015-211246`이 성공했다. 빌드 전 경로·입력·복귀·결과 프로토콜 검사도 통과했다.

4구 `rematch` 검사를 실제 두 프로세스로 통과했다. 두 계정은 출석 후 각각 30코인으로 시작했다.

| 경기 | 종료 | A 코인 | B 코인 | DB 전적 반영 |
|---|---|---:|---:|---|
| `8ce96ed7483d4782be7588d3c9c5851a` | 실제 1회 타격 후 시간 종료, 0:0 무승부 | 30 | 30 | 양쪽 4구 무승부 +1 |
| `b34561627ed44f02856cd35ebd8dedce` | 양쪽 재대전 동의 후 B 기권 | 31 | 29 | A 4구 승 +1, B 4구 패 +1 |

각 경기 입장료는 1이며 무승부 때 각 1 반환, 기권 때 승자에게 2 지급됐다. PostgreSQL의 `settlement_receipts`, `wallets`, `user_stats`를 직접 조회해 클라이언트 결과와 일치함을 확인했다. 양쪽 3구 전적은 모두 0으로 유지됐다.

실행 증빙: `Logs/LocalApiE2E/rematch-d7de0715525843bbba0250ca7bcf18e1/result.json`. 양쪽 타격 정지 해시는 `67374D07BE7ED01225661F69740033C447C1A5342D89B8096C76170CE6AC1C7E`로 일치했다.

정산 완료 전에 재대전을 요청하면 아직 활성 경기 또는 미갱신 잔액과 충돌할 수 있어, 서버 정산 영수증 확인 후에만 재대전 버튼을 활성화하도록 수정했다. 실행 도구는 PowerShell이 JSON 날짜를 DateTime으로 자동 변환하는 경우에도 빌드 최신 여부를 올바르게 판별한다.

3구 `disconnect` 검사도 통과했다. 경기 `8b17874cb3294aea8f3f28d66c492d10`에서 B 개발 프로세스를 경기 시작 5초 후 종료했다. A는 계속 생존 신호를 보내며 대기했고 서버 작업자가 `DisconnectForfeit`으로 정산했다. PostgreSQL에서 A 31코인·3구 승 1, B 29코인·3구 패 1을 확인했다. 양쪽 4구 전적은 0이다. B 보고서의 `Ok=false`는 실패가 아니라 의도한 프로세스 종료 때문에 최종 조회를 못 했다는 뜻이며, `ExitedForDisconnect=true`와 A의 서버 영수증을 함께 검사한다.

증빙: `Logs/LocalApiE2E/disconnect-cf3556a7a9f4436ab95f6facf6de31fe/result.json`.

3구 `reconnect` 검사도 양쪽 `Ok=true`로 통과했다. 경기 `a26e5b738b774a9885e69903071a0c60`에서 B의 Photon 패킷을 25초간 차단했고, 연결 만료 → 같은 방 재입장 → 체크포인트 복귀를 관찰했다. 양쪽이 중단과 복귀를 기록했으며 복구 당시 점수 0:0·차례 1·남은 경기 시간 46.082초가 일치했다. 이후 시간 종료로 `MutualConfirmation` 정산됐고 양쪽 30코인·3구 무승부 1, 4구 전적 0을 DB에서 확인했다. 이 검사는 Photon 통신만 차단하므로 HTTP까지 끊기는 전체 네트워크 장애와는 구별한다.

증빙: `Logs/LocalApiE2E/reconnect-9caf40b2a35540faaeb799a4fc208917/result.json`. 실제 타격 정지 해시는 양쪽 `7687E533C59B9F9A93CA0E7324CDA0A05208226C362D0F58F7C5897AB58583BF`로 일치했다.

세 실행의 보고서를 `LocalApiClientE2E.results.json`에 함께 보관했다. 총 4경기의 서버 정산 및 검사용 6계정의 `wallets.coin = SUM(wallet_ledger.coin_delta)`를 직접 확인했다. 자동 실행이 시작한 테스트 프로세스는 모두 종료했다. 로컬 API와 Unity 편집기는 유지했다.

## 검증 범위의 한계

개발용 대전 검사는 준비·타격·종료·재대전 메서드를 호출한다. 일반 경로 검사는 기존 가입 폼에 값을 설정하고 가입·경기장 카드의 버튼 이벤트를 호출한다. 실제 마우스 클릭 위치·터치 영역·화면 배치 검사는 아니며, 두 물리 기기 및 전체 네트워크 장애 검사는 별도로 남는다. 일반 경기장 검사에서는 초구 흩뿌리기 후 추가 타격 없이 시간 종료를 기다리며, 실제 큐 타격은 앞선 개발용 대전 검사에서 검증했다. 테스트 계정과 경기만 생성하며 기존 사용자 자료를 덮어쓰지 않는다.

## 일반 경기장·재실행 검증 중 발견한 문제

일반 경로 검사에서 `public-70a0fcc12cbb431caaba81a5050a9e4f`의 두 계정은 가입 폼·경기장 카드·시작 승인·Pavilion 진입까지 성공했으나 최종 완료 검사는 통과하지 못했다. 참여자 로그에 Pavilion 리소스 로드가 두 차례 기록되고, 폐기된 UI 및 효과를 만지는 NullReferenceException이 발생했다. 양쪽 HistoryCommit 이후 수동 씬 로드와 PUN 자동 씬 동기화가 중복되는 원인이었다.

`PunNetwork`의 자동 씬 동기화를 끄고 기존 시작 승인 경로로만 로드하도록 수정했다. `ChkBox01`은 오래된 애니메이션과 씬 폐기 후 작업을 종료하며, `EffCtrl`은 비활성화·풀 반환 시 지연 작업을 취소한다. 새 검사에서는 클라이언트마다 Pavilion 로드가 1회인지 확인하고, 처리되지 않은 예외가 있으면 성공으로 판정하지 않는다. 수정 후 실행 결과는 아래에 별도로 기록한다.

수정 후 `public-56760cf0346842e6828096cbfc5c9a74`에서 양쪽 가입 폼·현재 경기장 카드·Pavilion 결과 화면·정산·재대전 활성화가 통과했다. 경기 `fe97f8fbff6f49d892aad71c35f2f7c8`은 4구 정상 무승부이며 양쪽 30코인이다. 로그에 양쪽 `PavilionLoads=1`을 확인했고 처리되지 않은 예외도 없었다.

추가 검토에서 API 프로필을 다시 읽어도 `NetworkManager`·`PoolPlayer`의 코인 표시 캐시가 갱신되지 않는 경로를 수정했다. 자신의 서버 프로필을 읽을 때 로비의 입장 가능 잔액과 게임 내 자기 카드 캐시를 함께 갱신한다. 결과 화면은 프로필 갱신을 구독하고 상대의 공개 프로필도 다시 읽는다. 새 검사는 입장료 차감 후 29코인 캐시와 무승부 반환 후 30코인 결과 라벨을 확인한다. 클라이언트에서 코인을 계산해 지급하지 않고 서버 응답만 표시한다.

## 최종 빌드의 일반 경기장 검사

빌드 `PracticeMatchTest-20261015-215339`에서 두 검사 모두 양쪽 `Ok=true`, `UsedLoginForm=true`, `UsedPublicLobby=true`, `SawOutcome=true`, `RematchEnabled=true`로 통과했다. 각 클라이언트에서 입장 후 서버 잔액 및 표시 캐시 29코인, 결과 화면 자기 카드 30코인, Pavilion 1회 로드와 처리되지 않은 예외 없음을 확인했다.

| 종목 | 경기 ID | 정산 | 최종 코인 | 실행 증빙 |
|---|---|---|---|---|
| 4구 | `043d621991a44970bad9dd2be6a17189` | MutualConfirmation 무승부 | A/B 각 30 | `public-4886f7a2b55f42e58f813cf06dfd9a78` |
| 3구 | `2191ed711c2d4704937351e426cff9fe` | MutualConfirmation 무승부 | A/B 각 30 | `public-b74b80d33d98413796cb1880cbec1ad5` |

증빙 디렉터리는 `Logs/LocalApiE2E/` 아래다. Unity 편집기의 BackendGame 스크립트도 Reimport로 다시 컴파일했고 편집기 어셈블리에서 `SyncDisplayedCoin`·`RefreshServerCards`의 반영을 확인했다. Reimport가 내용 변경 없이 파일 시각만 바꾼 경우, 실행 도구는 플레이어 PDB의 SHA256 소스 체크섬과 현재 파일을 대조한다. 실제 내용이 달라지거나 체크섬을 확인할 수 없으면 새 빌드를 요구한다.

## 앱 재실행 후 결과 큐 복구 통과

동일 최종 빌드로 `restart-0b21d1c5e3c341aaa495c198b35e2861`을 실행했다. 4구 경기 `8df7979828474fc09c8ed1d163f0c121`에서 실제 타격 후 0:0 시간 종료를 기다렸다. A는 결과를 파일 큐에 저장하되 전송을 보류했다. 종료 전 보고서에 `StagedForRestart=true`, `PendingBeforeRestart=1`과 서버 미정산 상태를 확인하고 A 프로세스를 종료했다.

실행 도구가 새 A 프로세스를 시작해 같은 계정으로 다시 로그인했다. 파일 큐 1건 복구 → 재전송 → `Applied`/`MutualConfirmation` → 큐 0건을 확인했고 양쪽 `Ok=true`로 종료됐다. 최종 A 보고서는 `Resumed=true`, `PendingBeforeRestart=1`, `PendingAfterRestart=0`, `Coin=30`이다. 해당 사용자·API 주소에 대응하는 실제 `ApiResults-*.json` 파일도 `Writes=[]`인 것을 직접 확인했다.

일반 4구·일반 3구·재실행 복구 3경기를 PostgreSQL에서 조회했다. 각 경기 영수증 1개, 공통 기록 1개, 개인 기록 2개, 입장/정산 원장 4개이며, 6계정 모두 코인 30·원장 합계 일치였다. 각 계정에는 해당 종목의 무승부만 1회 반영되고 다른 종목 전적은 0이다. 보고서·종료 전 상태·DB 조회 결과는 `LocalApiPublicAndRestart.results.json`에 보관했다.

서버 중단 또는 전체 네트워크 단절을 동반한 장기 미전송, 다른 계정으로 로그인했을 때의 큐 격리, 파일 손상 복구와 공개 경기장의 승리·기권 UI는 이번 세 검사와 구별되는 추가 검증 대상이다. 두 물리 기기 실전과 운영용 결과 검증·분쟁 정책도 남아 있다.
