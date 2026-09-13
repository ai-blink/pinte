# 개발 진행

- 2026-09-13: **Pinte v0.1.0-beta.1 공개 pre-release**를 main `83074e5`·태그 `v0.1.0-beta.1`으로 게시했다. 영어/한국어/중국어(간체)/일본어 README와 Windows x64 자체 포함 ZIP을 제공한다. 표준 build는 경고0/오류0, 전체 자동 테스트 116개가 통과했다. 공개 배포본은 코드 서명되지 않았고 실제 UI·대상 앱 호환성은 여전히 `NEEDS_USER_UI_CHECK`다. [릴리즈](https://github.com/ai-blink/pinte/releases/tag/v0.1.0-beta.1).

- 2026-09-13: 일시 캡처 실패는 relay 요청을 취소하지 않고 release·일시 정지 후 자동 재시도한다. 새 캡처 프레임을 받은 뒤에만 조작을 재개한다. 손 도구의 켜짐/꺼짐 상태를 구분하고, 리사이즈 중 렌즈 여백을 44 DIP에서 24 DIP로 줄였다. [컴팩트 렌즈 수동 시나리오](../doc/compact-lens-validation.md)와 기존 실행 기록은 세부 확인 근거로 보존한다.
- M0~M4 초기화·영역 선택·캡처·입력 gate·보조 A/B 이력은 각 notes/runs/2026-09-08-* 기록과 dev-roadmap의 완료 항목을 따른다. 이전 수락은 이번 기능의 통과 근거가 아니다.
- 2026-09-12: UI·설정·시각 정비 — 승인 목업 토큰·카드·도구막대와 화면 피드백(라벨, 넓은 hit area, 고정 렌즈/crop)을 반영했다. 두 창 `×`, 24 DIP 스크롤바, relay를 중단하는 `✋ 이동`을 추가했다. build 0/0·Core41/Infra11, 52 PASS. UI 확인은 `NEEDS_USER_UI_CHECK`. [기록](../notes/runs/2026-09-12-magnifier-visual-refinement.md). 사용자 요청으로 로컬 커밋.
- 2026-09-08~09: M5에서 보고된 미리보기 크기 변경·Blender 빈 프레임 차단 문제를 수정했다. 선택 오버레이·미리보기의 항상 위를 제거하고, 10fps 캡처마다 미리보기를 강제 활성화하던 호출을 제거했다. 미리보기는 처음 선택 영역 밖에 배치되고, 이후 원본 영역과 겹치면 마지막 정상 프레임을 유지하며 갱신을 보류한다. 솔루션 build 경고·오류 0, Core 테스트 7개 통과, 사용자가 Blender 5.2 미리보기의 정상 표시를 확인했다. 활성 입력 검증은 `NEEDS_USER_UI_CHECK`다. 실행 기록은 `notes/runs/2026-09-08-m5-window-layering.md`에 남겼다.
- 2026-09-09: 위 M5의 원본 영역 밖 배치·겹침 갱신 보류는 미니맵 요구와 모순돼 교체했다. 미리보기 창은 Windows 캡처에서 제외하고 겹쳐도 선택 영역을 계속 갱신한다. A/B 표식은 정수 입력 좌표와 분리한 정규화 좌표로 고정하고, 두 지점 사이에 점선 계획을 표시한다. 솔루션 build 경고·오류 0, Core 테스트 7개 통과, 사용자가 A/B 표식 중심과 클릭 위치의 일치를 확인했다. 실행 기록은 `notes/runs/2026-09-09-m5-minimap-ab-preview.md`에 남겼다.
- 2026-09-09: 사용자 피드백으로 M5의 정본 목표를 전면 교체했다. 선택한 보이는 화면 좌표가 유일한 전달 대상이며, A/B 표식과 입력 지점은 같은 클릭 의미를 가져야 한다. 브라우저·Blender는 대상 창 제한이 아닌 호환성 검증 표면이다. 기존 실행본에는 없는 UI를 있다고 말하지 않는 것을 검증 규율로 명시했다. 새 계획은 `notes/plans/2026-09-08-magnifier-m5.md`다.
- 2026-09-09: M5 A/B 전용 흐름을 표준 실행본으로 build/relaunch했다. 미리보기 안 입력 허용·허용 상태 유지·A/B 전용 클릭·A→B 전달 중 미리보기 숨김을 App에 반영했고, build 경고·오류 0과 Core 테스트 7개를 확인했다. 사용자는 확대된 화면을 계속 조작하는 실제 돋보기는 동작하지 않는다고 확인했다. 상세와 다음 인계는 `notes/runs/2026-09-09-m5-realtime-magnifier-handoff.md`다.
- 2026-09-10: 요청에 따라 관련 로컬 3개 프로젝트·Magnifier·추가 MagnifierApp 레퍼런스, 공개 저장소 8개 README, Microsoft/OptiKey 자료를 탐색했다. 직접 드래그 가능·정밀 맞춤 어려움이라는 답변을 반영해 제품 의도·M5 계획과 기능/UX 초안·배치 목업을 작성했다. `notes/brainstorm/2026-09-10_magnifier-redesign_design.md`는 설계 확정 전이며 제품 소스·실행본은 바꾸지 않았다.
- 2026-09-10 후속: 사용자가 oCam 같은 실제 원본 테두리 창+독립 이동 확대 렌즈 창 구조를 확인했다. 테두리 이동·크기는 캡처 영역을 바꾸고 렌즈 이동은 원본 영역을 유지하도록 계획·수락 기준·목업을 수정했다. 입력 엔진과 실제 앱 동작은 검증 전이다.
- 2026-09-10 구현: 상시 원본 테두리·독립 렌즈·고정 마우스 제어·배율·배치 저장·기본 off를 구현했다. Infrastructure 전용 hook/태그 SendInput 중계와 경계 Up/해제 대기, 캡처 지연·capture 손실·실패 해제 후보를 연결했다. 표준 build 경고·오류 0, Core 23개 테스트 통과. 기존 A/B는 접힌 보조 도구로 보존했다.
- 최초 구현 검증은 `BLOCKED / NEEDS_USER_UI_CHECK`였다. native 프로브의 목표 Down 수신 뒤 외부 입력 guard가 작동해 중단했으며 상세는 `notes/runs/2026-09-10-two-window-implementation.md`에 보존한다.
- 2026-09-10 커밋: 사용자 요청으로 계획·UX·결정·상태도를 동기화하고 구현·테스트 45개 파일을 main의 `6b5c0df`에 저장했다. `notes/transfers/` 제외, 푸시 없음.
- 2026-09-10 R0 재검사: 사용자 직접 조작 확인 뒤 재개. 대상 원본/중계 Down·Up 중복을 재현했고 hook 내 직접 주입을 FIFO 처리로 옮겨 분리/겹침 곡선·왕복과 경계 Up·재무장 프로브 3개를 통과했다. 표준 build 경고·오류 0, Core 23개 통과. Main Hide/Owner 관계 수정 뒤 표준 앱 두 창 표시는 사용자 확인을 받았다.
- 2026-09-10 창 이동: 손잡이 수정·아래쪽 배치, build 0/0·Core 28·native 4개 통과. 사용자 이동 확인. `notes/runs/2026-09-10-window-drag-placement.md`.
- 2026-09-10 조작 유지: 확대 열기·일시 정지 뒤 자동 재개, Core 34·native 6개 통과. 사용자 클릭·드래그 확인 뒤 재실패.
- 2026-09-10 커서: hover·Up 복원, build 0/0·Core 34개·native 6개 통과. 드래그 중 이중 포인터 `BLOCKED`.
- 2026-09-10 OS 변환: 승인된 UIAccess 설치/API 수락 완료. WPF 수동·native 합성 클릭 전달 실패. build 0/0·Core 34개. 사용자 관리자 검증 진입 파일 준비, 자동 UI 중단. `notes/runs/2026-09-10-uiaccess-pointer-trial.md`.
- 2026-09-11 인계: 두 차례 수정 뒤에도 실패. 다른 root capture 전환 2회가 누름 중 중지를 유발했다. build 0/0·44개 통과와 별개로 BLOCKED. 관련 변경 커밋·푸시 없음. `notes/runs/2026-09-11-drag-blocked-handoff.md`.
- 2026-09-12: 영역 지정 단계·드래그 유지 수정 **USER_CONFIRMED_PASS** — 사용자 “아주 잘됨”. 표준 build 0/0, 테스트 실행은 사용자에게 맡김. 관련 변경·문서를 한 커밋으로 보존. 상세: `notes/runs/2026-09-12-region-selection-drag-confirmed.md`.
