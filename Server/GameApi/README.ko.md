# 로컬 게임 API

Unity → ASP.NET Core API → PostgreSQL 구조다. 뒤끝 인증과 저장 API를 사용하지 않는다. Photon 대전 통신은 유지한다.

다른 PC의 최초 설치·DB 생성·테스트 계정·백업 복원은 [로컬 DB 설정 안내](../../Docs/LocalDatabaseSetup.ko.md)를 따른다.

## 실행

PowerShell을 프로젝트 루트에서 연다.

```powershell
dotnet build Server/GameApi/GameApi.csproj
./Server/GameApi/Start-Local.ps1
```

주소: `http://127.0.0.1:5080/health`. 실행한 PowerShell에서 Ctrl+C로 종료한다.
현재 PC에는 `ConnectionStrings:GameDb`를 .NET 개발용 user-secrets에 설정했다. 비밀번호는 저장소·Unity에 넣지 않았다. 다른 PC는 다음 명령을 실행하고 실제 값을 직접 지정한다.

```powershell
dotnet user-secrets set 'ConnectionStrings:GameDb' 'Host=localhost;Port=5432;Database=magicCushion;Username=magicCushion;Password=<로컬 비밀번호>' --project Server/GameApi/GameApi.csproj
```

첫 시작 시 체크섬으로 관리되는 Migrations SQL을 트랜잭션으로 적용한다. 기존 스크립트를 수정하지 않고 다음 번호의 파일을 추가한다. 기존 차트가 있으면 시드로 덮어쓰지 않는다. `--migrate-only`로 DB 구성만 실행할 수 있다.

## Unity 실행

1. API 서버를 먼저 실행한다.
2. Arise 씬에서 Play를 실행하면 Come 로그인 화면으로 이동한다.
3. ID(영문·숫자·밑줄 3~32자)와 비밀번호(8~128자)를 입력하고 기존 가입 버튼을 누르면 새 계정을 생성하고 로그인한다. 기존 뒤끝 계정은 자동으로 옮기지 않았다.
4. 기존 계정은 로그인 버튼을 누른다. 성공 후 서버 차트·출석·프로필을 읽고 Photon에 연결한다.
5. 경기 결과 화면의 정산 문구를 확인한다. 완료된 후 코인·전적을 서버에서 다시 읽는다. 재시도 중이면 잔액을 임의로 올리지 않는다.

`Assets/Resources/GameApiConfig.json`으로 API 주소를 설정한다. 게스트·뒤끝 해시키 버튼은 비활성이다. 테스트용 로그인1(user01)·로그인2(user02)는 비밀번호 `11111111`을 자동 입력하고 로그인한다. 새 DB에서는 먼저 해당 계정을 가입해야 한다. 현재 인증 토큰은 메모리에만 있어 앱 재시작 시 다시 로그인한다. 결과 재전송 큐는 API 주소·사용자 ID별 파일로 분리되어 재로그인 후 복구된다.

## 주요 API

- 공개: `GET /health`, `POST /api/auth/register`, `/login`, `/refresh`
- 인증 필요: `POST /api/auth/logout`, `GET /api/me`, `PATCH /api/me/profile`
- 공개 게임 프로필(인증 필요): `GET /api/players/{id}` — 닉네임·아바타·전적·게임 코인 표시값. Jem·로그인 ID·인증정보는 제외하며 코인은 수정 불가
- 설정: `GET /api/charts/CommonCode`, `/SkillMatch`, `/Attendance`
- 출석: `POST /api/me/attendance` — 한국 날짜 기준, 1=Coin/2=Jem. 현재 개발 시드 해석이며 실제 운영 보상 코드와 대조 필요
- 경기: `POST /api/matches` (`RequestId`, `OpponentUserId`, `HallId`)
- 준비·생존·결과: `POST /api/matches/{id}/ready`, `/heartbeat`, `/result`, `/surrender`, `/disconnect`
- 조회: `GET /api/matches/{id}`, `GET /api/me/history`

등록 RequestId는 재시도 식별용 GUID N 형식이다. 상대 ID·방 번호만 받으며 참가자 동의와 서버의 차트·지갑으로 시작을 결정한다. 상대방 요청이 있어도 본인 인증으로 준비하기 전 입장료를 차감하지 않는다. 초대 중에는 신청자 자리만 예약한다. 상대방은 본인이 수락할 때 자리를 예약하므로 미수락 초대로 다른 경기가 막히지 않는다. 수락한 활성 경기는 사용자당 하나이며 초대는 2분 뒤 만료 처리한다.

결과는 양쪽 인증 보고가 일치해야 정산한다. 불일치는 자동 지급하지 않으며 검토 상태로 남는다. 이는 서버 물리 시뮬레이션으로 승패를 증명하는 방식이 아니므로 두 클라이언트의 공모까지 막지 못한다. 운영 출시에는 결과 검증·분쟁 처리 정책을 확정해야 한다.

서버는 5초마다 미완료 경기를 검사한다. 생존 신호 15초 + 재접속 유예 30초 후 한쪽만 생존하면 상대 패배, 양쪽 만료면 반환·전적 미반영이다. 명시적 기권은 상대 승리다. 서버가 중단된 기간도 서버 시각 기준으로 처리한다.

경기 행·두 지갑 잠금·전적·원장·영수증·개인 기록은 같은 PostgreSQL 트랜잭션으로 저장한다. 출석도 같은 지갑 잠금을 사용한다. 결과 요청 반복은 기존 영수증을 반환하며 다시 지급하지 않는다.

## 관리자 사이트 (1차)

`./Server/GameApi/Start-Admin.ps1`로 별도 로컬 포트 5082에서 실행하고 `http://127.0.0.1:5082/admin`에 접속한다. 최초 계정은 개발 PC의 설정 화면에서 직접 생성한다. 회원·차트·대전/정산·원장은 실제 DB를 조회하고 게임 계정 인증과 분리된 관리자 쿠키를 사용한다. 변경 업무와 모바일 결제는 후속 개발 범위다. 구현 범위·실행·잔여 작업: `../../Docs/AdminSiteProgress.ko.md`.

## API 검증

경기 등록과 준비 요청은 이제 버전 1 규칙 계약을 요구한다. 서버와 Unity 플레이어를 함께 갱신해야 한다. 등록 시 서버 차트에서 확정한 계약을 보관하고, 양쪽 준비 요청의 규칙이 일치한 뒤 입장료를 차감한다. 구형 준비 요청과 다른 경기장 규칙은 거절한다. 세부 동작 및 새 계약의 검증 범위는 `../../Docs/LocalApiMatchRules.ko.md`를 참조한다.

API를 켜고 별도 PowerShell에서 실행한다. 테스트 URL은 로컬만 허용한다.

```powershell
dotnet run --project Server/GameApi.Tests/GameApi.Tests.csproj -- http://127.0.0.1:5080
```

실제 DB에 `verify_a_*`, `verify_b_*` 개발 계정을 생성한다. 임의 비밀번호·토큰은 출력하지 않으며 기존 자료는 삭제하지 않는다. 분쟁 검사를 위한 개발 경기 한 개가 미해결 상태로 남을 수 있다. 반복 실행 시 로그인 요청 제한(분당 30회)에 걸리면 다음 창에서 다시 실행한다.

## Railway 배포 준비와 남은 운영 검증

실제 Unity 개발 플레이어 두 개를 실행하는 검사는 프로젝트 루트에서 `./Server/GameApi/Run-ClientE2E.ps1 -Mode rematch`로 실행한다. `disconnect`, `reconnect` 모드도 있다. 최신 개발 빌드와 별도 검사용 경기장 시드가 필요하며 절차·실제 통과 결과·검증 한계는 `../../Docs/LocalApiClientE2E.ko.md`에 기록했다. 두 물리 기기 실전 검사는 별도다.

현재 일반 경기장 로컬 검사도 통과했다. `-Mode public`은 기존 가입 폼·Consist 카드·Pavilion 결과 화면과 코인 표시를 검사하고, `-HallId 9003`을 추가하면 3구를 검사한다(기본 4구). `-Mode restart`는 실제 경기 결과를 저장한 뒤 앱을 종료하고 새 프로세스에서 같은 계정으로 재전송한다. 비밀번호는 파일·인자·로그에 저장하지 않는다. 두 물리 기기와 실제 네트워크 어댑터 장애·장기 미전송은 추가 검증 대상이다.

결과 파일 복구는 `-Mode corrupt`, Photon·HTTP 동시 전송 장애 주입은 `-Mode outage`로 검사한다. `corrupt`는 미전송 결과 본문을 손상시키고 앱을 다시 시작해 정상 백업 복구와 정산을 확인한다. `outage`는 개발 클라이언트 양쪽의 Photon 송수신과 HTTP를 55초간 차단해 서버의 양쪽 연결 만료 판정 이후 복구 처리를 확인한다. 개발 전용 장애 주입이며 실제 네트워크 어댑터를 비활성화하지 않는다. 복구 정책과 검사 결과는 `../../Docs/LocalApiRecovery.ko.md`를 참조한다. 이전 결과 조작·다른 계정 큐 격리 통과 근거는 `../../Docs/LocalApiResultGuard.ko.md`에 있다.

가입·외부 배포는 아직 하지 않았다. 저장소 루트를 Docker 빌드 컨텍스트로 사용하고 `Server/GameApi/railway.toml` 경로를 지정한다. 환경 변수 `ConnectionStrings__GameDb`에 Railway PostgreSQL의 내부 접속 문자열을 설정한다. Railway의 URL 형태 DATABASE_URL을 그대로 .NET 연결 문자열로 넘기지 않는다. 비밀번호에 특수문자가 있으면 Npgsql 연결 문자열 빌더로 변환한다.

배포 전 확인할 항목: 실제 HTTPS, DB와 API의 동일 지역·내부 연결, 프록시를 고려한 로그인 제한, 제한된 DB 실행 계정/마이그레이션 계정 분리, 백업·복원, 비용 한도·가용성, 두 기기 실전 대전, 계정 복구·탈퇴 정책, 출시 시점의 .NET 지원 버전. 서버·단말 시각 대신 서버 시각으로 정산한다. 원격 단말에서 localhost는 개발 PC가 아니므로 별도 HTTPS 주소가 필요하다.

원본 뒤끝 데이터와 비밀번호는 가져오지 않았다. 실계정 이전은 계정 소유 확인·비밀번호 재설정과 ID 매핑을 별도로 적용한다.

## 차트 항목 추가·수정

`/admin/charts`에서 경기장 규칙(SkillMatch), 출석 보상(Attendance), 공통 코드(CommonCode)의 `항목 추가` 또는 행별 `수정`을 사용한다. Owner·Operator만 저장할 수 있고 Viewer는 조회만 가능하다. 숫자 형식·범위·중복 키와 출석 일차 연속성을 서버에서 검사한다. 저장은 차트 잠금·원본 버전 확인·감사 기록을 하나의 DB 트랜잭션으로 처리한다. 다른 관리자가 먼저 저장했다면 목록에서 최신 값을 다시 확인해야 한다.

저장 즉시 API 차트에 반영되며 Unity는 재로그인 시 갱신한다. 기존 경기의 규칙 스냅샷은 유지된다. 삭제 기능은 제공하지 않는다. 관리자 실행 스크립트는 게임 API와 파일 잠금이 충돌하지 않도록 `bin/AdminServer`에 별도로 빌드한다.

검증: `dotnet run --project Server/AdminChart.Tests/AdminChart.Tests.csproj -- http://127.0.0.1:5082` (관리자 서버 실행 필요). DB 검사는 임시 스키마에 실제 차트를 복사해 진행하고 종료 시 해당 스키마를 정리한다. HTTP 검사는 기존 개발 관리자 로그인과 화면·유효성 검사·CSRF를 확인한다. 실제 차트 값은 변경하지 않는다.
