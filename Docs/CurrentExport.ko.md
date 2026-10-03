# 현재 프로젝트 내보내기 — 2026-09-24

Unity 6000.5.7f1 소스 프로젝트 ZIP이다. 새 폴더에 압축을 풀고 Unity Hub에서 해당 폴더를 프로젝트로 추가한다. Packages 복원에는 인터넷 연결이 필요할 수 있다.

Assets와 .meta, Packages, ProjectSettings, Docs, Tools를 포함한다. Library/Temp 캐시, Build/Builds 실행 파일, Logs, UserSettings, 이전 Exports와 프로젝트 밖 로컬 리플레이 저장 파일은 포함하지 않는다. 에디터에서 디스크에 저장하지 않은 변경도 포함하지 않는다. 문서에 언급된 Logs 증거 파일은 원래 작업 폴더에 남아 있다.

연습 3구·4구 및 리플레이와 기존 대전 경로를 포함한다. 새 연습 기반 대전은 개발용 친선 테스트 경로이며, 공개 경로 전환 및 서버 코인·전적 정산은 보류 상태다. 최신 실기 확인은 235546 빌드의 4구 이동 중 패킷 손실 시험까지 반영했다. 상세 완료 범위와 남은 검증은 MultiplayerDevelopmentProgress.ko.md 및 PracticeMultiplayerTwoClientTest.ko.md를 참고한다.

ZIP 안의 EXPORT-MANIFEST.json에 각 소스 파일의 SHA-256을 기록하고 압축 파일 내용 및 내보내기 완료 시 원본과 비교한다. ZIP 옆의 .zip.sha256은 압축 파일 자체의 검사값이다. 실제 새 폴더에서 Unity 재임포트 검증을 수행했다는 의미는 아니다.

같은 방식으로 다시 생성하려면 프로젝트 폴더에서 `python Tools/export_project.py`를 실행한다. 이전 내보내기는 보존한다.
