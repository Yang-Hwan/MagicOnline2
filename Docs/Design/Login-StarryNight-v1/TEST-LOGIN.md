# 임시 테스트 로그인 버튼 복원 (2026-10-27)

사용자 요청으로 새 로그인 패널 아래에 테스트로그인1(user01), 테스트로그인2(user02)를 다시 표시한다. 이전 IMPLEMENTATION.md의 테스트 로그인 숨김 설명은 이 변경으로 대체된다.

PnlLogin.ConfigureTestLogin의 기존 계정 입력 및 Authenticate(false) 이벤트를 재사용한다. 로그인 진행 중 두 테스트 버튼도 비활성화된다. 오류 메시지는 버튼 아래로 이동했다. 이미지 배경과 일반 로그인/회원가입 동작은 유지한다.

Unity 컴파일 및 StarryLoginValidation 통과. 두 버튼 활성 상태/계정 표시, 빈 입력 검사, 비밀번호 마스킹, 16:9와 4:3 렌더를 확인했다. 실제 서버 로그인은 이번 검증에서 실행하지 않았다.

화면: unity-test-login-buttons.png. Windows 실행 파일 재빌드 전에는 에디터/다음 빌드에만 반영된다.
