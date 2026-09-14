# 훅 생존 감시·자동 재설치 (입력 전송 조용한 중단)

- 사용자 보고: "입력 전송 잘되다가 화면은 정상적으로 렌즈에 나오는데 클릭 해도 입력 전송이 안됨 클릭해도 가상포인터가 안나옴".
- 작업공간: `C:/ai/projects/magnifier`, main. 사용자 실제 실행본은 `C:\app\Magnifier.App.exe`(`artifacts/release/Pinte-v0.1.0-win-x64.zip`과 동일 바이너리, 19:19 시작 뒤 4시간 실행 중 발생).

## 원인 근거

- `%LOCALAPPDATA%\Magnifier\diagnostics\relay-stop-50688.jsonl`: 22:55 "원래 화면 · 입력 해제" 기록 시점의 relay 상태는 FrameIsFresh=true, HookButtons=0, PhysicalLeftHeld=false, IsSuspended/IsDraining/IsIntercepting=false, 조작 요청은 유지. 중계에 필요한 내부 조건이 전부 충족돼 있었는데 클릭이 relay에 오지 않았다. "입력 실패"·"상태 불일치" 기록은 없다.
- 내부 상태가 정상인데 hook 콜백만 오지 않는 조합은 WH_MOUSE_LL 훅이 OS에서 제거된 경우다. Windows 문서(LowLevelMouseProc): 훅 프로시저가 `HKCU\Control Panel\Desktop\LowLevelHooksTimeout`(이 PC 500ms) 안에 돌아오지 않으면 Windows 7 이후에는 알림 없이 훅을 제거하며 앱이 알 방법이 없다.
- 훅 스레드가 500ms를 넘길 수 있는 경로: `SendInput`은 다른 앱의 LL 훅을 동기로 거친다(GazeScroll·DimScreen 같은 상주 도구), `SetWindowLongPtr`는 UI 스레드에 동기 메시지를 보낸다, 무거운 GPU/CPU 작업 중 스레드 기아.
- 훅이 죽으면 화면 캡처는 계속되고(프레임 정상), 클릭은 WPF 창으로 들어가 "버튼 해제와 최신 화면을 기다린 뒤 자동 재개합니다"만 표시되며 가상 포인터는 나오지 않는다. 보고 증상과 일치한다.
- 손 도구·이동·리사이즈의 `_suspended` 잔존 경로는 모두 해제 짝을 확인했다. 21:08 기록의 IsSuspended=true는 그 시점 손 도구/이동 중이었을 가능성으로 남긴다.
- 실제 훅 제거를 재현하지는 못했다(OS가 신호를 주지 않아 직접 관측 불가). 위는 로그·코드·문서로 좁힌 추정이며 사용자 실측이 판정한다.

## 변경

- `HookLivenessMonitor.cs`(신규, Infrastructure): 50ms 타이머마다 OS 커서 좌표·버튼(`GetCursorPos`/`GetAsyncKeyState`)이 바뀌었는데 훅 콜백이 한 번도 없었던 표본이 3회 연속이면 재설치를 요청한다. 정지 상태는 근거로 세지 않고, 재설치 뒤 1초 쿨다운. 순수 클래스.
- `WindowsLivePointerRelay.cs`: 훅 콜백 첫 줄에서 활동을 기록. `CheckSession` 첫 단계 `CheckHookLiveness`가 같은 스레드에서 `UnhookWindowsHookEx`→`SetWindowsHookEx` 재설치, 버튼 상태를 OS 값으로 재동기화(`_hookButtons`/`_leftHeld`/`_otherButtonsHeld`/`ObservePhysicalButton`, 버튼 없으면 draining 해제). 진행 중 중계·누름이 있으면 `StopInternal(resume)`로 release 후 자동 재개 대기, 아니면 "입력 훅 재설치 · 조작 자동 재개" 표시. 설치 실패는 `failed` 중지. relay 스레드 `Priority = Highest`.
- `WindowsLivePointerRelay.Diagnostics.cs`: 기록을 `Event`("stop"/"hook-reinstall") 필드가 있는 레코드로 통일. 재설치 기록에 HookInstalled·Win32Error·Strikes·MissedRelease·OsButtons 추가. 같은 `relay-stop-<pid>.jsonl`에 남긴다.
- `HookLivenessMonitorTests.cs`: 6사례(3회 연속 판정, 활동 시 초기화, 정지 무시, 버튼 변화, 쿨다운, 자체 주입 이동).

## 검증

- `dotnet build Magnifier.slnx --nologo`: 경고 0·오류 0.
- `dotnet test Magnifier.slnx --nologo`: Core 60·Infrastructure 18·App 48, 총 126 통과.
- publish: `dotnet publish src/Magnifier.App/Magnifier.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/release/Pinte-hook-watchdog-win-x64` → `Magnifier.App.exe` 135,987,236 bytes(이전 135,982,628). 사용자 승인으로 `C:\app\Magnifier.App.exe`를 교체했다. 이전 실행본은 `artifacts/release/Pinte-v0.1.0-win-x64.zip`에 그대로 있다.
- 실제 훅 제거 재현·복구: **NEEDS_USER_UI_CHECK**. 판정 근거는 (1) 오래 실행 뒤에도 클릭이 계속 전달되는가 (2) `relay-stop-<pid>.jsonl`에 `"Event":"hook-reinstall"` 기록이 남는가(남았다면 원인 확정, 안 남았는데 증상이 재발하면 이 가설이 틀린 것) (3) 렌즈 상태에 "입력 훅 재설치" 문구가 보이는가.

사용자 확인: 교체한 `C:\app\Magnifier.App.exe`를 평소처럼 실행하고, 이전에 증상이 났던 만큼 사용한 뒤 클릭·드래그가 계속 되는지와 위 로그를 확인한다.
