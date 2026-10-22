# 두 계정·경기 상태 조건부 갱신 검사 — 2026-10-09

## 결론

현재 펑션 SDK와 호출자 인증 방식에서는 상대 계정의 공개 행을 Where/Version 조건으로 갱신하는 요청이 실패했다. 운영 저장소 연결의 선행 조건이 충족되지 않았으므로 운영 코인·전적 정산은 비활성 상태로 유지한다. 펑션 호출 자체는 정상이며, 기존 행 ID 방식의 두 계정 갱신 성공과는 구별해야 한다.

## 직접 실행한 순서

공식 CLI 빌드·배포 → Tools > Magic Online > Backend Function Probe > Run from Arise → Come 씬 user02 로그인 → Consist 진단 창에서 기존 Run ID와 user04 행 지정 → 새로 추가한 11 Atomic pair + match state suite 클릭. 완료 후 Play 종료.

FunctionProbePublic 진단 데이터만 사용했다. 실제 지갑과 경기 상태 대신 두 계정의 Primary 카운터와 user02 Audit 카운터를 사용한 저장소 기능 검사다.

Run ID: `f71dd56b7df74dd5bafc810117d0b818`.
user02 Primary `2026-10-09T04:31:29.247Z`, user04 Primary `2026-10-09T04:34:54.885Z`.

## 결과

| 요청 | 내부 Status | 결과 |
| --- | --- | --- |
| 본인 Primary와 Audit: 현재 버전 조건으로 기존 값을 그대로 쓰는 대조 검사 | 204 | 성공 |
| 상대 Primary: 현재 버전 조건으로 기존 값을 그대로 쓰는 대조 검사 | 404 | 실패 |
| 상대 버전을 틀리게 지정한 세 행 트랜잭션 | 404 | 전부 유지, 단 상대 정상 조건도 실패하므로 원자적 조건 검증 통과로 판단하지 않음 |
| 상태 버전을 틀리게 지정한 세 행 트랜잭션 | 404 | 전부 유지, 동일한 이유로 통과 판정하지 않음 |
| 세 행에 현재 버전을 정확히 지정 | 404 NotFoundException | 실패: gameinfo not found |
| 이전 스냅샷으로 재요청 | 404 | 정상 갱신 자체가 실패했으므로 중복 방지 성공으로 판단하지 않음 |

최종 Counter/Version: user02=1/1, user04=1/1, 상태 대용 Audit=0/0. 값은 모두 검사 전과 같다. HTTP 200은 펑션 호출 성공이며 저장 성공을 뜻하지 않는다. 전체 Ok=false, elapsedMs=7722.

초기 inDate 조건식은 400으로 거절됐다. 공식 문서에 따라 inDate 조건을 제거하고 owner_inDate·RunId·Slot·Version 조건으로 재배포했다. 이때 본인 대조 검사는 성공하고 상대 대조 검사만 실패했다.

## 구현에 미치는 영향

두 계정 데이터를 행 ID로 쓰는 SetUpdateV2에는 이 검사에서 사용하는 Version 조건을 전달할 수 없다. 읽은 값을 나중에 덮어쓰는 방식만으로는 다른 경기나 구매의 잔액 변경을 보호할 수 없다. 읽기 검사와 실제 갱신 사이에 값이 바뀔 수 있기 때문이다.

따라서 운영 ISettlementStore를 기존 두 계정 행에 바로 연결하지 않는다. 다음으로 확인할 사항은 펑션 서버 권한으로 타인 소유 행에 조건부 갱신을 적용하는 공식 API가 있는지, 또는 서버만 수정하는 권위 저장소에 두 지갑·경기 상태·영구 영수증을 함께 저장할 수 있는지다. 이 조건을 확인하기 전에는 외부 DB가 없다는 사용자 조건을 무시하거나 클라이언트 단독 정산으로 변경하지 않는다.

비공개 기록 쓰기 및 운영 시작·결과·끊김 처리의 실제 저장 검증도 아직 남아 있다. 두 기기 경쟁 테스트가 아니라 한 Unity 에디터에서 실행한 서버 기능 검사다.

## 근거와 증거

- [Where 기반 Update: 자기 데이터만 수정](https://docs.backnd.com/sdk-docs/backend/5.15.2/base/game-information/update/using-query/)
- [SetUpdate와 SetUpdateV2의 차이](https://docs.backnd.com/sdk-docs/backend/base/game-information/transaction/crud/update/)
- [TransactionWriteV2의 오류와 전체 실패 규칙](https://docs.backnd.com/sdk-docs/backend/base/game-information/transaction/write-v2/)
- 실행 증거: FunctionProbeAtomicPair.log, FunctionProbeAtomicPair.png.

로컬 기존 41개 검증은 통과했고 새 진단 코드는 공식 CLI 빌드 및 Unity 컴파일 후 실행했다. 로컬 메모리 정산 테스트 통과를 실제 운영 저장소 보장으로 해석하지 않는다.
