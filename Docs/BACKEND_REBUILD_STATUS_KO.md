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
| CommonCode | 42867 | 파일 ID 265284 (2026-10-05 사용자 교체 정보) |
| SkillMatch | 42868 | 파일 ID 265095 (2026-10-04 사용자 교체 정보) |
| Attendance | 42869 | 파일 ID 265286 (2026-10-05 사용자 교체 정보) |

Assets/Scripts/Often/ReferArticle.cs의 차트 ID를 위 ID로 변경했다. 2026-09-18 테스트 파일 3개를 업로드하고 적용된 차트 열에서 적용 완료를 확인했다. 당시 원본 파일은 Docs/BackendSeed에 보관한다.

2026-10-04: 사용자 요청으로 SkillMatch_Id를 새 파일 ID 265095로 변경했다(이전 파일 ID 261569). LoadChartRows는 selectedChartFileId로도 조회하므로 뒤끝 콘솔에서 해당 파일이 선택·적용된 상태여야 한다. 새 파일 내용과 실제 서버 조회는 이번 변경에서 검증하지 않았으며, BackendSeed/SkillMatch.csv는 이전 테스트 데이터다.

2026-10-05: 사용자 요청으로 CommonCode_Id를 새 파일 ID 265284로 변경했다(이전 파일 ID 261567). LoadChartRows는 selectedChartFileId로도 조회하므로 뒤끝 콘솔에서 해당 파일이 선택·적용된 상태여야 한다. 새 파일 내용과 실제 서버 조회는 이번 변경에서 검증하지 않았으며, BackendSeed/CommonCode.csv는 이전 테스트 데이터다.

2026-10-05: 사용자 요청으로 Attendance_Id를 새 파일 ID 265286으로 변경했다(이전 파일 ID 261570). 출석 차트의 보상 종류 칼럼은 RewardType에서 RewardTypeCd로 변경되어 LoadAttendance에서 새 칼럼을 읽고 내부 RewardType 필드에 매핑한다. UserAttendance 게임 정보 테이블의 RewardType 저장·조회 형식은 유지한다. 뒤끝 콘솔에서 해당 파일이 선택·적용된 상태여야 하며, 새 파일 내용과 실제 서버 조회는 이번 변경에서 검증하지 않았다. BackendSeed/Attendance.csv는 이전 칼럼명을 사용하는 과거 테스트 데이터다.

## 남은 작업

- 테스트용 차트 수치를 실제 운영 규칙에 맞게 조정.
- 새 프로젝트의 인증 설정과 Unity TheBackendSettings 일치 여부 확인.
- 회원 가입/로그인, UserMain 초기 생성, 차트 조회, 출석 데이터 생성 실제 실행 검증.

제안한 테스트 기준값: 레벨 진행 기준 10경기, 3구·4구 경기장 각 1개, 목표 10점, 시간 20분, 입장 코인 범위 1~1,000,000, 마무리 미션 없음, 출석 7일 매일 코인 10. 테스트 복구용으로 업로드 및 적용했다. 3구는 3쿠션, 4구는 쿠션 제한 없음이다. RewardType=1을 테스트 코인 코드로 저장했으나 기존 코드에 보상 종류의 명확한 매핑과 실제 잔액 지급 구현이 확인되지 않아 실제 코인 지급까지 검증된 것은 아니다. 레벨 진행 기준도 표시용 조회 값이며 실제 레벨업 로직 검증과는 별개다.

이전 PROJECT_GUIDE_KO.md는 2026-09-17 최초 조사 기록이다. 이후 SDK는 5.18.17로 업데이트됐고 Backend.Initialize() 및 CDN 차트 API로 전환됐다. 원격 테이블·차트의 현재 상태는 이 문서를 우선한다.
