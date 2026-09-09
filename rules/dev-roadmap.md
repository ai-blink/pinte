# 로드맵

- [x] M0: Git·.NET 9 WPF·dev-docs 초기화 — `644e343`, build 경고/오류 0 (2026-09-08)
- [x] M1: WPF 영역 선택 오버레이와 확대 미리보기 — build 경고/오류 0, 사용자 수동 UI 확인 통과 (2026-09-08)
- [x] M2: 화면 캡처·좌표 변환 Core/Infrastructure 계약 — build 경고/오류 0, Core 테스트 3개, 사용자 실제 캡처 확인 통과 (2026-09-08)
- [x] M2.1: 선택 영역 실시간 미리보기 — build 경고/오류 0, Core 테스트 3개, 사용자 화면 변화 확인 통과 (2026-09-08)
- [x] M3: 상호작용 드래그와 입력 gate·release 경로 — build 경고/오류 0, Core 테스트 7개, 기본 비활성 UI 확인 통과; 활성 입력은 `NEEDS_USER_UI_CHECK` (2026-09-08)
- [x] M4: 길게 누름 수치 제어와 A→B 단일 직선 획 — build 경고/오류 0, Core 테스트 7개, A/B·미리보기 UI 확인 통과; 활성 획은 `NEEDS_USER_UI_CHECK` (2026-09-08)
- [ ] M5: 범용 정밀 돋보기 — 실제 두 창과 입력 중계 후보 구현, 최종 build 경고·오류 0, Core 23개 통과. 실사용 수락은 `BLOCKED / NEEDS_USER_UI_CHECK`. UX: `notes/brainstorm/2026-09-10_magnifier-redesign_design.md`, 계획: `notes/plans/2026-09-08-magnifier-m5.md`.
  - [ ] R0: hook 내 직접 주입을 FIFO 처리로 옮겨 중복 Down/Up 수정. 분리/겹침 곡선·왕복과 경계 Up·재무장 차단을 native 프로브 3개에서 확인. 실제 장치의 제품 조작·복귀 확인 필요.
  - [ ] R1: 상시 테두리/8개 크기 손잡이·독립 렌즈·직접 중계·고정 중지/복귀·release 구현. Main Hide/Owner 관계 수정 뒤 두 창 표시는 사용자 확인. 이동·크기·마커 조작·복귀는 실제 UI 확인 중.
  - [ ] R2: 배율/맞춤 표시·물리 배치 저장·PerMonitorV2·실패 표시·렌즈 이동 후 화면 안 보정·화면 변경 시 진입 복원 구현. 겹침·모니터 끝·제어부 복구·혼합 DPI·앱 호환 실측 및 보완 필요.
  - [ ] R3: A/B는 접힌 선택 도구로 보존. 우클릭·휠·집기/놓기 등의 확장은 핵심 검증 뒤 진행. 두 번 클릭의 native 더블클릭 의미도 실제 대상 확인 필요.
- 기준 구현 커밋: `6b5c0df`. 최신 검증 기록: `notes/runs/2026-09-10-r0-relay-retest.md`. 사용자 조작 중 자동 입력을 보내지 않으며, 새 UI 충돌은 사용자 확인 뒤 재개한다.
- 과거 A/B·표식 확인은 `notes/runs/2026-09-09-m5-realtime-magnifier-handoff.md`에 보존하며 새 완료 근거로 대신 쓰지 않는다.
