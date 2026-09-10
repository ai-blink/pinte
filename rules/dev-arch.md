# 아키텍처

## 현재 구조

- `src/Magnifier.App`: Main은 composition root·진입/복귀·비중첩 캡처와 배치 저장을 소유한다. SelectionOverlayWindow는 상시 투명 원본 테두리·8개 크기 손잡이, SelectionPreviewWindow는 독립 렌즈·배율·고정 제어·가상 포인터와 선택형 A/B를 소유한다. 직접 조작에 WPF mouse capture를 사용하지 않는다.
- `src/Magnifier.Core`: ScreenRegion/LensViewport는 독립된 원본·렌즈 물리 좌표를 보관한다. WindowPairPlacement는 초기/명시적 재배치 시 전체 창의 물리 크기로 아래쪽 배치를 계산한다. LensInputState는 조작 요청을 일시 정지와 분리하고, PointerInputSession은 Down/Move/Up과 release 재시도를 관리한다. ILivePointerRelay의 Start/Pause/Stop과 IWindowEnvironment는 App이 사용하는 계약이다.
- `src/Magnifier.Infrastructure`: GDI 캡처, 태그 SendInput, 전용 hook 스레드의 논리 포인터·layered 입력 통과·freshness/capture 감시, 물리 창 배치를 구현한다. hook 반환 뒤 FIFO 큐에서 전달·스타일을 변경한다. 요청이 남아 있고 버튼 해제·최신 프레임을 확인하면 재개한다. native 프로브 6개 통과, 제품 전체 실사용은 검증 중이다. HWND 고정·앱별 분기·권한 상승은 없다. WindowsInputTransformDiagnostic은 별도 CLI 진단에서만 토큰·OS 항등 입력 변환을 검사하며 제품 중계 엔진으로 사용하지 않는다.
- `src/Magnifier.Core.Tests`: Core 좌표 환산·프레임 불변 조건·입력 gate와 release 전이를 단위 테스트한다.
- `src/Magnifier.Infrastructure.Tests`: RelayCommandPump의 FIFO·pending Up과 Core 입력 해제 전이를 OS 입력 없이 검사한다. 입력 스레드는 감시 timer 전에 큐를 처리하고, capture 조회 중 관찰한 Up도 완료 우선으로 둔다. 취소 사유 로그는 별도 비동기 기록이며 입력 정책을 바꾸지 않는다.
- PointerCaptureMonitor는 매 Down의 원본 thread/root로 capture 감시만 제한한다. 같은 root 인계는 유지하고, 관찰 뒤 0/다른 root 전환 또는 조회 실패는 해제한다. 전달 목적지는 계속 물리 좌표다. **알려진 결함:** 2026-09-11 사용자 로그에서 다른 root의 비영 capture 전환이 누름 중 중지를 유발했다. 정상 인계인지 실제 손실인지 미확정이며 현재 회귀 테스트의 손실 가정도 재검토해야 한다. 최신 인계는 `notes/runs/2026-09-11-drag-blocked-handoff.md`다.

## 의존성 규칙

- App은 Core 계약을 소비하며, composition root에서만 Infrastructure 구현을 만든다. Win32 P/Invoke를 직접 보유하지 않는다.
- Infrastructure는 Core를 참조할 수 있지만 App을 참조하지 않는다.
- 실제 입력은 runtime gate와 단일 release 경로 뒤에 둔다. 전달 목적지는 대상 창 핸들이 아니라 선택한 `ScreenRegion`의 물리 화면 좌표다.
- 원본 영역 변경과 렌즈 위치 변경을 분리한다. 설정에는 두 창 배치와 배율만 기록하며 입력 허용 상태를 복원하지 않는다. manifest는 PerMonitorV2/asInvoker/uiAccess=false다.

선택형 `InputTransformTrial=true`는 진단 전용 uiAccess manifest를 사용한다. 사용자 승인으로 전용 서명·신뢰·Program Files 설치를 완료했다. 일반 manifest는 asInvoker/uiAccess=false다. `scripts/uiaccess`는 실제 설치 EXE manifest를 검사하고 기본 호출은 계획 조회다.

WindowsInputTransform은 OS RECT 설정/소유 확인/해제를, WindowsMagnifierSurface는 native child 확대를 소유한다. 둘은 프로브 전용 후보이며 클릭 전달 실패로 제품 연결을 보류했다. 일반 Main은 WindowsLivePointerRelay를 구성한다. `scripts/Start-Magnifier.ps1`과 루트 CMD는 사용자 수동 실행만 담당하며 관리자 요청은 사용자의 Admin 진입 파일 실행 시에만 발생한다.

두 보조 창에 진입 창을 Owner로 설정하지 않는다. Main Hide가 확대 창까지 숨기지 않도록 Main이 반환·종료를 명시적으로 관리한다.

## 검증 전략

- Core: 좌표 변환, 범위, 포인터 세션 gate·Down·Move·Up·release 단위 테스트.
- Infrastructure: 화면 캡처 실패·입력 해제·UIPI 거부 표기 테스트와 진단 모드.
- App: WPF 명령·캡처·입력 상태 표기 테스트와 사용자의 실제 대상 앱 확인.

## 다이어그램

`docs/diagrams/capture-flow.md`는 두 창의 독립 좌표와 캡처 경계를, `docs/diagrams/input-session.md`는 현재 입력 중계 후보의 release·물리 해제 대기 전이를 설명한다. 두 다이어그램은 구현 설명이며 실사용 수락 증거가 아니다.
