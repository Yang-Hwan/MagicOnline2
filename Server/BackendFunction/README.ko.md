# 뒤끝 펑션 진입점 준비

`BFunc.Function(Stream, ILambdaContext)`는 공식 1.0.2 템플릿의 진입점과 DLL 이름을 따른다. `Backend.Initialize` 이후 SDK의 UserInDate만 호출자 ID로 사용한다. 요청 본문에 UserId나 코인을 넣어 호출자와 금액을 바꾸는 요청은 거부한다.

일반 요청은 `{"Action":"Health"}`이며 StorageConnected와 SettlementEnabled가 모두 false임을 응답한다. 정산 요청은 SettlementStorageNotConfigured로 거부한다. 추가된 Probe 명령은 명시적으로 활성화한 테스트 계정과 전용 테이블에서만 실행한다. 운영 저장소·서버 인증이 연결된 정산 함수가 아니다. 배포하거나 일반 게임 호출에 연결하지 않았다. [실서버 진단 실행 방법](../../Docs/BackendFunctionProbe.ko.md)을 참고한다.

공식 템플릿 DLL은 저장소에 넣지 않고 로컬 경로를 참조한다. 다른 컴퓨터에서는 공식 템플릿을 받은 후 FunctionSdkDir을 지정한다.

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.5.7f1/Editor/Data/DotNetSdk/dotnet.exe' build Server/BackendFunction/BackendFunction.csproj --configuration Release
```

이 명령은 로컬 컴파일 검증이다. 실제 디버깅·배포는 [공식 개발 툴](https://docs.backnd.com/sdk-docs/function/intro/)로 진행해야 한다. SDK 예외 원문에는 인증 정보가 포함될 수 있으므로 응답이나 로그에 남기지 않는다.

호출 검증 8개를 기존 정산 검사에 추가했고, 총 36개 검사에서 인증 ID 누락·위조 필드·중복 명령·잘못된 JSON·큰 요청·미지원 명령·저장소 미연결 거부를 확인한다. SDK 초기화의 실제 서버 인증 왕복 검증은 별도다.
