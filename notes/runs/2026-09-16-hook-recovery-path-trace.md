# 2026-09-16 — 훅 복구 한계와 relay 경로 진단

## 범위

`C:/ai/projects/magnifier`의 `WindowsLivePointerRelay`가 key-demo-osk를 함께 실행한 상태에서 전체 실제 입력 전달을 멈춘다는 사용자 보고를 조사했다. 이 기록은 원인을 확정하지 않으며, 다음 실제 재현을 판별 가능한 상태로 만드는 변경과 배포 근거다.

## 관찰

- 기존 실행본의 `hook-reinstall` 기록은 프로세스 수명 동안 12회 누적된 뒤 훅 생존 확인 자체를 영구 중단할 수 있었다. 그 뒤 별도 훅 소실이 발생하면 relay는 조용히 복구를 포기할 수 있다.
- 훅이 드래그 중 제거되면 실제 물리 Up callback이 오지 않아 `_state.IsPressed`와 `_leftHeld`가 고착될 수 있었다.
- key-demo-osk의 `TargetWindowTracker`는 `WH_MOUSE_LL`을 설치하지만 callback 끝에서 `CallNextHookEx`를 반환한다. 짧은 클릭 subscriber도 첫 `await Task.Delay(200)`에서 즉시 반환한다. 따라서 이 관찰만으로 OSK가 Pinte 훅을 직접 차단했다고 결론 내리지 않는다.
- 새 배포 직전 Pinte의 기존 진단은 stop·hook-loss·재설치만 기록했다. 입력 세션 armed, callback, command queue, 대상 Down 사이의 중단 위치는 알 수 없었다.

## 변경

- `HookRecoveryLimiter`는 마지막 복구 시도 뒤 60초 이상 공백이 있을 때만 재설치 예산을 초기화한다. 수명 누적 12회로 영구 비활성화하지 않는다.
- `HookLossRecoveryState`는 확정 훅 소실 중 relay가 누름을 소유하면 물리 버튼 해제를 기다리고, release 뒤 `StopInternal`을 통해 합성 Up 한 번과 stop을 보장한다.
- 복구 예산 소진·재설치 실패는 상태를 조용히 유지하지 않고 stop/release와 구조화 `hook-recovery-exhausted` 진단으로 끝낸다. 다음 입력 세션은 복구 상태를 새로 준비한다.
- `relay-path` 진단은 세션 armed 1회와 렌즈 안 왼쪽 누름당 최대 세 경계만 기록한다: hook enqueue/post 결과, command dequeue, 대상 Down 시작 또는 거절. 정상 move는 기록하지 않는다.

## 검증과 배포

- `dotnet build Magnifier.slnx --nologo` — 경고 0, 오류 0.
- `dotnet test Magnifier.slnx --nologo` — Core 60, Infrastructure 26, App 48, 총 134 통과.
- publish: `dotnet publish src/Magnifier.App/Magnifier.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/release/Pinte-relay-path-trace-win-x64 --nologo`.
- 사용자 승인으로 이전 Pinte를 종료하고 `C:\app\Magnifier.App.exe`를 SHA-256 `12DAF61C2BD09F03F08EB9FEA123EDD1EBEEB6676DFB0544494AD9553CF92B01` publish와 일치하게 교체·재실행했다. 이전 실행본은 `C:\app\Magnifier.App.pre-path-trace-20260916-210652.exe`로 보존했다.

## 남은 수락

새 Pinte를 실행한 상태로 key-demo-osk를 켜고 일반 대상에서 렌즈 클릭·드래그를 재현한다. 실패하면 Pinte를 닫지 않은 채 현재 PID의 `%LocalAppData%\Magnifier\diagnostics\relay-stop-<pid>.jsonl`에서 `relay-path`를 읽는다. callback 기록 부재, post 실패, dequeue 부재, target-press-begun 이후 실패를 구분한 뒤에만 다음 동작 수정을 결정한다. 이 단계는 `NEEDS_USER_UI_CHECK`다.
