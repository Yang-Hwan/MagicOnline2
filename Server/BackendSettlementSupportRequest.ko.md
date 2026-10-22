# 뒤끝 기술지원 문의 초안

제목: 펑션에서 두 사용자 재화의 조건부 트랜잭션 구현 방법 및 데이터베이스 서버 SDK 지원 여부

프로젝트: magicCushion
환경: Unity 6000.5.7f1, .NET 8, 뒤끝 펑션 개발 도구 1.0.2
검사일: 2026-10-09

두 명이 참여하는 4구·3구 경기 정산을 구현하고 있습니다. 시작 시 양쪽 입장료를 차감하고, 종료 시 승자에게 합계를 지급하거나 무승부이면 반환합니다. 같은 사용자의 다른 재화 변경과 경쟁할 수 있어 양쪽 지갑 버전과 경기 상태를 하나의 원자적 트랜잭션으로 검사·갱신해야 합니다.

호출자의 인증은 Backend.Initialize(ref stream) 후 Backend.BMember.GetUserInfo()로 확인했습니다. 공개 게임정보 진단 테이블에서 다음 결과를 직접 확인했습니다. 운영 재화에는 적용하지 않았습니다.

1. GetV2로 A/B의 공개 행 조회: 성공.
2. SetUpdateV2(table, inDate, owner_inDate, param) 두 개를 TransactionWriteV2로 묶어 A/B 갱신: 성공, 내부 204.
3. SetUpdate(table, where, param)에서 owner_inDate·RunId·Slot·Version 조건으로 호출자 A의 두 행 갱신: 성공, 내부 204.
4. 같은 조건 방식으로 B 행 갱신: 내부 404 NotFoundException, message=gameinfo not found.
5. A/B/경기 상태 대용 행을 같은 조건 방식으로 묶어 갱신: 전체 실패, 모든 값 유지.

확인이 필요한 사항:

- 펑션 서버 권한으로 B 소유 행에 버전 조건 또는 잔액 조건을 검사하며 갱신하는 공식 API가 있나요? 메서드 시그니처와 최소 예제를 부탁드립니다.
- SetUpdateV2 또는 다른 트랜잭션 API에 갱신 조건을 전달할 수 있나요? 조건이 불일치하면 나머지 사용자/경기 상태/영수증 쓰기도 모두 롤백되어야 합니다.
- 기존 Where 기반 트랜잭션의 조건은 실제 커밋 시점에도 검사되나요, 아니면 먼저 행을 찾는 조회 조건인가요? 네 동시 요청 중 하나만 성공한 실제 관찰은 있으나 공식 보장 범위를 확인하고 싶습니다.
- 현재 데이터베이스 기능을 .NET 8 펑션에서 이용할 수 있나요? 가능하면 공식 서버 SDK/API, 서버 인증, ClientAccess=false 테이블 접근 방법을 부탁드립니다. 2026-02-24 담당자 답변에는 펑션 미지원이라고 안내돼 있어 변경 여부를 확인하고자 합니다.
- 다른 사용자의 비공개 경기 기록을 서버에서 저장하는 공식 방법이 있나요?
- 코인·전적·경기 상태·영구 중복 방지 영수증을 함께 처리할 때 권장하는 뒤끝만의 구성은 무엇인가요? 서버만 수정하고 클라이언트가 직접 변경할 수 없는 권위 저장소도 필요합니다.

인증키, 토큰, 실제 사용자 식별자는 포함하지 않았습니다. 문의는 2026-10-09 공식 커뮤니티에 등록했습니다.

참고 문서:

- https://docs.backnd.com/sdk-docs/backend/base/game-information/transaction/crud/update/
- https://docs.backnd.com/sdk-docs/backend/base/game-information/transaction/write-v2/
- https://community.thebackend.io/t/topic/11164

## 전송 진행 상태

2026-10-09 사용자의 문의 전송 승인 및 로그인 완료 후 Oyh52 계정으로 공식 커뮤니티의 문의 게시판 > 펑션에 등록했다. 등록된 제목과 본문을 확인했다.

- 접수 글: https://community.thebackend.io/t/sdk/12074
- 게시글 번호: 12074
- 상태: 등록 완료, 담당자 답변 대기
- 증빙: ../Docs/BackendSupportSubmission.png
- 문의 범위: 두 계정 조건부 트랜잭션, 커밋 시 조건 검사 보장, 데이터베이스 서버 SDK 및 비공개 기록 저장 지원.

사이트 안내에 따르면 이메일 발신이 비활성화되어 있으므로 답변은 커뮤니티 글에서 확인해야 한다. 운영 정산은 공식 지원 경로 확인 전까지 비활성 상태다.
