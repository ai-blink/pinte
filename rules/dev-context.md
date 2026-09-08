# 재개 컨텍스트

- 브랜치: `main`.
- 현재 상태: M3 기본 비활성 입력 gate·release 경로까지 완료됐다. 다음 목표는 M4 길게 누름 수치 제어와 A→B 단일 직선 획의 설계·구현이다.
- 사용자 승인 설계: 범용 Windows 오버레이, 이동 가능한 상호작용 확대창, 길게 누름 수치 조절, A→B 단일 직선 획.
- 최근 증거: `dotnet build Magnifier.slnx --nologo` 경고·오류 0, `dotnet test Magnifier.slnx --nologo` Core 테스트 7개 통과, 사용자가 기본 해제 checkbox와 비활성 드래그 안내 지속을 확인했다 (2026-09-08).
- M3 활성 입력 확인: 전역 포인터를 실제로 보내는 checkbox 활성화 검증은 `NEEDS_USER_UI_CHECK`다. 대상 앱·권한 상태를 정한 사용자 승인 전에는 실행하지 않는다.
- M4 시작 조건: 길게 누름과 A→B 단일 직선 획의 완료선·수락 확인·stop rule을 먼저 계획에 고정한다.
