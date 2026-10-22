# 뒤끝 펑션 실서버 검증 실행 방법

최신 상태(2026-10-08): Health 실제 성공 후 FunctionProbePublic/Public 및 FunctionProbePrivate/Private 테이블을 스키마 미정의로 생성했다. 필드는 진단 Insert가 입력한다. user02/user04만 허용하는 ProbeSettings.Enabled=true 진단 버전을 공식 빌드·배포했다. 아래 기본 비활성·미배포 설명은 최초 준비 당시 상태이며 현재 저장 검사는 아직 실제 값을 확인하기 전이다. 검사 종료 후 Enabled=false로 다시 배포한다.

이 소스는 실제 경기 정산이 아니라 펑션 지원 범위를 확인하기 위한 진단 도구다. 실제 Coin·승패·경기 기록을 변경하지 않는다. 자동 실행하지 않으며 현재 배포되지 않았다.

## 준비

### 씬과 화면 버튼으로 실행

Unity에서 `Tools > Magic Online > Backend Function Probe > Run from Arise`를 선택한다. `Assets/Scenes/Arise.unity`에서 Play를 시작하고 기존 흐름대로 Come 로그인 씬으로 이동한다. Come에서 기존 테스트 로그인 버튼으로 로그인하면 Consist로 이동하며 검증 창은 유지된다.

검증 창의 `1 Generate Run ID` → `2 Health` → `3 Seed` → `4 Read self` → `5 Write self` → `4 Read self` 순서로 실행한다. 정상 배포·테이블·권한이 준비된 경우에만 Seed 후 Counter=0/Version=0, Write self 후 Counter=1/Version=1을 확인할 수 있다. 이는 기대 결과이며 아래 실제 실행 결과와 구분한다.

검증 창이 보이는 게임 메뉴 씬에서는 기존 UI 입력을 차단하여 검증 버튼 클릭이 게임 버튼으로 전달되지 않게 한다. Come 로그인 씬은 로그인을 위해 입력을 허용한다. Hide probe window 또는 실행 종료 시 차단을 해제한다. 다시 표시하려면 `Tools > Magic Online > Backend Function Probe > Show window in Play Mode`를 선택한다. 씬 파일이나 빌드 씬 목록을 수정하지 않고 실행 중에만 검증 오브젝트를 생성한다.

### 2026-10-07 실제 Unity 실행 결과

입력 차단 수정 후 같은 경로로 재실행했다. Consist에서 Health는 10ms, Seed는 1ms 뒤 `status=400 errorCode=BadRequestException`을 반환했다. 새 RunId는 `3ebb97a9e46b46e1902b92ac2022258a`이며 로컬 생성 값이다. Seed 클릭 후에도 뒤의 연습 메뉴가 열리지 않아 클릭 전달 문제는 해결된 것을 확인했다. 데이터 생성 성공 응답은 없으며 Counter/Version 변경 검증은 아직 수행할 수 없다. 실행을 종료해 Arise로 복귀했다. 화면 증거: `FunctionProbeHealth400.png`.

| 실행 위치 | 클릭한 버튼 | 관찰 결과 | 데이터 변화 |
| --- | --- | --- | --- |
| Arise 시작 메뉴 | Run from Arise | Play 실행 후 Come으로 이동 | 검증 데이터를 저장하지 않음 |
| Come 검증 창, 로그인 전 | 2 Health | 초기화/로그인 조건 미충족으로 호출 차단 | 서버 요청 없음 |
| Come 로그인 화면 | user02 로그인 | 로그인 성공, Consist로 이동 | 기존 로그인 처리만 실행 |
| Consist 검증 창 | 2 Health | 약 18ms, SDK status=400 | 펑션 검증 데이터 저장 없음 |
| Consist 검증 창 | 1 Generate Run ID | 로컬 RunId 생성 | 서버 데이터 변화 없음 |
| Consist 검증 창 | 3 Seed | 약 4ms, SDK status=400 | Primary/Audit 생성 성공을 확인하지 못함 |

뒤끝 콘솔에서 함수 목록에 `등록된 함수가 없습니다.`와 함수 호출 횟수 0을 확인했다. 이 결과는 펑션 정산 미지원 판정이 아니라 배포·호출 설정이 아직 준비되지 않았다는 상태다. HTTP 400의 정확한 원인은 기존 로그에 오류 상세가 없으므로 단정하지 않는다. 후속 소스에서는 errorCode도 기록한다.

추가 확인: Unity `Assets/TheBackend/Resources/TheBackendSettings.asset`의 functionAuthKey가 비어 있다. 값 자체를 출력하지 않고 설정 유무만 확인했다. 공식 배포 CLI도 현재 PATH에서 확인되지 않았다. 따라서 인증키 설정과 공식 도구를 통한 함수 배포가 다음 실행의 선행 조건이다. 이번 작업에서는 인증키를 코드/로그에 복사하거나 저장하지 않았다.

실제 사용자 코인·승패를 변경하는 진단 명령은 없다. 두 계정 갱신, 롤백, 동시 저장의 실서버 검사는 아직 실행되지 않았다.

1. 실제 플레이어와 분리된 뒤끝 테스트 계정 A/B를 준비한다.
2. **기존 베이스의 게임 정보**에서 `FunctionProbePublic`(Public)과 `FunctionProbePrivate`(Private)를 생성한다. 새 데이터베이스 서비스에 만드는 테이블이 아니다. 테스트 계정에서 읽기·쓰기 가능한 설정을 사용하고 운영 테이블 권한은 바꾸지 않는다.
3. 두 테이블의 스키마는 동일하다.

| 필드 | 자료형 | 의미 |
| --- | --- | --- |
| RunId | string | 매 검사 묶음의 GUID N 형식 |
| Slot | string | Primary 또는 Audit |
| Counter | int | 테스트 숫자, 최초 0 |
| Version | int | 조건부 저장 검사용 숫자, 최초 0 |
| LastRequestId | string | 마지막 요청 ID |

4. `Server/BackendFunction/ProbeSettings.cs`의 Enabled를 true로 바꾸고 AllowedUserInDates에 테스트 계정 두 개의 뒤끝 UserInDate를 등록한다. 기본 설정에서는 ProbeDisabled로 거부한다. 인증키나 비밀번호를 이 파일에 넣지 않는다.
5. 공식 펑션 개발 툴로 `Server/BackendFunction`을 `MatchSettlementProbe`라는 별도 진단 함수로 배포한다. 함수 인증키는 공식 도구/SDK 설정으로 지정하고 저장소에 커밋하지 않는다. SDK DLL 경로는 FunctionSdkDir으로 변경할 수 있다. .NET 로컬 빌드 통과는 실제 배포 성공을 의미하지 않는다.
6. Unity의 테스트 GameObject에 BackendFunctionProbeRunner를 붙인다. 에디터 또는 Development Build에서만 코드가 포함된다. 로그인 후 Inspector에서 함수 이름을 확인하고 컴포넌트의 ContextMenu `Probe/...` 명령을 실행한다.

## 검사 순서 및 판정

### 1. 호출과 응답 시간

Health를 한 번 실행하고 로그의 elapsedMs·status·reply를 기록한다. 곧바로 다시 호출한 결과와 몇 분 동안 호출하지 않은 뒤 첫 호출을 비교한다. 시간 측정은 클라이언트 왕복 시간이며 펑션 처리 시간만 측정한 값이 아니다. 자동 워밍업·주기 호출은 구현하지 않았다.

Health의 Ok=true는 진단 진입점 호출 성공이다. SettlementEnabled=false는 의도된 값이며 실제 정산 가능 판정이 아니다.

### 2. 본인 읽기·쓰기

A에서 Generate Run ID를 실행하고 같은 RunId를 B에 복사한다. Public 상태에서 양쪽이 Seed를 한 번씩 호출한다. 반환되는 PrimaryRowId를 서로 교환해 OtherUserInDate/OtherPrimaryRowId에 입력한다. 두 계정 모두 ReadSelf Counter=0·Version=0인지 확인한 다음 A의 WriteSelf와 ReadSelf로 1이 되는지 확인한다.

Seed는 재호출 시 기존 행을 초기화하지 않는다. 일부 삽입 실패나 중복 행이면 새 RunId로 다시 검사한다. 이 초기화 경로의 중복 고유성은 정산 방식으로 사용하지 않는다.

### 3. 두 계정과 Private 권한

A에서 ReadPeer → WritePair를 호출한다. 양쪽 계정에서 ReadSelf를 실행해 각각 Counter/Version이 1 증가했는지 확인한다. Public에서 성공하더라도 관리자 권한 또는 안전한 재화 쓰기를 보장하는 것은 아니다.

UsePrivateTable=true로 바꾸고 새로운 RunId로 양쪽 Seed 후 같은 순서를 반복한다. 상대 Private 조회가 거부되면 WritePair도 진행하지 않는다. 반환 status와 어느 단계에서 거부됐는지 기록한다. Private 조회 실패가 곧 모든 쓰기 API의 실패를 입증하는 것은 아니지만 이 구현 경로는 사용할 수 없다는 근거가 된다.

### 4. 전체 실패 확인

Rollback은 Primary/Audit 두 행 수정과 존재하지 않는 세 번째 행 수정을 한 트랜잭션으로 요청한다. 서버가 세 번째 행 조회를 404로 응답했을 때만 수행한다. 작업 요청의 Ok=false와 RollbackObserved=true, 두 행의 before=after를 확인한다. 이 검사는 특정 실패 요청에서 변화가 없었다는 관찰이며, 모든 서버 장애·응답 손실 상황의 보장을 증명하지 않는다.

### 5. 동일 요청의 조건부 경쟁·재시도

Public의 **새 RunId**로 Seed하고 ReadSelf의 Version=0을 확인한다. ExpectedVersion=0, ConcurrentRequests=4로 Concurrent identical conditional requests를 실행한다. 요청은 동일 RunId·기대 버전·RequestId를 사용하고 SendQueue를 거치지 않는다. 로그가 모두 끝난 뒤 ReadSelf를 호출한다.

판정에는 함수 응답 본문의 Ok를 사용한다. InvokeFunction의 HTTP/SDK 성공 status만 세면 안 된다.

- 기대 관찰: 저장 함수 Ok=true는 정확히 1개, 나머지는 실패; 최종 Counter=1·Version=1.
- Ok=true가 여러 개면, 최종 값이 1이어도 조건부 쓰기가 한 번만 승인된 것으로 판단하면 안 된다.
- 성공 0개면 Where 기반 쓰기 미지원·조건 오류 등 실패 원인을 확인한다. 중복 방지 통과가 아니다.
- Retry old version을 실행한 뒤 함수 Ok=false와 Counter/Version이 그대로인지 확인한다.

횟수가 적은 경쟁 검사를 통과해도 모든 경쟁을 보장하지 않는다. 실제 정산에는 고유 MatchId 등록, 서로 다른 경기의 동일 사용자 갱신, 권한 제한, 부분 실패 복구까지 별도 검증이 필요하다. 본 검증 코드를 그대로 정산 코드로 사용하지 않는다.

## 결과 기록 양식

| 항목 | SDK status | 함수 Ok/Code | 왕복 ms | 이전/이후 값 | 판단 |
| --- | --- | --- | --- | --- | --- |
| 첫 Health / 반복 / 휴지 후 | | | | | |
| 본인 읽기·쓰기 | | | | | |
| Public 상대 조회·양쪽 갱신 | | | | | |
| Private 상대 조회·양쪽 갱신 | | | | | |
| 실패 트랜잭션 | | | | | |
| 동시 요청 4개 / 이전 버전 재시도 | | | | | |

검사가 끝나면 ProbeSettings.Enabled=false로 바꾸고 진단 함수의 사용을 중지한다. 소스에는 테스트 테이블 삭제 기능을 넣지 않았다. 운영 함수·권한·사용자 데이터를 변경하지 않는다.
