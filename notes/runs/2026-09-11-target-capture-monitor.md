# 반복 드래그 해제 로그와 원본 capture 감시 수정

- workspace `C:/ai/projects/magnifier`, branch `main`, 기준 HEAD `725cd05`.
- 사용자: 직전 수정 뒤에도 **“드래그 자꾸 풀려”**.
- 최종 판정: **USER_REPORTED_FAIL / BLOCKED_REPEAT_DRAG_RELEASE**. 아래 감시 범위 수정·44개 테스트 통과 뒤에도 재발했다. 후속 실제 결과를 문서 끝에 기록한다.

## 실제 중단 근거

`C:/Users/user/AppData/Local/Magnifier/diagnostics/relay-stop-28180.jsonl`에서 다음 3회 중단을 확인했다.

| KST | 사유 | 상태 |
|---|---|---|
| 2026-09-11 02:47:27 | 대상 포인터 capture 손실 · 조작 중지 | WasPressed=true, PhysicalLeftHeld=true, HookButtons=1, PendingLeftRelease=false |
| 02:47:32 | 동일 | 동일 |
| 02:47:37 | 동일 | 동일 |

engine MVID는 직전 실행본 `0dbeda53-3808-48bd-a240-aa790243cb90`과 일치했다. Up 대기 경합이 아닌, 실제 누름 중 capture 비교 분기가 release와 요청 취소를 실행한 것이다. 직전 큐 우선 수정은 이 사용자 문제를 해결하지 못했다.

기존 코드는 `GetGUIThreadInfo(0)`로 전면 스레드를 읽고, 처음 관찰한 비영 capture HWND와 다르기만 하면 중지했다. 다른 전면 스레드의 상태와 같은 원본 안의 HWND 인계를 구별하지 않았다. 기존 로그에는 비교한 HWND/thread가 없어 세 사건 각각이 어느 변화였는지까지 확정할 수 없다.

## 수정

- Down마다 원본 물리 좌표에서 입력 통과 적용 후 실제 HWND의 thread와 root를 확인한다. **입력은 계속 ScreenRegion 좌표로 전달**하며, 특정 앱/창으로 입력 목적지를 고정하지 않는다. 보관한 thread/root는 해당 누름의 capture 감시에만 쓴다.
- `PointerCaptureMonitor`는 현재 전면 스레드 대신 해당 원본 thread의 GUI 상태를 조회한다.
- 같은 원본 root의 부모·자식 capture 인계는 연속 드래그로 유지한다. 아직 원본의 capture를 관찰하지 않은 상태에서 0이나 무관한 root를 읽어도 capture 손실로 만들지 않는다.
- 관찰한 원본 capture가 0/다른 root로 사라지거나 원본 GUI thread 조회가 실패하면 기존 release/hard stop을 유지한다. Up 대기 우선 처리도 유지한다.
- 로그에 thread, 원본 root, 이전/현재 capture, 현재 capture root, 조회 성공 값을 남긴다. 재발 시 전면 변경·내부 인계·실제 해제·조회 실패를 구별할 수 있다.

`WindowsLivePointerRelay.cs`, `.Native.cs`, `.Diagnostics.cs`, 새 `PointerCaptureMonitor.cs`와 회귀 테스트를 변경했다. UI에 Win32 호출을 추가하지 않았고 UIAccess 설치본을 변경하지 않았다.

## 검증

- `dotnet test Magnifier.slnx --nologo`: Core 34 + Infrastructure 10 = **44개 통과**, 실패·건너뜀 0.
- 추가 6개: 원본 thread 조회 유지, 같은 root 인계 중 추가 Up 없음, 무캡처/무관 capture 구분, 실제 원본 capture 손실 시 1회 Up/요청 취소, 조회 중 pending Up 우선, thread 조회 실패·외부 root 전환·reset.
- `dotnet build Magnifier.slnx --nologo`: 경고 0, 오류 0.
- 프로브 build 경고/오류 0. `--describe`만 실행해 MVID `fb7a90a8-2e63-470c-b5e7-1fb2f92a1db7` 확인. 자동 UI/입력은 실행하지 않았다.

표준 실행본: `C:/ai/projects/magnifier/src/Magnifier.App/bin/Debug/net9.0-windows/Magnifier.App.exe`.
사용자 관리자 진입 파일: `C:/ai/projects/magnifier/Start-Magnifier-Admin.cmd`.

실행 중인 Magnifier/RelayProbe가 없는 상태에서 빌드했다. 실제 대상의 지속 드래그·마우스 복귀는 사용자 재검증 전이며, 테스트 통과를 실사용 수락으로 계산하지 않는다. 실제 target capture가 해제되는 앱의 OLE/다중 root·thread 인계 등은 아직 실측하지 않았다. 재발하면 새 로그의 Capture 세부 값을 먼저 읽고 추가 추정 패치를 피한다.

기존 변경과 외부 자료를 보존했다. 스테이징·커밋·푸시는 하지 않았다.

공식 근거: [GetGUIThreadInfo와 foreground 조회 한계](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getguithreadinfo), [GetWindowThreadProcessId](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowthreadprocessid), [GetAncestor GA_ROOT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getancestor).

## 수정 후 사용자 재실패와 인계

사용자 “아직도 풀려”. `relay-stop-32236.jsonl`의 2026-09-11 03:04:52·03:04:56 KST 두 사건 모두 최신 MVID `fb7a90a8-2e63-470c-b5e7-1fb2f92a1db7`이다. PhysicalLeftHeld=true, PendingLeftRelease=false 상태에서 WasPressed=true → IsPressed=false, InputRequested=false로 끝났다.

두 사건의 Capture 값은 동일하다: Thread=28092, TargetRoot=17045840, Previous=1249406, Current=3214510, CurrentRoot=3214510, ReadSucceeded=true. 즉 조회 실패나 capture=0이 아닌 **다른 root의 비영 capture 전환**이 중지 조건에 걸렸다. 해당 전환이 정상 OLE/보조 창 인계인지 실제 손실인지는 로그만으로 확정할 수 없다. 현 테스트도 다른 root를 손실로 가정하므로 테스트 통과가 그 가정을 입증하지 않는다.

이 수정은 사용자 문제를 해결하지 못했다. 현재 요청에 따라 새 패치를 멈추고 관련 구현·진단·문서를 한 커밋으로 보존한다. 실행 중인 PID 32236은 종료하지 않았고 no-build 테스트 44개를 재통과했다. 다음 조사와 전체 남은 범위는 [인계 문서](2026-09-11-drag-blocked-handoff.md)에 고정한다. 푸시 없음.
