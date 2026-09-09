# 아키텍처

## 현재 구조

- `src/Magnifier.App`: Main은 composition root·진입/복귀·비중첩 캡처와 배치 저장을 소유한다. SelectionOverlayWindow는 상시 투명 원본 테두리·8개 크기 손잡이, SelectionPreviewWindow는 독립 렌즈·배율·고정 제어·가상 포인터와 선택형 A/B를 소유한다. 직접 조작에 WPF mouse capture를 사용하지 않는다.
- `src/Magnifier.Core`: ScreenRegion/LensViewport는 독립된 원본·렌즈 물리 좌표를 보관한다. LensInputState/PointerInputSession은 기본 off·명시 시작·물리 해제 대기·Up 실패 재시도를 관리한다. ILivePointerRelay/IWindowEnvironment는 App이 사용하는 계약이다.
- `src/Magnifier.Infrastructure`: GDI 캡처, 태그 SendInput, 전용 hook 스레드의 논리 포인터·layered 입력 통과·캡처 freshness/capture 감시, 물리 창 배치를 구현한다. hook은 억제 판단 후 반환하고 주입·스타일 변경은 같은 스레드의 FIFO 큐에서 수행한다. native 프로브 3개 통과, 제품 실사용은 검증 중이다. HWND 고정·앱별 분기·권한 상승은 없다.
- `src/Magnifier.Core.Tests`: Core 좌표 환산·프레임 불변 조건·입력 gate와 release 전이를 단위 테스트한다.

## 의존성 규칙

- App은 Core 계약을 소비하며, composition root에서만 Infrastructure 구현을 만든다. Win32 P/Invoke를 직접 보유하지 않는다.
- Infrastructure는 Core를 참조할 수 있지만 App을 참조하지 않는다.
- 실제 입력은 runtime gate와 단일 release 경로 뒤에 둔다. 전달 목적지는 대상 창 핸들이 아니라 선택한 `ScreenRegion`의 물리 화면 좌표다.
- 원본 영역 변경과 렌즈 위치 변경을 분리한다. 설정에는 두 창 배치와 배율만 기록하며 입력 허용 상태를 복원하지 않는다. manifest는 PerMonitorV2/asInvoker/uiAccess=false다.

두 보조 창에 진입 창을 Owner로 설정하지 않는다. Main Hide가 확대 창까지 숨기지 않도록 Main이 반환·종료를 명시적으로 관리한다.

## 검증 전략

- Core: 좌표 변환, 범위, 포인터 세션 gate·Down·Move·Up·release 단위 테스트.
- Infrastructure: 화면 캡처 실패·입력 해제·UIPI 거부 표기 테스트와 진단 모드.
- App: WPF 명령·캡처·입력 상태 표기 테스트와 사용자의 실제 대상 앱 확인.

## 다이어그램

`docs/diagrams/capture-flow.md`는 두 창의 독립 좌표와 캡처 경계를, `docs/diagrams/input-session.md`는 현재 입력 중계 후보의 release·물리 해제 대기 전이를 설명한다. 두 다이어그램은 구현 설명이며 실사용 수락 증거가 아니다.
