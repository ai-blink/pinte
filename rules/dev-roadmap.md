# 로드맵

- [x] M0: Git·.NET 9 WPF·dev-docs 초기화 — `644e343`, build 경고/오류 0 (2026-09-08)
- [x] M1: WPF 영역 선택 오버레이와 확대 미리보기 — build 경고/오류 0, 사용자 수동 UI 확인 통과 (2026-09-08)
- [x] M2: 화면 캡처·좌표 변환 Core/Infrastructure 계약 — build 경고/오류 0, Core 테스트 3개, 사용자 실제 캡처 확인 통과 (2026-09-08)
- [x] M2.1: 선택 영역 실시간 미리보기 — build 경고/오류 0, Core 테스트 3개, 사용자 화면 변화 확인 통과 (2026-09-08)
- [x] M3: 상호작용 드래그와 입력 gate·release 경로 — build 경고/오류 0, Core 테스트 7개, 기본 비활성 UI 확인 통과; 활성 입력은 `NEEDS_USER_UI_CHECK` (2026-09-08)
- [x] M4: 길게 누름 수치 제어와 A→B 단일 직선 획 — build 경고/오류 0, Core 테스트 7개, A/B·미리보기 UI 확인 통과; 활성 획은 `NEEDS_USER_UI_CHECK` (2026-09-08)
- [ ] M5: 보이는 화면 영역의 실시간 돋보기 조작 — A/B 표식·한 획은 build 경고·오류 0, Core 테스트 7개, 표식 일치까지 확인했으나, 확대 미리보기의 지속 Down → Move → Up 전달은 `BLOCKED`다. 다음은 미리보기 capture와 전역 입력 전달을 함께 성립시키는 별도 실시간 조작 모드 설계·구현·브라우저/Blender 검증이다. 정본: `notes/plans/2026-09-08-magnifier-m5.md`, 인계: `notes/runs/2026-09-09-m5-realtime-magnifier-handoff.md`
