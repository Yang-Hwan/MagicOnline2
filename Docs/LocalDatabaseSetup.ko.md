# 다른 PC의 로컬 DB 개발 환경 설정

작성일: 2026-10-22. Windows PowerShell 기준이며 명령은 별도 표시가 없으면 저장소 루트에서 실행한다.
구조는 Unity → 로컬 ASP.NET Core API → PostgreSQL이다. Photon은 대전 통신에 계속 사용한다.
Git에는 소스·마이그레이션·초기 차트만 포함한다. 기존 PC의 DB 데이터, 접속 비밀번호, 관리자 쿠키, 빌드 파일은 복제되지 않는다.

## 1. 준비 및 소스 받기

- Git
- Unity Hub 및 Unity **6000.5.7f1** (ProjectSettings/ProjectVersion.txt 기준)
- **.NET 8 SDK** (Runtime만 설치하면 빌드할 수 없음)
- **PostgreSQL 18** 서버와 Command Line Tools (현재 개발 PC의 설치 버전)

PostgreSQL 설치 시 포트는 `5432`, 관리자 계정은 `postgres`를 사용하고 관리자 비밀번호를 기억한다.
아래 Git 주소로 새로 받거나, 이미 받은 저장소에서 `git pull --ff-only`를 실행한다. 로컬 수정이 있다면 먼저 보관한다.

```powershell
git clone https://github.com/Yang-Hwan/MagicOnline2.git
Set-Location MagicOnline2
dotnet --version
$pgBin = 'C:\Program Files\PostgreSQL\18\bin'
& "$pgBin\psql.exe" --version
```

다른 버전이나 경로에 설치했다면 `$pgBin`만 실제 경로로 바꾼다. Unity Hub에서 이 폴더를 프로젝트로 추가한다.

## 2. DB와 개발용 계정 생성 (PC별 최초 1회)

```powershell
& "$pgBin\psql.exe" -h localhost -p 5432 -U postgres -d postgres
```

관리자 비밀번호로 접속한 뒤 아래를 **psql 창**에서 실행한다. 따옴표 없는 PostgreSQL 이름은 소문자로 변환되므로 아래 이름은 큰따옴표를 유지한다.

```sql
CREATE ROLE "magicCushion" LOGIN;
\password magicCushion
CREATE DATABASE "magicCushion" OWNER "magicCushion";
\q
```

`\password`에서 새 개발용 DB 비밀번호를 두 번 입력한다. 이미 계정·DB가 있다면 재생성하지 않고 해당 접속 정보를 사용한다.
이 계정은 자기 DB의 테이블 생성·마이그레이션을 수행한다. PostgreSQL 관리자 계정을 게임 접속 정보로 사용하지 않는다.

## 3. API의 DB 접속 정보 저장

PowerShell로 돌아와 아래 `<DB 비밀번호>`를 2단계의 값으로 바꿔 실행한다. 이는 테스트 게임 계정 비밀번호와 별개다.

```powershell
dotnet user-secrets set 'ConnectionStrings:GameDb' 'Host=localhost;Port=5432;Database=magicCushion;Username=magicCushion;Password=<DB 비밀번호>' --project Server/GameApi/GameApi.csproj
```

user-secrets는 현재 Windows 사용자 프로필에 저장된다. Git으로 이동하지 않으므로 새 PC에서 다시 설정해야 한다.
연결 문자열에 세미콜론 등 특수문자가 있는 비밀번호는 Npgsql 연결 문자열 규칙에 맞게 값을 인용해야 한다.
기존 `ConnectionStrings__GameDb` 환경 변수가 있으면 user-secrets보다 우선하므로 의도한 DB를 가리키는지 확인한다.

## 4. 테이블 구성과 API 실행

```powershell
dotnet build Server/GameApi/GameApi.csproj
./Server/GameApi/Start-Local.ps1 -MigrateOnly
./Server/GameApi/Start-Local.ps1
```

실행 중인 창은 열어 둔다. 별도 PowerShell에서 DB 연결까지 확인한다.

```powershell
Invoke-RestMethod 'http://127.0.0.1:5080/health'
```

`Ok=True`, `Database=Connected`가 나오면 정상이다. 서버 종료는 실행 창에서 Ctrl+C를 누른다.
스크립트 실행 정책으로 차단된다면 해당 창에만 `Set-ExecutionPolicy -Scope Process Bypass`를 적용하고 다시 실행한다.

마이그레이션은 시작할 때 순서대로 적용되며 이미 적용한 SQL은 체크섬으로 검사한다.
초기 차트는 `Docs/BackendSeed`의 CommonCode·SkillMatch·Attendance CSV에서 생성하며 기존 차트를 덮어쓰지 않는다.
적용된 `Server/GameApi/Migrations/*.sql`은 수정하지 않는다. 변경은 다음 번호의 SQL 파일로 추가한다.
SQL 줄바꿈은 `.gitattributes`로 LF를 유지한다.

## 5. 테스트 계정 만들기와 Unity 실행

새 DB에는 `user01`, `user02`가 자동 생성되지 않는다. API 실행 후 아래를 **최초 한 번** 실행한다.
응답의 인증 토큰은 화면에 출력하지 않는다.

```powershell
foreach ($loginId in @('user01', 'user02')) {
    $body = @{ LoginId = $loginId; Password = '11111111' } | ConvertTo-Json
    Invoke-RestMethod 'http://127.0.0.1:5080/api/auth/register' -Method Post -ContentType 'application/json' -Body $body | Out-Null
    Write-Host "$loginId 생성 완료"
}
```

이미 존재하면 `409 / LoginIdTaken`이 반환된다. 기존 계정을 그대로 사용하며 비밀번호는 자동 재설정하지 않는다.
Unity 로그인 화면의 가입 버튼으로도 각 계정을 생성할 수 있다.

1. `Assets/Resources/GameApiConfig.json`의 `BaseUrl`이 `http://127.0.0.1:5080`인지 확인한다.
2. Unity에서 `Assets/Scenes/Arise.unity`를 열고 Play를 누른다.
3. 로그인 화면의 **로그인1**은 `user01 / 11111111`, **로그인2**는 `user02 / 11111111`을 자동 입력하고 즉시 로그인한다.
4. 로그인 후 서버 차트·출석·프로필 로딩과 Photon 연결을 확인한다. 처리 중에는 로그인 버튼이 잠시 비활성화된다.

게임 계정과 관리자 계정은 별개다. 테스트 로그인 버튼은 현재 빌드에도 표시된다.
Photon 설정은 `Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings.asset`을 사용한다.
DB 로그인은 성공하지만 대전 연결이 실패한다면 Photon 설정과 인터넷 연결도 확인한다.

## 6. 관리자 화면 (선택)

다른 PowerShell 창에서 실행한다. 같은 user-secrets와 DB를 사용한다.

```powershell
./Server/GameApi/Start-Admin.ps1
```

브라우저에서 `http://127.0.0.1:5082/admin`을 연다. 현재 Development·로컬 접속에서는 관리자 ID만으로 개발 로그인을 하며,
최초에는 해당 ID의 개발용 관리자를 생성한다. 이후에는 등록된 관리자 ID를 사용한다.
관리자 화면에서 회원·차트·경기·원장을 확인할 수 있다. 이 개발 로그인 구성을 외부에 공개하지 않는다.

## 7. 다른 PC에서도 함께 대전할 때

각 PC에 별도 DB를 만들면 계정 UUID와 경기 데이터가 달라진다. **두 클라이언트가 대전하려면 같은 API와 같은 DB를 사용해야 한다.**
각 PC에서 독립적으로 개발할 때만 각자의 `127.0.0.1:5080`을 사용한다.
공유 테스트에서는 한 PC에 DB·API를 두고 두 클라이언트의 `BaseUrl`을 그 API 주소로 맞춘다.
현재 스크립트는 루프백으로만 수신하므로 주소 변경만으로 다른 PC에서 접속할 수는 없다.
공유 환경은 API 수신 주소·방화벽·HTTPS를 별도로 구성하고 PostgreSQL 포트를 단말에 직접 노출하지 않는다.

## 8. 기존 PC 데이터까지 가져오기 (선택)

신규 개발 환경은 앞 절차로 충분하다. 기존 회원·차트·전적까지 필요하면 DB 백업을 별도로 옮긴다.
API·관리자 서버와 클라이언트를 중지하고, 원본 PC PowerShell에서 실행한다.

```powershell
$pgBin = 'C:\Program Files\PostgreSQL\18\bin'
& "$pgBin\pg_dump.exe" -h localhost -p 5432 -U magicCushion -d magicCushion -Fc --no-owner --no-acl -f "$env:USERPROFILE\Desktop\magicCushion.dump"
```

새 PC의 2단계에서 만든 **비어 있는 DB**에 복원한다. 4단계를 이미 실행해 테이블이 있다면 그대로 덮어쓰지 말고 별도 빈 DB를 생성해 접속 설정을 바꾼다.

```powershell
& "$pgBin\pg_restore.exe" -h localhost -p 5432 -U magicCushion -d magicCushion --no-owner --no-acl --exit-on-error "$env:USERPROFILE\Desktop\magicCushion.dump"
```

백업을 만든 버전과 같거나 호환되는 PostgreSQL 도구를 사용한다. 복원 후 4단계로 남은 마이그레이션과 health를 확인한다.
백업에는 사용자·인증 관련 정보가 들어 있으므로 Git에 넣지 않는다. DB 비밀번호·user-secrets는 별도로 설정한다.
Unity 로컬의 미전송 결과 큐는 이 DB 백업에 포함되지 않으므로 원본 PC의 정산 재전송을 마친 뒤 옮긴다.

## 9. 문제 확인 및 검사

| 증상 | 확인할 내용 |
| --- | --- |
| dotnet 명령 없음 | .NET 8 SDK 설치 후 PowerShell 다시 열기 |
| DB 연결 거절 | PostgreSQL Windows 서비스, 5432 포트, Host 확인 |
| password authentication failed | DB 계정 비밀번호와 user-secrets 일치 여부 |
| database/role does not exist | 2단계 실행 여부와 대소문자 확인 |
| Configure ConnectionStrings:GameDb | 현재 Windows 사용자로 3단계 실행, Development 환경 확인 |
| Applied migration changed | SQL 변경·줄바꿈·복원한 DB와 코드 버전 확인. 이력 행을 삭제해 우회하지 않기 |
| 로그인1/2 실패 | 해당 DB에 테스트 계정 생성 여부와 비밀번호 확인. 5회 실패 시 5분 대기 |
| HTTP 429 | 인증 요청 제한에 걸린 상태. 1분 뒤 재시도 |
| 5080/5082 포트 사용 중 | 기존 서버 창을 확인하고 중복 실행 종료 |

선택 검사 명령:

```powershell
dotnet run --project Server/MatchSettlement.Tests/MatchSettlement.Tests.csproj --configuration Release
dotnet run --project Server/GameApi.Tests/GameApi.Tests.csproj -- http://127.0.0.1:5080
```

두 번째 검사는 API 실행이 필요하며 실제 로컬 DB에 검사용 계정·경기를 추가한다.
추가 구현 설명은 [API 안내](../Server/GameApi/README.ko.md), 기존 검증 기록은 [DB 전환 결과](LocalDatabaseMigrationProgress.ko.md)를 참고한다.
