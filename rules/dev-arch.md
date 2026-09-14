# 아키텍처

## 현재 구조

- 이번 기능: MainWindow.SourceEditing은 원본 편집/완료와 표시 수명, MainWindow.Settings는 모달과 편집 보류 합산을 맡는다. SourceIndicatorWindow는 편집창과 별개인 표시창이며 SourceIndicatorLifetime은 5초 기한/취소를 맡는다. SelectionPreviewWindow.Chrome/Resize는 일반·컴팩트와 실제 창 리사이즈를 맡는다. 프레임은 source/session/geometry 경계 이전에 시작된 결과를 폐기한다.
- IWindowEnvironment.SetPassiveOverlay는 표시창의 영구 클릭 통과·비활성화를 Infrastructure에 위임한다. 표시창 HWND는 relay Configure 목록에서 제외해 스타일 복원 충돌을 피한다. 편집창 ContentAperture는 접거나 좌표 의미를 바꾸지 않는다.
- Magnifier.App.Tests는 fake 계약·STA에서 설정 호환, 표시 수명과 입력 보류 회귀를 확인한다. 이번 작업에서는 테스트를 추가·컴파일하고 실행은 사용자에게 맡긴다. 실제 좌표·클릭 통과·대상 반응은 사용자 확인 전 통과로 기록하지 않는다.

- `src/Magnifier.App`: Main은 composition root·영역 지정/확대/복귀·비중첩 캡처와 배치 저장을 소유한다. 먼저 SelectionOverlayWindow의 원본 테두리·8개 손잡이로 영역을 정하고 `이 영역 확대`로 렌즈·입력을 연다. 선택 취소는 대기 중인 확대 작업도 무효화한다. SelectionPreviewWindow는 독립 렌즈·배율·고정 제어·가상 포인터와 선택형 A/B를 소유한다. 직접 조작에 WPF mouse capture를 사용하지 않는다.
- `src/Magnifier.Core`: ScreenRegion/LensViewport는 독립된 원본·렌즈 물리 좌표를 보관한다. 기존 WindowPairPlacement 계산은 보존하지만 현재 진입은 테두리만 배치하고 확대 확정 시 원본을 옮기지 않는다. LensInputState는 조작 요청을 일시 정지와 분리하고, PointerInputSession은 Down/Move/Up과 release 재시도를 관리한다. ILivePointerRelay의 Start/Pause/Stop과 IWindowEnvironment는 App이 사용하는 계약이다.
- `src/Magnifier.Infrastructure`: GDI 캡처, 태그 SendInput, 전용 hook 스레드의 논리 포인터·layered 입력 통과·freshness/capture 감시, 물리 창 배치를 구현한다. hook 반환 뒤 FIFO 큐에서 전달·스타일을 변경한다. 요청이 남아 있고 버튼 해제·최신 프레임을 확인하면 재개한다. native 프로브 6개 통과, 제품 전체 실사용은 검증 중이다. HWND 고정·앱별 분기·권한 상승은 없다. WindowsInputTransformDiagnostic은 별도 CLI 진단에서만 토큰·OS 항등 입력 변환을 검사하며 제품 중계 엔진으로 사용하지 않는다.
- `src/Magnifier.Core.Tests`: Core 좌표 환산·프레임 불변 조건·입력 gate와 release 전이를 단위 테스트한다.
- `src/Magnifier.Infrastructure.Tests`: RelayCommandPump의 FIFO·pending Up과 Core 입력 해제 전이를 OS 입력 없이 검사한다. 입력 스레드는 감시 timer 전에 큐를 처리하고, capture 조회 중 관찰한 Up도 완료 우선으로 둔다. 취소 사유 로그는 별도 비동기 기록이며 입력 정책을 바꾸지 않는다.
- PointerCaptureMonitor는 매 Down의 원본 thread/root를 감시에만 쓴다. 2026-09-12 수정은 같은 thread의 다른 root 인계를 유지하며, capture=0만으로 해제하지 않는다. 원본이 닫히거나 조회가 실패하면 중지한다. capture 관찰 뒤 외부 thread가 이를 소유하거나, capture=0과 무관한 전면 창 전환이 함께 확인되면 해제한다. 전면 창은 보조 근거이며 capture는 원본 thread에서 조회한다. 전달 목적지는 물리 좌표다. 새 trace에 capture thread·원본 생존·외부 전면 전환을 남긴다. 수정 후 사용자 “아주 잘됨” 확인을 받았다. 앱별 전체 호환성까지 확인된 것은 아니다.
- HookLivenessMonitor는 훅 콜백 횟수와 OS 커서/버튼 표본만 비교하는 순수 판정기다(D-027/D-028). `Sample`은 `None/Probe/Reinstall`을 돌려주고, 재설치·프로브 주입·버튼 재동기화는 WindowsLivePointerRelay.CheckHookLiveness가 relay 스레드에서 수행하며 진행 중 입력 중에는 하지 않는다(드래그 보호). 진단 기록은 `Event` 필드로 stop과 hook-reinstall을 구분한다. App.xaml.cs는 제품 경로 단일 인스턴스 Mutex로 전역 훅 중복을 막는다(D-028).

[Windows DragDetect](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-dragdetect)는 버튼을 누른 채 감지 범위 밖으로 움직여도 감지를 마친다. [WM_CAPTURECHANGED](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-capturechanged)는 앱 자체 해제에도 발생한다. 이를 근거로 capture 값만으로 물리 드래그 취소를 추론하던 조건을 좁혔다. 과거 사용자의 OLE/보조 창 인계 여부까지 입증된 것은 아니다.

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
