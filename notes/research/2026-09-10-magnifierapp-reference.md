# 사용자 추가 MagnifierApp 레퍼런스

날짜: 2026-09-10 · 코드·문서 읽기 비교. 실행·빌드·수정하지 않았다.

원본: [MagnifierApp](<C:/Users/user/Downloads/새 폴더 (4)/tiny/새 폴더/rac hair/MagnifierApp/README.md>).
아래 줄 번호는 같은 폴더 MainWindow.xaml.cs 기준이다. 원본 문서의 과거 작업 경로·배포·위임 지시는 현재 Magnifier 작업으로 가져오지 않는다.

## 현재 앱보다 참고할 부분

- 배율 1~8×, 0.1 증감 버튼, 프리셋, 화면·영역·창 고정, Freeze·복제 화면이 있다.
- CompositionTarget.Rendering과 WriteableBitmap 재사용으로 캡처를 갱신한다(581·707). 현재 앱의 100ms 타이머와 비교할 후보이며 실제 fps 측정은 아니다.
- TryBeginForwardedPointer → TryContinueForwardedPointer → TryEndForwardedPointer(1639·1724·1764)로 연속 입력 생명주기를 표현한다.
- GetEffectiveForwardedViewportPoint와 UpdateSynchronizedCursor(990·1009)는 표시 위치와 실제 커서 이동 충돌을 별도 상태로 다룬다.
- app.manifest는 asInvoker / uiAccess=false / PerMonitorV2다. Windows Magnification 입력 변환을 쓰는 방식과 구분한다.

## 그대로 이식하면 목표와 충돌하는 부분

| 관찰 | 이번 판단 |
|---|---|
| ApplyImmersiveMode(453)가 제목·설정·상태를 숨기고 F1이 복구 토글 | 마우스 복귀 제어는 몰입 중에도 남겨야 함 |
| ExecutePassthroughInput(1582)에서 WindowFromPoint·자식 HWND·SetForegroundWindow 사용 | 순수 전역 좌표 전달과 다른 구조임을 명시 |
| 터미널·탐색기·바탕화면·IEasyDesk 등 분기(1256·1337~1382·1610 부근) | 범용 성공 근거로 간주하거나 분기 목록을 복제하지 않음 |
| 대부분 WM_MOUSE 메시지 전달, 일부 합성 pointer와 커서 동기화 | README의 드래그 지원 설명을 모든 앱 실시간 성공으로 해석하지 않음 |
| TrySendMouseMoveMessage·TrySendMouseButtonMessage(1459·1503)는 SendMessage 뒤 true | 대상에서 의미 있는 조작이 일어났다는 증거가 아님 |
| MainWindow.xaml.cs 약 2,400줄에 캡처·좌표·창 탐색·입력·설정 집중 | 경험을 참고하고 현행 App/Core/Infrastructure 경계를 유지 |

## 공식 API와 교차 확인한 쟁점

1. EnsureSyntheticMouseDevice(1068)는 CreateSyntheticPointerDevice에 PT_MOUSE를 넣는다. [공식 API](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-createsyntheticpointerdevice)는 PT_TOUCH 또는 PT_PEN만 허용한다고 명시한다. 따라서 이 분기를 검증된 합성 마우스 해법으로 채택할 수 없다. 실제 이 실행본에서 어느 fallback이 작동했는지는 측정 전이다.
2. 창 메시지 훅(296)은 전달 중 WM_NCHITTEST에 HTTRANSPARENT를 반환한다. [공식 WM_NCHITTEST](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-nchittest)의 HTTRANSPARENT 전달 설명은 같은 스레드 창에 한정된다. 다른 프로세스로 클릭이 통과한다고 가정할 수 없다.
3. 주입 모드의 좌표 환산(954)은 DPI·배율·원점 조합을 별도로 다룬다. 혼합 DPI의 정밀도는 테스트로 확인하며 코드 존재만으로 맞다고 판정하지 않는다.

## 계획 반영

레퍼런스의 확대→직접 조작 경험, 배율 버튼·프리셋·프레임 재사용을 우선 비교한다. 주력 요구를 A/B로 축소하지 않는다. 기본 화면에는 복귀·보기/조작·배율·다른 곳만 남기고 창 고정·복제·캡처 저장·프리셋 편집을 한꺼번에 배치하지 않는다.

다음 구현의 첫 실측은 레퍼런스와 현행 앱의 같은 목표 클릭·곡선 드래그·마우스 복귀 비교다. 사용자 승인된 UI 검증 단계에서 진행하며, 그 전에는 레퍼런스를 정상 동작 정본 또는 확정 이식 대상으로 보고하지 않는다.
