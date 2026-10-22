# 경기 결과 저장 작업 진행 기록

## 2026-10-15 로컬 PostgreSQL 전환

뒤끝 인증·저장 호출을 로컬 C# API로 교체하고 PostgreSQL 정산 저장소를 구현했다. 실제 HTTP·DB 검사 43개, Unity HTTP 스모크 검사와 Arise→Come 로그인 화면 진입을 확인했다. 실행은 Server/GameApi/README.ko.md, 구현·검증·남은 범위는 Docs/LocalDatabaseMigrationProgress.ko.md를 따른다. 아래 뒤끝 서버 검사 기록은 과거 경로의 기록이며 새 경로의 운영 완료를 의미하지 않는다.

## 본인 데이터 수정·조회 실서버 성공

사용자 보고: ProbeWriteSelf elapsedMs=737 / HTTP 200 / 함수 Ok=true / 내부 Status=204. 뒤이은 ReadSelf elapsedMs=209 / HTTP 200 / Ok=true / RowId=2026-10-08T14:08:56.589Z / Counter=1 / Version=1 / LastRequestId=SelfWrite. 본인 데이터 수정과 조회 및 ISO 행 ID 유지가 실제 호출에서 확인됐다. 앞선 Seed 보고와 이번 RowId의 시각이 다르므로 두 보고가 동일 RunId라는 점까지 입증한 것으로 기록하지 않는다. 양쪽 사용자 갱신·트랜잭션 롤백·동시 요청과 운영 정산은 아직 미검증이다.

## Seed 성공 후 수정값 미변경 조사

사용자 관찰: Seed 1070ms/200/Ok=true, 마지막 ReadSelf 327ms/200/Ok=true이나 Counter=0, Version=0. WriteSelf 응답은 미제공이므로 수정 실패 원인 전체를 확정하지 않는다. RowId가 `10/8/2026 1:57:58 PM`으로 표시되는 버그는 확인했다. 저장 응답 JObject.Parse의 날짜 자동 변환으로 inDate의 ISO 형식·밀리초가 손실됐다. DateParseHandling.None으로 저장 응답을 읽도록 수정하고 ISO 문자열·서로 다른 밀리초 유지 회귀 검사를 추가해 39개 통과했다. 공식 재빌드 성공. 기존 같은 RunId에서 ReadSelf → WriteSelf → ReadSelf 재검사가 필요하며 Seed를 재호출하지 않는다.

## 저장 검사 준비

2026-10-08 콘솔에서 FunctionProbePublic(public) 및 FunctionProbePrivate(private)를 활성·스키마 미정의로 생성하고 목록에서 확인했다. 진단 코드가 RunId/Slot/Counter/Version/LastRequestId를 입력한다. 기존 UserMain의 gamer_id와 owner_inDate를 대조하여 user02/user04의 식별값을 확인하고 두 계정만 진단 요청을 허용하도록 설정했다. 공식 빌드 성공. 실제 Seed 및 이후 숫자 변경 결과는 아직 미확인이다. 일반 참가자 및 운영 코인·전적 테이블을 수정하는 경로는 추가하지 않았다.

## 수정 배포 후 Health 실제 성공

사용자가 Consist 검증 창의 2 Health를 다시 실행한 결과: elapsedMs=5524, status=200, accountChanged=false, 함수 본문 Ok=true / Code=ReadyForIntegration / ApiVersion=1 / StorageConnected=false / SettlementEnabled=false. 서버 호출 및 서버 API를 통한 사용자 식별 경로가 성공했다. 5,524ms는 한 번의 클라이언트 왕복 관찰이며 정상 평균 지연·콜드 스타트 원인으로 단정하지 않는다. 저장·양쪽 갱신·정산 가능 여부는 아직 미검증이다. 다음은 전용 테스트 계정과 FunctionProbePublic/FunctionProbePrivate 테이블을 준비하고 진단 쓰기 허용 목록을 설정하는 단계다.

## 2026-10-08 배포 오류 해결 및 진단 함수 배포

배포 후 사용자 Health 관찰: HTTP 200, 2976ms, 함수 응답 Unauthenticated. 공식 SDK Initialize 구현은 요청 자격 정보와 내부 API를 준비하지만 UserInDate를 채우지 않아 진입점의 초기 가정이 잘못됐다. 진입점에서 BMember.GetUserInfo를 호출해 서버가 조회한 row.inDate를 호출자 식별값으로 사용하도록 수정했다. 요청 본문의 사용자 값은 사용하지 않는다. 이미 JSON 문자열인 응답을 JsonToStream으로 다시 감싸던 부분도 StringToStream으로 수정했다. 공식 CLI 빌드 통과. 수정 후 실제 Health 성공 여부는 재호출로 확인해야 한다.

사용자가 실행한 deploy는 CLI 설정의 예제 경로 `C:\fileName\BackendFunction`에서 publish.zip을 찾다 실패했다. 설치된 공식 Backend.exe와 인증키 설정을 확인했고, 인증키는 보존한 채 projectPath를 현재 저장소의 Server\BackendFunction, functionName을 MatchSettlementProbe로 수정했다. 공식 `backend build`로 `bin\Release\net8.0\linux-x64\publish.zip` 생성 성공, `backend deploy`에서 서버 응답 StatusCode 200 / Message OK 및 함수명 MatchSettlementProbe를 확인했다.

이로써 공식 빌드·배포 선행 작업은 완료됐다. 현재 ProbeSettings.Enabled=false이므로 실제 데이터 쓰기는 비활성이다. 다음은 Unity Health 호출로 함수 진입·응답을 확인하고, 전용 계정·테이블을 연결해 저장 검사를 진행하는 것이다. 아래 과거 미설치·미배포 상태 기록은 당시 결과다.

2026-10-08. 8단계 전체 수행 요청에 따라 배포 선행 조건부터 점검했다. 단계 번호는 MatchResultImplementationPlan의 A~H에 대응한다.

| 단계 | 상태 | 확인 결과 / 다음 완료 기준 |
| --- | --- | --- |
| 1. 펑션 배포·호출 | 진행 중, 사용자 환경 작업 필요 | CLI 미설치, CLI 설정 없음, Unity 펑션 인증키 미설정. 콘솔 로그인 세션 만료. 설치 파일 서명 검증 완료, 실행 실패 |
| 2. 저장·권한·동시성 | 선행 단계 대기 | 전용 테스트 계정·테이블로 실제 호출 후 양쪽 갱신·롤백·중복 승인 검사 |
| 3. 운영 저장소 | 2단계 결과 대기 | 기존 ISettlementStore 계약을 충족할 저장소·권한 경로 확정 후 구현 |
| 4. 시작 기록·차감 | 서버 규칙 구현, 운영 연결 대기 | 실제 두 계정 Ready 후 한 번 차감 및 Playing 저장 |
| 5. 종료 저장·지급 | 서버 규칙 구현, 운영 연결 대기 | 결과 합의 후 Coin·종목별 전적·원장·기록 동시 반영 |
| 6. 나가기·끊김 | 서버 규칙 구현, 게임 호출·예약 평가 대기 | 두 기기 나가기·복귀·한쪽/양쪽 만료 검사 |
| 7. 상세 기록 삭제 | 정책·메모리 검증 준비, 운영 예약 삭제 대기 | 실제 저장소에서 만료 기록 정리, 누적 전적·원장 유지 |
| 8. 최종 검증 | 1~7 완료 후 수행 | 두 계정·두 기기의 이전/이후 데이터와 중복 요청 검사 기록 |

## 이번에 수행한 작업

- 공식 다운로드 주소에서 Setup.exe를 내려받아 디지털 서명 `Valid`, 게시자 `AFI, INC.`를 확인했다.
- PowerShell에서 설치 실행을 시도했지만 Windows가 `지정된 경로를 찾을 수 없습니다`로 실행을 거부했다. 파일 존재는 확인했다. 정확한 원인은 아직 판정하지 못했다. 설치 완료·재부팅·배포 성공으로 기록하지 않는다.
- 설치 파일 위치: `C:\Users\oyh52\AppData\Local\MagicOnline2\BackendFunctionTools\1.0.2\Setup.exe`.
- BackendFunction.csproj에 공식 템플릿의 Lambda 프로젝트 표시와 publish.zip 생성 설정을 반영했다. 이 설정만으로 공식 CLI 빌드를 수행한 것은 아니다.
- Check-DeploymentPrerequisites.ps1을 추가·실행했다. CLI, CLI 설정, CLI 인증키, 프로젝트/함수명 일치, Unity 인증키 점검 결과 모두 false다. 인증키 값은 출력하지 않는다.
- 로컬 .NET 빌드는 경고 0개·오류 0개로 통과했다. 실서버 배포와 구분한다.
- 로컬 검사 38개를 재실행해 모두 통과했다. 테스트 출력도 운영 저장소와 배포가 미설정임을 명시한다. 실제 두 기기·뒤끝 저장 검사는 아니다.
- 콘솔에 접근하자 로그인 페이지로 이동했다. 2026-10-07의 함수 0개 관찰 이후 현재 목록은 로그인 전이므로 재확인하지 못했다.

## 사용자가 필요한 최소 작업

추가 확인: 2026-10-08 사용자 첨부 Inspector 화면과 현재 프로젝트 설정 파일에서 Function Auth Key 입력을 확인했다. Check-DeploymentPrerequisites 재실행에서 UnityFunctionKeyConfigured=true다. 값 자체는 출력하지 않았다. CLI 및 CLI 설정은 여전히 미확인 상태다. Unity 키 입력·저장 선행 조건은 완료됐지만 유효성은 실제 호출로 검증해야 한다.

후속 확인: 2026-10-08 재로그인 후 magicCushion 프로젝트 최고관리자 접근을 확인했다. 펑션 페이지는 활성 상태이며 등록된 함수 없음·호출 횟수 0이다. 로그인 선행 조건은 해결됐다. 배포 준비 스크립트 재실행 결과 CLI·CLI 설정·Unity 펑션 인증키는 여전히 미설정이다. 아래 로그인 작업은 완료됐으며 설치·재부팅 확인이 남았다.

1. 열어 둔 뒤끝 콘솔에 로그인한다. 비밀번호나 인증키를 채팅에 보내지 않는다.
2. 위 공식 설치 파일을 직접 실행해 개발 도구를 설치한다. 설치 과정에서 Windows 승인 화면이 있다면 직접 처리한다. 공식 안내에 따라 작업을 저장하고 재부팅한다. 재부팅은 자동으로 실행하지 않았다.
3. 설치·로그인 완료를 알려 주면 CLI 재점검 → 진단 함수 공식 빌드·배포 → Unity Health → 테스트 저장 순서로 이어간다. 인증키 연결은 콘솔 접근 후 진행한다.

아직 이 선행 조건 때문에 운영 Coin·전적 연결이나 실제 저장 검사를 실행하지 않았다. 이 상태에서 8단계 완료로 표시하거나 검사용 메모리 저장소를 운영에 연결하지 않는다.

## 두 기기 최종 검사 기록 양식

| 검사 | 종목 | 기기 A/B 씬·버튼 | MatchId | 시작 전 Coin | 차감 후 Coin | 종료 후 Coin | 전적 증가 | 원장/기록 개수 | 판정 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 정상 승리 | 4구 | 미실행 | | | | | | | |
| 정상 승리 | 3구 | 미실행 | | | | | | | |
| 무승부 | 두 종목 | 미실행 | | | | | | | |
| 명시적 나가기 | 두 종목 | 미실행 | | | | | | | |
| 끊김 후 유예 내 복귀 | 두 종목 | 미실행 | | | | | | | |
| 한쪽 연결 만료 | 두 종목 | 미실행 | | | | | | | |
| 양쪽 연결 만료 | 두 종목 | 미실행 | | | | | | | |
| 결과 불일치 | 두 종목 | 미실행 | | | | | | | |
| 잔액 부족·중복·응답 손실 | 두 종목 | 미실행 | | | | | | | |
| 상세 기록 삭제 후 재요청 | 두 종목 | 미실행 | | | | | | | |

공식 참고:

- [설치·재부팅 안내](https://docs.backnd.com/sdk-docs/function/intro/)
- [CLI 설정](https://docs.backnd.com/sdk-docs/function/cli/setting/)
- [공식 CLI 빌드](https://docs.backnd.com/sdk-docs/function/cli/build/)
- [배포](https://docs.backnd.com/sdk-docs/function/cli/deploy/)

## 2026-10-09 직접 실행: 두 계정 공개 데이터 갱신 성공

Unity Come 로그인 → Consist 진단 창에서 user02/user04를 같은 Run ID로 준비하고 Write pair를 한 번 실행했다. 두 계정 Counter가 0에서 1로 증가했다. 요청 계정 ID 날짜 변환과 GetV2 단일 row 파싱을 수정·재배포했고 로컬 41개 검증이 통과했다. 상세 실행 순서·행 ID·증거는 FunctionProbePairResult.ko.md에 기록했다. 운영 정산 활성화 및 경쟁·롤백 검사는 아직 완료되지 않았다.


## 2026-10-09 직접 실행: 롤백·조건부 경쟁 요청

Consist 진단 창의 8 Rollback 검사에서 정상 두 행은 모두 0으로 유지됐다. 같은 버전의 네 요청은 한 개 성공·세 개 실패했고, 저장된 Counter/Version은 1/1이었다. 이전 버전 재시도도 실패하고 값이 유지됐다. 상세 결과는 FunctionProbeRollbackConcurrency.ko.md에 기록했다. 다음은 두 계정과 경기 상태의 버전 조건을 함께 묶는 트랜잭션 및 비공개 기록 권한 검사다. 운영 정산은 계속 비활성 상태다.


## 2026-10-09 두 계정·경기 상태 조건부 갱신: 선행 조건 미충족

11 Atomic pair + match state suite를 추가·배포하고 Consist에서 직접 실행했다. 본인 두 행의 Where/Version 대조 갱신은 204 성공, 상대 행의 동일 조건 방식은 404 실패했다. 정상 세 행 갱신도 404로 실패했으며 값은 1/1, 1/1, 0/0으로 유지됐다. 이 검사를 완료 성공으로 표시하지 않는다. FunctionProbeAtomicPair.ko.md에 근거와 로그를 기록했다. 운영 연결 전에 타인 행 조건부 갱신의 공식 서버 API 또는 서버 권위 저장소를 확인해야 한다. 운영 정산 비활성 유지.


## 2026-10-09 공식 서버 API·저장소 경로 조사

설치 공식 SDK와 참조 DLL의 SHA256이 동일함을 확인했다. GameData/PlayerData 공개 메서드에 상대 행 ID와 버전 조건을 동시에 받는 경로를 찾지 못했고, Database 펑션 지원 전환을 확정하는 공식 서버 SDK 안내도 확인하지 못했다. BackendSettlementStorageReview.ko.md에 확인 범위와 경로별 판단을 기록했다. 기술지원 최소 재현 질문은 Server/BackendSettlementSupportRequest.ko.md에 작성했으며 전송하지 않았다. 현재 운영 연결의 선행 조건은 공식 지원 경로 확인이며 운영 정산 비활성 상태를 유지한다.

## 2026-10-09 기술지원 문의 등록 완료

사용자의 전송 승인 및 로그인 완료 후 공식 커뮤니티 문의 게시판 > 펑션에 등록했다. 접수 글은 https://community.thebackend.io/t/sdk/12074 (게시글 12074)이며 등록된 제목과 본문을 확인했다. 인증키·토큰·실제 사용자 식별자는 제외했다. Docs/BackendSupportSubmission.png에 증빙을 저장했다. 담당자 답변을 바탕으로 두 계정 조건부 갱신과 서버 권위 저장소의 공식 구현 경로를 판단할 예정이다. 운영 정산은 비활성 상태를 유지한다.

## 2026-10-15 로컬 PostgreSQL 전환 및 실제 대전 정산

이전 뒤끝 검증 이력은 위에 보존한다. 현재 구현은 뒤끝 로그인·펑션 대신 로컬 GameApi와 PostgreSQL을 사용한다. 기반 구현·43개 HTTP/DB 검사는 `LocalDatabaseMigrationProgress.ko.md`, 실제 Unity 두 클라이언트의 4구 종료·재대전·기권 및 3구 이탈·복귀 정산 통과 결과는 `LocalApiClientE2E.ko.md`에 정리했다. Railway 운영 배포는 아직 하지 않았다.

일반 Consist 경기장 카드에서 4구/3구의 Pavilion 결과 화면·코인 표시·재대전 활성화도 두 로컬 프로세스로 통과했다. 중복 씬 로드·폐기 후 비동기 UI 작업·코인 표시 캐시를 수정했다. 경기 종료 후 미전송 결과 1건을 파일에 저장한 채 앱 종료 → 새 프로세스에서 동일 계정 로그인 → 재전송·정산 → 메모리/파일 큐 0건을 확인했다. 최종 3경기의 DB 영수증·기록·종목별 전적·원장 증빙은 `LocalApiPublicAndRestart.results.json`에 보관했다. 두 물리 기기와 운영용 결과 검증은 추가 작업 대상이다.
