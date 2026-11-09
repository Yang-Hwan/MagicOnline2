# Unity 하단 메뉴 적용

2026-10-27. 승인된 5개 메뉴 시안을 Consist 실행 화면에 적용했다.

## 구성

- MainSwipe.Awake에서 BottomMenuView를 설치한다. 이미지와 폰트가 없으면 기존 화면으로 남는다.
- 기존 세로 ScrollView/좌측 메뉴를 비활성화하고 PnlMainSwipe 아래에 네이티브 하단 메뉴를 만든다. 씬 원본과 대기실의 직렬화 연결은 보존한다.
- 1672×941 디자인 좌표계를 비율 유지 확대/축소한다. 4:3/울트라와이드는 레터박스로 처리해 그림과 버튼 위치가 어긋나지 않는다.
- Assets/Resources/Menu의 다섯 PNG는 예시 글자/숫자를 제거한 UI용 이미지다. 모든 닉네임·재화·레벨·진행 바·레이블·경기장 정보·버튼은 Unity UI다. 공통 상단/하단은 home 텍스처를 공유한다.
- 시안의 기본 아바타 그림을 유지했다. 별도의 아바타 선택 기능은 추가하지 않았다.
- 기존 한국어 폰트의 검은 underlay가 검정 글자를 뭉개므로 이 뷰 전용 Material에서 underlay/outline을 제거했다. 공용 폰트 자산은 변경하지 않았다.

## 연결한 기능

- 메인/싱글/멀티/순위/상점의 하단 전환 및 선택 강조.
- BackendGame/UserInfo 기반 닉네임, 레벨, 레벨 진행률, 코인, 보석. 재화 충전 버튼은 상점으로 이동한다.
- PracticeSceneFlow의 3구/4구 연습, 개별/시합 리플레이. 연습 복귀 시 싱글 탭 선택.
- BackendChart.skillMatchData의 경기장, 실제 접속 인원 이벤트, 입장 코인 범위, 경기 종류/목표/시간/마무리 규칙. 기존 RoomSwipe.GotoWaitRoom으로 매칭/대기실 진입을 유지한다.
- 출석부는 로그인에서 받은 BackendAttendance 캐시를 4행 단위로 보여준다. 조회를 위해 보상 지급 POST를 재호출하지 않는다.
- 기존 출석 팝업은 없고 프로필 팝업은 빈 컨테이너였으므로 자체 네이티브 대화상자를 만들었다. 닉네임 저장은 기존 PATCH /api/me/profile과 계정 데이터 갱신을 사용한다. 테스트 중 실제 닉네임 저장은 하지 않았다.
- 설정 팝업에서 게임 소리 켜기/끄기. 이 설정은 현재 실행에만 적용한다.

## 아직 없는 서비스

순위 조회 API, 상품 판매/현금결제, 우편함·보유장비·선물함·친구 기능은 기존 구현이 없어 준비 상태로 표시한다. 가짜 순위/가격/알림 개수를 실제 데이터로 표시하지 않는다. 순위 화면의 내 기록은 실제 계정 승패 통계이며 순위값을 만들어내지 않는다. 상점 상품 구매는 비활성화되어 있고 구매 복원도 결제 서비스 준비 안내만 표시한다.

## 검증

- Unity 6000.5.7f1에서 컴파일 성공.
- BottomMenuValidation: 다섯 탭의 전환, 좌측 메뉴/세로 스크롤 숨김, 경기장 차트 바인딩, 코인 최소/최대 경계, 미연결 결제 비활성화, 반복 메뉴 전환 검사 통과.
- 실제 Unity 카메라 렌더 8장 및 각 해상도에서 GraphicRaycaster로 하단 5개 버튼 클릭 영역 확인. 1672×941, 1024×768, 2560×1080.
- BottomMenuRuntimeValidation: 실제 Play 모드에서 5개 탭 전환, 출석/프로필 팝업, 대기실 복귀에 해당하는 활성화 전환, 4개 연습/리플레이 버튼의 씬 진입과 싱글 탭 복귀 모두 통과. 런타임 오류 기록 없음.
- 실제 멀티 상대와 매칭하는 E2E 경기 및 프로필 서버 저장은 이번 검증에서 실행하지 않았다.
- git diff --check 통과.
- Implemented 폴더는 Unity 렌더 및 검증 결과. 로그아웃 상태 렌더는 재화 0/기본 프로필이다. 멀티의 test-data 이미지는 검증용 메모리 차트임을 파일명에 명시했다.
- Windows 실행 파일은 재빌드하지 않았다. 기존 실행 파일에는 다음 빌드 후 반영된다.

## 이미지 생성

기존 승인 시안을 built-in image_gen으로 편집해 UI용 이미지 5개를 생성했다. 원본 시안은 보존했다. 최종 파일: Assets/Resources/Menu/home.png, single.png, multiplayer.png, ranking.png, shop.png.

공통 프롬프트:

Production UI backplate extraction/edit. Preserve EXACT pixel layout, landscape 1672x941 proportions, all artwork, icons, card borders and backgrounds of this approved game UI. Remove EVERY text letter and number on the entire screen, including Korean headings, button labels, subtitles, currency values, nickname, level text, counts, numbers on medals, prices, small notices, and bottom navigation text. Fill removed text cleanly with the local original background material. Keep all icon artwork including avatar, blue gem, coin, plus, gear, home/cue/trophy/cart navigation icons. Remove the green progress bar FILL only leaving empty dark track. Remove red mail notification badge entirely if present. Make bottom navigation icons all WHITE and remove yellow selected-tab underline and selected-tab yellow top line (selection will be drawn by code). Keep all existing black/yellow label ribbons blank. Do NOT reposition, redesign, crop, resize or add elements. No text or numbers anywhere. This is a blank decorative UI backplate for native Unity text/buttons to be overlaid at their original positions.

순위 이미지 추가 지시: Remove all player avatars from table rows too, leaving clean empty dark cells.
