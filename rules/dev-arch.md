# 아키텍처

## 현재 구조

- `src/Magnifier.App`: WPF 진입점·선택 오버레이·미리보기 창·기본 비활성 입력 gate와 A/B 시각 계획을 소유한다. 실제 A→B 전달 직전에는 미리보기가 선택 영역을 가로채지 않게 조정하고, Win32 구현을 직접 호출하지 않고 Core 계약을 소비한다.
- `src/Magnifier.Core`: 물리 화면 영역, BGRA32 프레임, 미리보기→원본 화면 좌표 환산, 단일 포인터 세션과 release 계약을 소유한다.
- `src/Magnifier.Infrastructure`: Windows GDI 화면 캡처와 `SendInput` 기반 포인터 입력을 구현한다. 대상 창 해석·권한 상승은 소유하지 않는다.
- `src/Magnifier.Core.Tests`: Core 좌표 환산·프레임 불변 조건·입력 gate와 release 전이를 단위 테스트한다.

## 의존성 규칙

- App은 Core 계약을 소비하며, composition root에서만 Infrastructure 구현을 만든다. Win32 P/Invoke를 직접 보유하지 않는다.
- Infrastructure는 Core를 참조할 수 있지만 App을 참조하지 않는다.
- 실제 입력은 runtime gate와 단일 release 경로 뒤에 둔다. 전달 목적지는 대상 창 핸들이 아니라 선택한 `ScreenRegion`의 물리 화면 좌표다.

## 검증 전략

- Core: 좌표 변환, 범위, 포인터 세션 gate·Down·Move·Up·release 단위 테스트.
- Infrastructure: 화면 캡처 실패·입력 해제·UIPI 거부 표기 테스트와 진단 모드.
- App: WPF 명령·캡처·입력 상태 표기 테스트와 사용자의 실제 대상 앱 확인.

## 다이어그램

`docs/diagrams/capture-flow.md`가 M2.1의 선택·캡처·미리보기 경계를, `docs/diagrams/input-session.md`가 M3 입력 세션의 release 전이를 설명한다.
