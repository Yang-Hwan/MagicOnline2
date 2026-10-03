# magicCushion 뒤끝 재구성 현황

2026-09-18, 로그인된 뒤끝 콘솔의 magicCushion 프로젝트에서 확인 및 생성.

## 완료

| 게임 정보 테이블 | 분류 | 스키마 | 상태 |
|---|---|---|---|
| UserMain | Public | 미정의 | 활성 |
| UserAttendance | Private | 미정의 | 활성 |
| UserAttendanceTerm | Private | 미정의 | 활성 |

UserMain은 상대 프로필과 전적 조회 코드 때문에 Public으로 구성했다. 실제 회원 정보 행은 로그인 후 클라이언트의 Insert 코드가 생성한다. 회원 계정이나 과거 데이터를 수동 복원하지 않았다.

| 차트명 | 새 차트 ID | 파일 적용 |
|---|---|---|
| CommonCode | 42867 | CommonCode.csv / 261567 |
| SkillMatch | 42868 | SkillMatch.csv / 261569 |
| Attendance | 42869 | Attendance.csv / 261570 |

Assets/Scripts/Often/ReferArticle.cs의 차트 ID를 위 ID로 변경했다. 2026-09-18 테스트 파일 3개를 업로드하고 적용된 차트 열에서 적용 완료를 확인했다. 원본 파일은 Docs/BackendSeed에 보관한다.

## 남은 작업

- 테스트용 차트 수치를 실제 운영 규칙에 맞게 조정.
- 새 프로젝트의 인증 설정과 Unity TheBackendSettings 일치 여부 확인.
- 회원 가입/로그인, UserMain 초기 생성, 차트 조회, 출석 데이터 생성 실제 실행 검증.

제안한 테스트 기준값: 레벨 진행 기준 10경기, 3구·4구 경기장 각 1개, 목표 10점, 시간 20분, 입장 코인 범위 1~1,000,000, 마무리 미션 없음, 출석 7일 매일 코인 10. 테스트 복구용으로 업로드 및 적용했다. 3구는 3쿠션, 4구는 쿠션 제한 없음이다. RewardType=1을 테스트 코인 코드로 저장했으나 기존 코드에 보상 종류의 명확한 매핑과 실제 잔액 지급 구현이 확인되지 않아 실제 코인 지급까지 검증된 것은 아니다. 레벨 진행 기준도 표시용 조회 값이며 실제 레벨업 로직 검증과는 별개다.

이전 PROJECT_GUIDE_KO.md는 2026-09-17 최초 조사 기록이다. 이후 SDK는 5.18.17로 업데이트됐고 Backend.Initialize() 및 CDN 차트 API로 전환됐다. 원격 테이블·차트의 현재 상태는 이 문서를 우선한다.
