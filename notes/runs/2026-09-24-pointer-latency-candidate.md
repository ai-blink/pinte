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

## 정본 교체 (2026-09-24 01:58, 사용자 승인)

- 교체 전: 실행 중 `Magnifier.App` 프로세스 없음, 바로 가기 대상 `C:\app\Magnifier.App.exe`(작업 폴더 `C:\app`, 인자 없음) 확인.
- 교체 전 정본 SHA-256 `C7DB1CB56BA95639903B0A23341E9511E04555A4AA4E71416A55E0F9A53E7AD5` → 백업 `C:\app\Magnifier.App.pre-low-latency-20260924-0158.exe`(해시 동일 확인).
- 교체 후 정본 SHA-256 `40B44D0D727115A09952543DE60E62DD62FAD14F6F79A268EF0B56F1B5C479B9`(후보와 일치).
- 복원: 백업 파일을 `C:\app\Magnifier.App.exe`로 복사.
- 실제 게임 반복 시험(단일 클릭10·더블클릭10·짧은 드래그10·긴 드래그5)은 `NEEDS_USER_UI_CHECK` — 결과 미수신.
## 입력 타이밍 튜닝 패널 교체 (2026-09-24 02:30, 사용자 승인)

- 사용자 확인: 저지연 후보(`40B44D0D…`)는 게임 전달은 되지만 체감 지연이 크다 → 재빌드 없이 값을 바꾸는 시험 패널을 추가했다.
- 구현: Core `PointerTimingSettings`(범위 도착0~300·누름0~200·체류0~300·간격0~300ms, 5ms 간격)·`ILivePointerRelay.ApplyTimingAsync`, relay는 시퀀스 idle 경계에서만 전체 스냅샷 교체(진행 중 제스처 불변)·`timing-applied` 경로 로그·`target-prepared`에 rev/값 기록, App `PointerTimingWindow`(적용/직전값/기본값/프리셋/A·B)·`%LOCALAPPDATA%\Magnifier\pointer-timing.json`(schemaVersion·revision, 임시파일 교체 저장, 손상 시 기준값).
- 검증: build 경고0/오류0, Core65·Infrastructure80·App61 PASS, Release single-file publish.
- 교체: `40B44D0D…` → 백업 `C:\app\Magnifier.App.pre-timing-panel-20260924-0230.exe`, 새 정본 SHA-256 `E36833C480A8F8B38AABDE514BF11D20723EAC92EE2835337A00C2618AA42581`.
- 주의: 작업 트리의 미커밋 사용자 변경(`SelectionPreviewWindow.Resize.cs`)이 이 publish에 포함됐다.
- 패널 표시·적용·게임 체감별 결과는 `NEEDS_USER_UI_CHECK`.
## 사용자 결과와 고급 설정 전환 (2026-09-24 02:49, 사용자 승인)

- 사용자 확인: 5/1/5/0ms(도착/누름/체류/간격)에서 게임 전달·체감 모두 됨. 프로필 rev4 저장(직전값 15/5/10/0). 한 게임·한 환경 결과이며 자동 계측 아님.
- 사용자 요구: 시스템·사양·게임·앱마다 다르므로 고급 설정으로 조절 가능하게. → 진입 창 시험 버튼 제거, 설정 창 「⚙ 고급」 페이지에 현재값과 「입력 타이밍 조절 열기」 추가(설정 모달을 닫고 비모달 조절 창을 연다). 프리셋 기본/빠름/저지연(5/1/5/0)/즉시, 10ms 미만은 1ms 단위 증감.
- 제품 기본값은 100/35/60/0 유지(단일 환경 결과로 전역 기본값을 바꾸지 않음).
- 검증: build 경고0/오류0, Core65·Infrastructure81·App61 PASS, publish 5.9s. 이전 세션의 수십 분 지연은 재현되지 않아 원인 미상.
- 교체: `E36833C4…` → 백업 `C:\app\Magnifier.App.pre-advanced-timing-20260924-0249.exe`, 새 정본 `E459018B7AE9666CAF2647AF77ABBF044D117190761DFD8414987A033769ED5D`. 고급 페이지 표시·조절 창 동작은 `NEEDS_USER_UI_CHECK`.
- 사용자 최종 확인(2026-09-24): 고급 페이지·조절 창·5/1/5/0 유지 포함 "다 잘됨". USER_CONFIRMED(한 게임·한 PC).
