# 로드맵

- [x] M0: Git·.NET 9 WPF·dev-docs 초기화 — `644e343`, build 경고/오류 0 (2026-09-08)
- [x] M1: WPF 영역 선택 오버레이와 확대 미리보기 — build 경고/오류 0, 사용자 수동 UI 확인 통과 (2026-09-08)
- [x] M2: 화면 캡처·좌표 변환 Core/Infrastructure 계약 — build 경고/오류 0, Core 테스트 3개, 사용자 실제 캡처 확인 통과 (2026-09-08)
- [x] M2.1: 선택 영역 실시간 미리보기 — build 경고/오류 0, Core 테스트 3개, 사용자 화면 변화 확인 통과 (2026-09-08)
- [x] M3: 상호작용 드래그와 입력 gate·release 경로 — build 경고/오류 0, Core 테스트 7개, 기본 비활성 UI 확인 통과; 활성 입력은 `NEEDS_USER_UI_CHECK` (2026-09-08)
- [x] M4: 길게 누름 수치 제어와 A→B 단일 직선 획 — build 경고/오류 0, Core 테스트 7개, A/B·미리보기 UI 확인 통과; 활성 획은 `NEEDS_USER_UI_CHECK` (2026-09-08)
- [ ] M5: 범용 정밀 돋보기 — **BLOCKED_REPEAT_DRAG_RELEASE / 이중 포인터 BLOCKED**. 두 창·조작 유지 구현, 단위 테스트 44개 통과와 실제 수락을 구분한다. 범위/담당: 앱 전체/Codex 구현·사용자 UI 확인. 계획 `notes/plans/2026-09-08-magnifier-m5.md`, 다음: R0 차단 해결 후 전체 사용 흐름 수락 (2026-09-11).
- [ ] R0: 입력 전달·마우스 복귀, Infrastructure 담당. 큐 우선 처리와 원본 thread/root 감시 수정 뒤에도 사용자 실패. 최신 로그 2회는 누름 중 다른 root capture 전환으로 중지했다. 다음: 정상 창 인계와 실제 손실을 구별할 근거 수집·감시 정책 수정·연속 드래그 검증. build 0/0·44개 통과, 상세는 최신 인계 (2026-09-11).
- 커서 차단: hover·Up 뒤 복원은 프로브 확인, 드래그 중 강제 복귀는 추가 획 발생. UIAccess 설치/API 수락 뒤 WPF 수동·native 합성 전달 실패. 제품 연결 보류. 사용자가 표준 앱 관리자 검증을 인수했으며 자동 UI 검사는 중단했다.
- [ ] R1: 두 창 사용 흐름, App/Core 담당. 테두리·8개 손잡이·독립 렌즈·고정 제어·release 구현. 아래쪽 배치·제목 이동·이동 뒤 조작은 사용자 확인. 다음: R0 해결 뒤 독립 좌표·크기·마우스 복귀 확인, NEEDS_USER_UI_CHECK (2026-09-11).
- [ ] R2: 배율·배치·DPI·호환, App/Infrastructure 담당. 배치 저장·PerMonitorV2·화면 보정/변경 대응 구현, 음수 좌표·좁은 영역 단위 테스트 통과. 다음: 겹침·모니터 끝·혼합 DPI·브라우저/Blender/Windows 앱 실측, NEEDS_USER_UI_CHECK (2026-09-11).
- [ ] R3: 보조 조작, App/Core/Infrastructure 담당. A/B 보존. 다음: 핵심 수락 뒤 우클릭·휠·집기/놓기 등 추가, NOT_STARTED. Could 항목을 핵심 완료 선행 조건으로 삼지 않는다 (2026-09-11).
- 인계 기준: 이 문서와 `notes/runs/2026-09-11-drag-blocked-handoff.md`를 포함한 사용자 요청 커밋. 이전 `725cd05` 뒤 구현·진단·문서를 함께 보존하며 푸시 없음. 사용자 보고 실패를 수락 대기로 낮추지 않는다.
- 과거 A/B·표식 확인은 `notes/runs/2026-09-09-m5-realtime-magnifier-handoff.md`에 보존하며 새 완료 근거로 대신 쓰지 않는다.
