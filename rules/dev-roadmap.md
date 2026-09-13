# 로드맵

- 2026-09-13 공개 기준: [Pinte v0.1.0-beta.1 pre-release](https://github.com/ai-blink/pinte/releases/tag/v0.1.0-beta.1)를 main `83074e5`와 태그 `v0.1.0-beta.1`으로 배포했다. 표준 build 0/0·자동 테스트 116개 통과는 릴리즈 기준이며, 실제 UI와 대상 앱 호환성은 `NEEDS_USER_UI_CHECK`다.

- [ ] 설정 모달 후속 확인: 초기화 순서 오류 수정은 전체 116개 자동 테스트에 포함돼 통과했다. 진입 창/일반·컴팩트 설정 열기, 메뉴 전환·변경 반영·닫은 뒤 재개는 `NEEDS_USER_UI_CHECK`다. [기록](../notes/runs/2026-09-12-settings-modal-init-fix.md).

- [ ] 첫 프레임 무한 대기 후속 확인: 순환 대기 수정은 전체 116개 자동 테스트에 포함돼 통과했다. 사용자 재확인 대상은 최초 확대/영역 편집 후 첫 화면과 클릭·드래그다. [기록](../notes/runs/2026-09-12-first-frame-wait-fix.md).

- [ ] 컴팩트 렌즈·원본 표시 정책: Hidden 기본/5초/항상 윤곽선, 재편집→완료, 공통 ToolbarPlacement, 실제 8방향 렌즈 리사이즈를 구현했다. 자동 회귀와 표준 build는 릴리즈 기준으로 통과했다. UI·대상 반응은 사용자 확인 대기, `NEEDS_USER_UI_CHECK`다. 다음은 [수동 행렬](../doc/compact-lens-validation.md)에서 확인된 차단 수정이다.

- [ ] UI·설정 개편: 1번/3번 배치, 크기·비율 패널, 왼쪽 메뉴 설정 모달을 WPF에 구현. P0/P1 보완 뒤 승인 목업 토큰·카드·도구막대·테두리 시각 정비, 시작 라벨/크기 명칭/넓은 hit area, 고정 렌즈+중앙 crop, 두 창 `×`, 24 DIP 스크롤바와 `✋ 이동`을 반영했다. build 0/0·총 52 테스트 통과. 다음: 표준 실행본으로 고정 렌즈 직접 조작·이동 후 재개를 포함한 수동 확인의 BLOCKER만 수정한다. 실제 UI·대상 반응은 `NEEDS_USER_UI_CHECK`. [시각 기록](../notes/runs/2026-09-12-magnifier-visual-refinement.md) (2026-09-12).
- [x] M0: Git·.NET 9 WPF·dev-docs 초기화 — `644e343`, build 경고/오류 0 (2026-09-08)
- [x] M1: WPF 영역 선택 오버레이와 확대 미리보기 — build 경고/오류 0, 사용자 수동 UI 확인 통과 (2026-09-08)
- [x] M2: 화면 캡처·좌표 변환 Core/Infrastructure 계약 — build 경고/오류 0, Core 테스트 3개, 사용자 실제 캡처 확인 통과 (2026-09-08)
- [x] M2.1: 선택 영역 실시간 미리보기 — build 경고/오류 0, Core 테스트 3개, 사용자 화면 변화 확인 통과 (2026-09-08)
- [x] M3: 상호작용 드래그와 입력 gate·release 경로 — build 경고/오류 0, Core 테스트 7개, 기본 비활성 UI 확인 통과; 활성 입력은 `NEEDS_USER_UI_CHECK` (2026-09-08)
- [x] M4: 길게 누름 수치 제어와 A→B 단일 직선 획 — build 경고/오류 0, Core 테스트 7개, A/B·미리보기 UI 확인 통과; 활성 획은 `NEEDS_USER_UI_CHECK` (2026-09-08)
- [ ] M5: 범용 정밀 돋보기 — **핵심 수정 USER_CONFIRMED_PASS**, `v0.1.0-beta.1` 공개 pre-release를 배포했지만 전체 수락은 진행 중. 범위/담당: 앱 전체/Codex 구현·사용자 UI 확인. 계획 `notes/plans/2026-09-08-magnifier-m5.md`, 다음: 이중 포인터·경계/오류별 복귀·범용 호환 확인.
- 완료 근거 — 영역 지정·반복 드래그 해제 수정: 테두리로 지정 후 확대, 드래그 감시 조건 수정. 사용자 “아주 잘됨”, 표준 build 0/0. `notes/runs/2026-09-12-region-selection-drag-confirmed.md` (2026-09-12).
- [ ] R0: 입력 전달·마우스 복귀, Infrastructure 담당. 드래그 유지 수정은 사용자 확인 완료, 관련 자동 테스트는 릴리즈에서 실행해 통과했다. 다음: 이중 포인터와 경계/오류별 release·복귀를 사용자 확인 범위에 따라 후속 처리.
- 커서 차단: hover·Up 뒤 복원은 프로브 확인, 드래그 중 강제 복귀는 추가 획 발생. UIAccess 설치/API 수락 뒤 WPF 수동·native 합성 전달 실패. 제품 연결 보류. 사용자가 표준 앱 관리자 검증을 인수했으며 자동 UI 검사는 중단했다.
- [ ] R1: 두 창 사용 흐름, App/Core 담당. 영역 지정 → 이 영역 확대 → 조작 흐름은 사용자 확인 완료. 다음: 독립 좌표·크기·선택 취소 등 세부 조건을 별도 확인, NEEDS_USER_UI_CHECK (2026-09-12).
- [ ] R2: 배율·배치·DPI·호환, App/Infrastructure 담당. 배치 저장·PerMonitorV2·화면 보정/변경 대응 구현, 음수 좌표·좁은 영역 단위 테스트 통과. 다음: 겹침·모니터 끝·혼합 DPI·브라우저/Blender/Windows 앱 실측, NEEDS_USER_UI_CHECK (2026-09-11).
- [ ] R3: 보조 조작, App/Core/Infrastructure 담당. A/B 보존. 다음: 핵심 수락 뒤 우클릭·휠·집기/놓기 등 추가, NOT_STARTED. Could 항목을 핵심 완료 선행 조건으로 삼지 않는다 (2026-09-11).
- 저장 기준: `v0.1.0-beta.1`은 main `83074e5`와 태그 `v0.1.0-beta.1`으로 푸시·공개 pre-release까지 완료했다. 이전 실패 기록은 보존하며, 다음 변경은 별도 사용자 요청으로 릴리즈한다.
- 과거 A/B·표식 확인은 `notes/runs/2026-09-09-m5-realtime-magnifier-handoff.md`에 보존하며 새 완료 근거로 대신 쓰지 않는다.
