# 2026-09-24 저지연 입력 후보

## 목표

클릭·더블클릭·드래그 전달 경로를 유지하면서 사용자가 체감하는 고정 대기와 50ms tick 양자화를 줄인다.

## 변경

- `PointerTiming` 기본값: `ArrivalMs=100`, `MinimumHoldMs=35`, `PostReleaseMs=60`, `BetweenGesturesMs=0`.
- `TimedPointerSequence`에 `NextWakeAt`과 제한된 같은-message 전이를 추가했다. 0ms 또는 이미 지난 deadline은 불필요한 다음 tick을 기다리지 않는다.
- Move는 최대 8개씩 처리하되 후속 native message timer를 예약해 입력 경로를 독점하지 않는다. 좌표를 버리거나 마지막 좌표로 합치지 않는다.
- 기존 50ms health timer의 hook liveness/capture 감시 cadence는 유지한다.
- 명령 큐 drain 뒤 0ms 시퀀스의 relay finalize를 한 번 재확인해 pending release 때문에 health tick까지 남는 경로를 막았다.

## 검증

- `dotnet build Magnifier.slnx --nologo` — 경고 0, 오류 0.
- `dotnet test src/Magnifier.Core.Tests/Magnifier.Core.Tests.csproj --no-build --nologo` — 65 PASS.
- `dotnet test src/Magnifier.Infrastructure.Tests/Magnifier.Infrastructure.Tests.csproj --nologo --no-restore` — 76 PASS.
- Release self-contained single-file publish 성공: `src/Magnifier.App/bin/Release/net9.0-windows/win-x64/publish/Magnifier.App.exe`.
- publish 후보 SHA-256: `40B44D0D727115A09952543DE60E62DD62FAD14F6F79A268EF0B56F1B5C479B9`.

## 상태와 한계

- 현재 정본 `C:\app\Magnifier.App.exe`는 기존 `C7DB1CB5…`이며 새 후보로 교체하지 않았다.
- 실제 Windows native timer coalescing·worker 교체·게임 수신은 정적 검토와 fake-clock 테스트 범위 밖이다.
- 클릭·더블클릭·드래그의 실제 전달 유지, 게임 반응, 더블클릭 인식은 `NEEDS_USER_UI_CHECK`다.
- 기본값을 더 낮추거나 배포하려면 같은 대상에서 클릭·더블클릭·드래그를 반복하고, 성공/실패/미확인을 구분해 기록한다.

## 다음 단계

사용자 확인으로 후보가 통과하면 실행 중 앱을 종료한 뒤 정본 교체·해시 확인·실제 게임 반복 시험을 진행한다. 실패하면 기존 정본을 유지하고 파라미터별 A/B 결과와 대상 창 상태를 먼저 기록한다.
