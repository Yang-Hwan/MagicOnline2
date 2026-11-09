# 별빛 로그인 UI 적용

Come 씬의 PnlLogin.Awake에서 StarryLoginView를 적용한다. 승인 시안의 배경·로고·패널·아이콘·버튼 바탕은 Assets/Resources/Login/StarryNightBackplate.png로 보존하고, 입력란·제목·버튼 글씨·상태 안내는 Unity TMP/UI를 사용한다. 인증 및 가입 호출은 기존 PnlLogin을 사용한다. 개발용 로그인1/2 버튼은 시안에서 숨긴다.

1672×941 기준 좌표와 전체 뷰의 동일 배율을 사용하여 컨트롤과 배경 위치를 일치시킨다. 다른 비율은 레터박스로 처리한다. UI 이미지는 무압축·밉맵 없음·NPOT 크기 보존으로 설정한다.

검증: StarryLoginValidation의 별도 PreviewScene에서 빈 로그인/가입 안내, 비밀번호 모드, 16:9 및 4:3 렌더링을 확인했다. 열린 Unity에서 Arise → Come Play 실행도 확인했다. 실제 계정 가입이나 인증 서버 왕복은 이번 디자인 검증에서 실행하지 않았다. 기존 Windows 실행 파일은 새로 빌드해야 반영된다.

unity-implemented.png는 실제 Unity UI 렌더링이다. Arise 로고 및 기존 씬 파일은 변경하지 않았다.

## 이미지 제작

내장 image_gen 사용. 승인된 login-starry-night.png를 입력 이미지로 하여 제목 로그인, 아이디·비밀번호 placeholder, 로그인 버튼 글씨/화살표, 회원가입 글씨만 지우고 나머지 그림·버튼·아이콘·배치를 보존하도록 요청했다. 빈 공간에는 실제 TMP 텍스트와 입력 컨트롤을 배치했다.
