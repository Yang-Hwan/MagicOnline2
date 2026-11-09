# 하단 메뉴 통합 시안

생성 방식: built-in image_gen. Unity 구현 변경 없이 시안만 작성.

메인은 승인된 첨부 시안을 그대로 보존했고 나머지 네 화면을 새로 생성했다. 상단 프로필/재화/설정과 하단 메인→싱글플레이→멀티플레이→순위→상점 순서를 공유한다.

| 파일 | 내용 |
|---|---|
| 01-home.png | 우편함, 보유장비, 선물함, 출석부, 친구, 기타 |
| 02-single.png | 3구/4구 연습, 개별/시합 리플레이 |
| 03-multiplayer.png | 기존 파란 걸이형 경기장 카드, 좌우 선택, 경기 조건, 입장 |
| 04-ranking.png | 전체/3구/4구 분류 제안, 순위 목록 및 내 순위 |
| 05-shop.png | 추천/보석/큐대 분류 제안, 현금 구매 카드 |

기존 기능 참조: PracticeMenuView.cs의 네 모드, HallRoom.cs의 제목/접속 인원/입장비/경기 종류/목표/마무리/시간, RoomSwipe.cs의 경기장 가로 선택, PnlLobby.cs의 입장 후 상대 대기 흐름. 멀티는 기존 room_wall_02.png 및 room_card_hanger_part.png를 참고해 새 메뉴 틀에 맞춘 시안이며 실제 화면 캡처가 아니다.

닉네임, 재화, 경기장 이름/접속 인원/조건, 순위, 상품명/가격은 모두 시안용 예시. 순위 필터와 구매 복원은 UI 제안이며 구현 완료를 뜻하지 않는다. 실제 게임 규칙과 판매 데이터는 구현 단계에서 연결한다.

## 공통 생성 프롬프트

Use case: ui-mockup. Produce ONE finished landscape 1672x941 Korean billiards game screen, flat 2D comic graffiti with bold ink, halftone and yellow/white/cyan accents, matching the supplied HOME mockup. Reference image 1 is the shared UI style/layout reference. Reproduce its identical top header (avatar, nickname "타이거우즈20", "Lv.20", green progress bar on left, blue gem "7,000" plus, gold coin "7,000" plus, blue gear on right). Reproduce its identical fixed bottom navigation strip and order: "메인", "싱글플레이", "멀티플레이", "순위", "상점", same icon positions, yellow highlight ONLY on current page. No left sidebar. Content fits between header at y125 and footer y835, generous readable spacing, crisp Korean type. No device frames. Preserve supplied background art around content. No photorealism. 

## 02-single

Image 2 is the exact background to use: cracked wall, cyan billiards table lower left and video tapes lower right. Current tab "싱글플레이". Heading "싱글플레이" at top of content. Four large comic button cards in centered 2x2 grid, x420..1260 y245..720. Exact labels and small subtitles: "3구 연습" / "자유롭게 배치하고 연습", "4구 연습" / "기본기부터 차근차근", "개별 리플레이" / "저장한 연습 다시 보기", "시합 리플레이" / "멀티플레이 경기 다시 보기". Icons: three carom balls (ivory red yellow), four carom balls (two red two ivory), video with single cue ball, replay video with VS. Keep decorative table and tapes visible at edges. Large intuitive yellow or white actionable cards with dark readable labels, not a list of fake recordings.

## 03-multiplayer

Current tab "멀티플레이". Image 2 references existing arena wall surround, image 3 references existing BLUE HANGING ARENA CARDS. Preserve that arena selection concept: dark wall room backdrop and three blue rectangular rounded hanging plaques suspended from a horizontal rail, center selected with yellow outline. Integrate comic UI from image1. No blank white holes from source transparency. Heading "멀티플레이" and small "경기장 선택". Three cards side by side with left and right carousel arrow controls. On left card exact "4구 경기장", "접속 24명", "입장 100 코인", "4구 · 사구", "목표 3개", "제한시간 10분", button "선택". Center card "3구 경기장", "접속 18명", "입장 500 코인", "3구 · 쓰리쿠션", "목표 10개", "제한시간 15분", "쿠션 마무리", prominent yellow "입장하기". Right card "고급 경기장", "접속 8명", "입장 1,000 코인", "3구 · 쓰리쿠션", "목표 15개", "제한시간 20분", button "선택". Small billiards icons near card tops. Bottom of content small pagination dots and helper "경기장 입장 후 상대를 기다립니다". This is ARENA selection, not a list of individual private rooms, not an active match or waiting screen. No invented create room controls. All shown counts are mock data.

## 04-ranking

Image2 exact cracked cement ranking background with trophy right, flying balls left, cyan table bottom. Current tab "순위". Content centered x300..1280 leaving golden trophy exposed on right. Heading "순위". Compact filter tabs "전체" selected yellow, "3구", "4구". A dark readable leaderboard panel with header columns "순위", "닉네임", "승리", "승률". Five clean aligned rows, small avatar icon in each. Gold/silver/bronze rank medals for first3. Data rows: "1" "쿠션마스터" "320승" "82%"; "2" "당구의신" "298승" "79%"; "3" "매직플레이어" "275승" "76%"; "4" "쓰리쿠션" "260승" "73%"; "5" "뱅크샷" "248승" "71%". Beneath a distinct yellow-bordered personal row "내 순위" "128" "타이거우즈20" "42승" "60%". All data illustrative. No extra navigation or giant podium hiding usable list. Legible mobile-scale typography.

## 05-shop

Image2 exact rough cement shop background with cue rack left and treasure case right. Current tab "상점". Heading "상점", category tabs "추천" selected yellow, "보석", "큐대". Three clear product cards in central x350..1330 y260..690, preserving rack and treasure at edges: first blue gem pile, name "보석 500개", yellow purchase button "₩5,500"; second larger gem pile, name "보석 1,200개", small "추천", yellow purchase button "₩11,000"; third handsome red black cue illustration diagonally, name "레드 스트라이크 큐", yellow purchase button "₩9,900". Prices are explicitly illustrative: subtle footer text "상품과 가격은 시안용 예시입니다". Small utility button "구매 복원" beneath cards. Do not claim discounts or real sale terms. Clear purchase affordance with price, not payment checkout dialog. Maintain flat comic drawings not 3D products.
