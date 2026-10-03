# 멀티당구 연습 물리·조준 표시 통합

적용일: 2026-09-27

## 적용 범위

로비 및 Hall을 통해 진입하는 기존 Pavilion 대전(3구·4구)과 TableSet07 기반 새 대전 경로를 대상으로 한다. 기존 Pavilion의 매칭 진입, 득점·마무리 규칙과 코인 처리 경로는 유지한다.

## 변경 사항

- Pavilion 타격을 연습과 같은 파워/팔로스루 모델로 변경: 기본 팔로스루 0.20, 속도 배율 1.04~0.92, 회전 지렛대 0.46. 기존 팔로스루 추가 전진력은 적용하지 않는다.
- 연습의 ClothPhysics, FollowThroughProfile, BallImpactTrial을 공통 사용한다. 미끄럼/구름 저항, 회전 유지, 두께별 충돌 보정, 공/쿠션 런타임 재질, Y 고정과 정지 기준을 맞춘다. 0.01초를 4분할한다.
- ShotPrediction을 IShotPredictionSource 기반으로 변경하여 두 경기장의 조준 예측이 같은 엔진을 사용한다. 기준 파워 0.40, 첫 적구의 단순 직선과 수구 궤적 표시를 유지한다. 실제 선택 파워의 전체 결과 예측선은 아니다.
- Pavilion에도 상단 중앙 예상 두께(% 및 두 공 겹침), 파워·팔로스루 0.00~1.00 수치 UI를 생성한다.
- Pavilion 수구 궤적에 시작점과 실시간 끝점을 유지한다. 새 대전 관전자 화면은 보간된 수구 위치로 궤적을 갱신하고 승인된 샷의 게이지 수치를 표시한다.
- 팔로스루만 변경해도 기존 조준 메시지를 송수신한다. 마우스를 놓는 프레임의 최종 이동도 게이지 입력에 반영한다.
- 기존 타격 메시지에 팔로스루·회전 유지값을 추가한다. 네 필드의 이전 문자열은 파싱 가능하지만 이전 물리 결과까지 재현하지는 않는다.
- 일반 매칭 버전을 pavilion-practice-physics-3으로 변경하여 이전 물리 클라이언트와의 혼합 매칭을 방지한다. 두 플레이어 모두 새 빌드가 필요하다. 새 대전 테스트의 별도 매칭 구분은 유지한다.

## 검증

- Unity 런타임 소스 컴파일 오류 없음.
- MultiplayerPresentationValidation: 실제 Pavilion 씬을 사용한 오프라인 검증 통과. 게이지·두께·첫 적구/수구 예측선, 실제 수구 궤적, 전송 문자열, 팔로스루만 변경한 송수신 경로 확인.
- 검증 타구의 실제 정지 위치와 공유 예측 결과의 차이: 수구 약 0.000000835 Unity 단위, 첫 적구 약 0.000000030 Unity 단위. 특정 배치 한 타구에 대한 결과이며 모든 입력의 오차 보장을 뜻하지 않는다.
- 보고서: Logs/multiplayer-presentation-validation.json
- 화면: Logs/pavilion-practice-aim.png, Logs/pavilion-practice-trail.png. 오프라인 fixture 화면의 기존 점수·이름 등은 씬 기본값이다.
- 연습/새 대전 전체 회귀 및 Windows 빌드를 통과했다. 상세 결과는 아래에 기록한다.
- 이번 변경 후 실제 두 계정의 온라인 재대전 실기는 별도로 확인해야 한다.

## 백업

변경 전 소스: Logs/MultiplayerPracticePresentationBackup-20260927-141631/

검증 실행: Unity 메뉴 Tools → Magic Online → Validate Multiplayer Practice Presentation. 현재 씬에 저장하지 않은 수정이 있으면 실행을 거절한다. 테스트용 씬 수정은 저장하지 않고 종료 후 원래 씬을 다시 연다.

## 전체 회귀 결과

2026-09-27 최신 소스로 PracticeIntegrationValidation 통과(completed=true, passed=true, errors=[]). 연습 3구·4구, 결과 복귀, 20개 리플레이 슬롯, 조작/재생 값, 대전 시작·타격·결과·복구·재대전, 대전 종료 후 연습 복귀, 물리 설정 복원을 확인했다. 관전자 수구 궤적 끝점과 승인된 타격의 게이지 수치 검사를 추가하여 통과했다. 누락 스크립트·참조·지원되지 않는 머티리얼은 모두 0건이다.

보존 보고서: Logs/multiplayer-practice-regression-20260927.json.

## Windows 빌드

2026-09-27 Windows 개발 빌드 성공(약 130초). 실행 파일: Builds/PracticeMatchTest-20260927-142917/MagicOnline2-Test.exe. 실행 파일을 직접 열면 일반 대전 경로를 사용하며, 같은 폴더의 Start-New-Match-Test.cmd는 새 대전 테스트 경로를 사용한다. 일반 대전/테스트 대전은 기존처럼 서로 별도 매칭된다. 배포 시 exe뿐 아니라 폴더 전체가 필요하다.

빌드 전 경로·포인터·복구·결과 검사를 모두 통과했다. 보고서: Logs/multiplayer-practice-build-20260927.json.

## 2026-09-27 경기 시작 공 배치 수정

Pavilion의 SetBalls 및 InitRandPosFlutter에서 Transform만 이동하던 처리를 Rigidbody.position과 함께 즉시 갱신하도록 변경했다. 첫 물리 스텝 전 위치 전송 및 BallState.Move에서 이전 물리 좌표를 읽을 수 있던 순서를 보완했다. 배치 시 이전 속도·회전·타격 프로필과 정지 대기 시간도 초기화한다. 물리 프로필 초기화 전 Transform 동기화를 수행한다.

검증에 Start 이전 3구 배치, 실제 흩뿌리기, 경기와 같은 BallState.Move 갱신, 정지 후 4구 재배치 및 흩뿌리기를 추가했다. 3구·4구 시작, 조준선·두께·게이지·전송값·실제 궤적·예측 대비 위치 검사를 모두 통과했다. 보고서: Logs/multiplayer-ball-startup-validation-20260927.json. 실제 두 계정 온라인 재대전은 이번 자동 검증에 포함하지 않았다.

수정본 Windows 빌드 성공: Builds/PracticeMatchTest-20260927-150905/MagicOnline2-Test.exe (78초). 빌드 시 경로·포인터·복구·결과 검사도 통과했다.

## 2026-09-27 대전 수구라인 디자인 통일

Pavilion 씬의 CueLine01~07, CueLinePath 및 함께 보이는 BallLine01~04에 TableSet07의 대응 라인 재질·폭 곡선·색상 공간·정렬 순서·끝점 설정을 적용했다. 연습 원본 재질을 공유하며 다른 물리/입력 컴포넌트는 변경하지 않았다. 실제 대전 씬의 조준선과 이동 궤적 화면을 확인했고, 기존 3구·4구 시작/가이드/두께/게이지/물리/전송 검증을 통과했다. 보고서: Logs/multiplayer-line-style-validation-20260927.json.
