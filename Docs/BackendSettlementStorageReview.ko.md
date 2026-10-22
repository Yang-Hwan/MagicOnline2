# 정산 저장소 경로 확인 — 2026-10-09

## 확인 결론

현재 설치된 공식 펑션 SDK와 공개 자료에서 두 사용자 소유 행에 버전 조건을 함께 적용하는 공식 서버 API를 확인하지 못했다. 이는 펑션 실행 불가라는 뜻이 아니다. 펑션 호출과 양쪽 행 ID 갱신은 성공했고, 정산 저장소에 필요한 조건부 쓰기와 서버 권한 연결이 해결되지 않은 상태다.

별도 서버가 없으며 뒤끝 펑션을 사용하는 기존 방향을 유지한다. 확인되지 않은 서버 권한을 추정해 SDK 내부 헤더를 조작하거나 실제 지갑을 이전하지 않는다.

## 설치 SDK 확인

공식 설치 경로 C:/Program Files/AFI/BackendFunction/API/Backend.dll와 프로젝트 참조 캐시의 Backend.dll SHA256은 모두 아래와 같다.

`99875E30DC60B037C9B8B51F586DDA93023C0FCE6E76088F6F7F7343E32C417F`

따라서 설치 API 대신 오래된 다른 DLL을 참조해서 발생한 차이는 확인되지 않았다. 공식 다운로드 안내는 펑션 도구 1.0.2와 2025-08-08 배포 파일을 안내한다. Unity 베이스 SDK 업데이트를 펑션 서버 SDK의 동일 기능 지원으로 취급하지 않는다.

설치 DLL의 공개 시그니처를 검사한 결과:

| 경로 | 확인한 메서드 | 정산에 필요한 추가 기능 |
| --- | --- | --- |
| GameData 행 ID 쓰기 | SetUpdateV2(tableName, inDate, owner_inDate, Param) | 별도 Where/ExpectedVersion 인자 없음 |
| GameData 조건 쓰기 | SetUpdate(tableName, Where, Param) | 상대 행 접근은 실제 검사에서 404 |
| PlayerData 상대 쓰기 | AddUpdateOtherData(tableName, inDate, owner_inDate, Param) | 별도 버전 조건 인자 없음 |
| 수치 연산 | UpdateWithCalculationV2(tableName, inDate, owner_inDate, Param) | 정산 영수증·경기 상태와 조건부로 묶는 트랜잭션 지원은 확인되지 않음 |
| 새 Database | 설치 펑션 SDK에 서버 Database 타입 확인 안 됨 | 공식 서버 SDK 및 서버 전용 인증 확인 필요 |

이 검사는 공개 API 표면을 확인한 것이다. 공개되지 않은 지원 API나 이후 지원 변경의 부재를 증명하는 것은 아니다.

## 공식 자료 재확인

- [Where 기반 Update](https://docs.backnd.com/sdk-docs/backend/5.15.2/base/game-information/update/using-query/)는 본인 데이터만 수정 가능하다고 명시한다.
- [SetUpdate/SetUpdateV2](https://docs.backnd.com/sdk-docs/backend/base/game-information/transaction/crud/update/)의 공개 시그니처에는 상대 행 ID와 Where 조건을 함께 받는 메서드가 없다.
- [데이터베이스 시작하기](https://docs.backnd.com/sdk-docs/database/intro/)는 Unity 클라이언트 초기화와 사용자 로그인 경로를 안내한다. 이것만으로 .NET 펑션 서버 전용 연결을 확정할 수 없다.
- [2026-02-24 담당자 답변](https://community.thebackend.io/t/topic/11164)은 데이터베이스의 펑션 이용을 미지원으로 안내한다. 이후 지원을 확정하는 구체적인 서버 SDK/API 안내는 이번 검색에서 찾지 못했다.
- [2026-04-20 상점 관련 답변](https://community.thebackend.io/t/topic/11448)은 펑션으로 서버 로직 구현이 가능하다는 일반적인 설명을 제공하지만 Database 서버 API의 구체적인 사용법이나 두 지갑 트랜잭션 보장을 제공하지 않는다. 따라서 지원 전환의 근거로 사용하지 않는다.

## 경로별 판단

| 방식 | 판단과 이유 |
| --- | --- |
| 양쪽 UserMain 값을 읽고 SetUpdateV2로 덮어쓰기 | 채택 보류. 읽기와 쓰기 사이에 다른 경기·구매 변경이 있으면 잔액이나 전적을 덮어쓸 수 있음 |
| 서버리스 인스턴스의 lock/static으로 보호 | 채택하지 않음. 서로 다른 인스턴스에 걸친 직렬화를 보장하지 못함 |
| 경기 상태만 조건 검사하고 두 지갑은 무조건 쓰기 | 채택 보류. 같은 경기 중복을 막더라도 다른 경기에서 공유 지갑 갱신 경쟁은 남음 |
| 자기 계정별 정산 요청·지급을 따로 처리 | 다른 정산 모델. 양쪽 동시 처리와 직후 완료 요구가 달라지므로 사용자 판단 없이 전환하지 않음 |
| 새 Database의 서버 권위 테이블 | 우선 확인 대상. 공식 펑션 SDK/인증과 원자적 조건 실패 처리가 확인되면 기존 연결안을 재검토 |
| 별도 DB/서버 도입 | 현재 사용자 환경 밖의 구성. 임의 생성·과금·이전하지 않음 |

## 다음 단계의 완료 조건

기술지원 확인용 최소 재현 및 질문은 Server/BackendSettlementSupportRequest.ko.md에 정리했다. 문의 전송은 별도 사용자 지시가 필요하다.

지원 경로가 확인되면 테스트 전용 저장소로 양쪽 조건 실패 전체 롤백, 공유 지갑 경쟁, 영구 영수증 고유성, 비공개 기록 쓰기 및 클라이언트 직접 수정 차단을 검사한다. 그 결과를 근거로 ISettlementStore를 구현하고 시작·종료·이탈 정산을 연결한다. 지금까지의 진단 성공을 이 완료 조건의 대체로 사용하지 않는다.

현재 추가 코드 배포나 운영 정산 활성화는 하지 않았다. 이미 준비된 도메인 정산 코드와 테스트는 유지한다.
