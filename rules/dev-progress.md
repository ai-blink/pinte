# 개발 진행

- 2026-09-17: D-030 실행본 첫 publish가 native self-extract 옵션 없이 만들어져 WPF `SourceInitialized`에서 `DllNotFoundException`으로 종료했다. 배포본은 즉시 이전 SHA `46F4768D…401872`로 복원했고, `PublishSingleFile=true`일 때 `IncludeNativeLibrariesForSelfExtract=true`를 프로젝트에 고정했다. 새 self-extract publish SHA `9D5A01C0…A6747D`를 `C:\app\Magnifier.App.exe`에 교체하고 PID 16324가 4초 이상 유지됨을 확인했다. 이는 진입 창 시작 확인이며 렌즈·relay 장기 동작은 여전히 `NEEDS_USER_UI_CHECK`다.
- 2026-09-17: 실행 중 재발 로그에서 `relay-path` 106회가 callback→큐→대상 Down까지 도달했고, 이후 `hook-loss` 38회·같은 워커의 재설치 12회 뒤 recovery budget 소진이 입력 요청을 스스로 취소한 것을 확인했다. D-030은 검증된 훅 소실 때 기존 STA 메시지 스레드를 정리하고 새 워커에 마우스·물리 Esc 훅을 함께 설치한다. Esc 훅 설치 실패는 fail-closed, WPF Esc는 provenance 판단을 하지 않는다. build 0/0·총 139 테스트 통과. 사용자 실제 장기 실행·key-demo-osk 병행은 `NEEDS_USER_UI_CHECK`.
- 2026-09-16: Pinte relay 무응답 재발을 조사해 D-029(60초 공백 뒤 복구 예산 초기화·소유 press의 release/stop)를 반영했다. 물리 Esc만 취소하고 주입 Esc는 무시하도록 분리했으며, `relay-path`에 자동 재개 전환도 남긴다. key-demo-osk 직접 원인은 미확정이다. build 0/0·총 137 테스트 통과. SHA-256 `46F4768D…401872` publish를 `C:\app`에 교체·시작했고 실제 재현은 `NEEDS_USER_UI_CHECK`. [기록](../notes/runs/2026-09-16-hook-recovery-path-trace.md).
- 2026-09-15: D-027 워치독이 회귀를 냈다 — 로그로 (1) 상시 주입 커서 오탐 폭주(재설치 1000+회) (2) 두 인스턴스 동시 실행이 훅 체인을 서로 느리게 함 (3) 누름 중 재설치가 드래그를 끊음을 확정. 단일 인스턴스 Mutex·재설치 전 자기검증·진행 중 입력 보호·재설치 상한으로 수정(D-028). build 0/0, 테스트 128개 통과. 두 번째 실행 거부(exit 5) 실증, 새 프로세스는 진입 창에서 재설치 0. `C:\app` 교체. 실제 드래그·클릭은 `NEEDS_USER_UI_CHECK`. [기록](../notes/runs/2026-09-15-hook-watchdog-storm-regression.md).
- 2026-09-14: 사용자 보고 "잘되다가 클릭해도 입력 전송이 안 됨·가상 포인터 없음"을 훅의 조용한 제거(`LowLevelHooksTimeout`)로 좁혀 훅 생존 감시·자동 재설치를 넣었다(D-027, 이후 D-028이 자기검증으로 대체). [기록](../notes/runs/2026-09-14-hook-liveness-watchdog.md).
- 2026-09-14: **Pinte v0.1.0 정식 릴리즈**를 태그 `v0.1.0`과 Windows x64 자체 포함 ZIP으로 게시한다. 세밀 배율 제어, geometry 구성 재시도, stale-frame 진단과 중복 Left Down 방어를 포함한다. 표준 build는 경고0/오류0, 전체 자동 테스트 120개가 통과했다. 코드 서명과 실제 UI·대상 앱 호환성은 여전히 `NEEDS_USER_UI_CHECK`다. [릴리즈](https://github.com/ai-blink/pinte/releases/tag/v0.1.0).

- 2026-09-13: **Pinte v0.1.0-beta.1 공개 pre-release**를 main `83074e5`·태그 `v0.1.0-beta.1`으로 게시했다. 영어/한국어/중국어(간체)/일본어 README와 Windows x64 자체 포함 ZIP을 제공한다. 표준 build는 경고0/오류0, 전체 자동 테스트 116개가 통과했다. 공개 배포본은 코드 서명되지 않았고 실제 UI·대상 앱 호환성은 여전히 `NEEDS_USER_UI_CHECK`다. [릴리즈](https://github.com/ai-blink/pinte/releases/tag/v0.1.0-beta.1).

- 2026-09-13: 일시 캡처 실패는 relay 요청을 취소하지 않고 release·일시 정지 후 자동 재시도한다. 새 캡처 프레임을 받은 뒤에만 조작을 재개한다. 손 도구의 켜짐/꺼짐 상태를 구분하고, 리사이즈 중 렌즈 여백을 44 DIP에서 24 DIP로 줄였다. [컴팩트 렌즈 수동 시나리오](../doc/compact-lens-validation.md)와 기존 실행 기록은 세부 확인 근거로 보존한다.
- M0~M4 초기화·영역 선택·캡처·입력 gate·보조 A/B 이력은 각 notes/runs/2026-09-08-* 기록과 dev-roadmap의 완료 항목을 따른다. 이전 수락은 이번 기능의 통과 근거가 아니다.
- 2026-09-12: UI·설정·시각 정비 — 승인 목업 토큰·카드·도구막대와 화면 피드백(라벨, 넓은 hit area, 고정 렌즈/crop)을 반영했다. 두 창 `×`, 24 DIP 스크롤바, relay를 중단하는 `✋ 이동`을 추가했다. build 0/0·Core41/Infra11, 52 PASS. UI 확인은 `NEEDS_USER_UI_CHECK`. [기록](../notes/runs/2026-09-12-magnifier-visual-refinement.md). 사용자 요청으로 로컬 커밋.
- 2026-09-11 인계: 두 차례 수정 뒤에도 실패. 다른 root capture 전환 2회가 누름 중 중지를 유발했다. build 0/0·44개 통과와 별개로 BLOCKED. 관련 변경 커밋·푸시 없음. `notes/runs/2026-09-11-drag-blocked-handoff.md`.
- 2026-09-12: 영역 지정 단계·드래그 유지 수정 **USER_CONFIRMED_PASS** — 사용자 “아주 잘됨”. 표준 build 0/0, 테스트 실행은 사용자에게 맡김. 관련 변경·문서를 한 커밋으로 보존. 상세: `notes/runs/2026-09-12-region-selection-drag-confirmed.md`.
- 2026-09-08~10 초기 M5·R0 이력(14항목)은 `../memory/archive-2026-09.md`에 보존한다.
