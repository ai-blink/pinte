# 훅 워치독 폭주 회귀 수정 (드래그·입력 전송 실패)

- 사용자 보고: 2026-09-14 훅 감시(D-027)를 넣은 실행본에서 "드래그 안됨 · 입력 전송 실패".
- 작업공간: `C:/ai/projects/magnifier`, main. 사용자 실행본 `C:\app\Magnifier.App.exe`.

## 원인 근거 (로그로 확정)

`%LOCALAPPDATA%\Magnifier\diagnostics`의 새 프로세스 두 개 로그:

| 프로세스 | hook-reinstall | 누름 중 재설치 | 시간대 |
|---|---|---|---|
| 53164 | 1161 | 0 | 14:20~14:58 |
| 23884 | 843 | **9** | 14:29~14:55 |

- **Magnifier.App이 두 개 동시 실행**(53164 내가 띄운 것, 23884 사용자 추가 실행 — 둘 다 같은 새 바이너리 SHA 일치). 각자 전역 WH_MOUSE_LL 훅을 걸고, 훅 체인이 서로 느려지며 `LowLevelHooksTimeout`(500ms)을 넘겨 훅이 계속 제거·재설치되는 자기지속 폭주가 됐다.
- 23884에서 **누르는 중 재설치가 9번** — 재설치의 `StopInternal(resume)`가 매번 드래그를 release로 끊었다. 이것이 "드래그 안됨". 재설치 순간(약 150ms) 창에 클릭이 대상에 안 가는 것이 "입력 전송 실패".
- 이 PC 커서는 프로브 결과 **전부 주입(injected) 이동**(보조 입력 장치, 3초에 30이벤트 전부 injected 플래그). D-027 워치독은 "커서는 움직이는데 훅은 조용함"을 훅 죽음으로만 해석해, 상시 주입 환경에서 오탐이 잦았다. 즉 D-027 수정이 회귀를 만들었다.
- 자기검증 프로브로 확인: **태그 절대-현위치 SendInput은 자기 훅에 되돌아온다**(taggedDelta=1, 제로델타는 Windows가 버려 안 됨). 재설치 전에 이 프로브로 살아있음을 확인할 수 있다.

## 변경

- `App.xaml.cs`: **단일 인스턴스 잠금**. `Local\Pinte.Magnifier.SingleInstance` Mutex를 제품 경로에서 획득하고, 두 번째 실행은 안내 후 종료(코드 5). 진단(`--diagnose-input-transform`)·TRIAL 경로는 잠금 이전에 반환해 영향 없음. OnExit에서 해제. 두 인스턴스가 훅 체인을 서로 느리게 만드는 폭주 트리거를 제거한다.
- `HookLivenessMonitor.cs`: **자기검증 재설치**. `Sample`이 bool 대신 `None/Probe/Reinstall`을 돌려준다. 3연속 무응답이면 곧장 재설치하지 않고 `Probe`(태그 자기주입 요청)를 낸다. 살아있는 훅은 프로브(또는 임의 입력)를 활동으로 되울려 의심을 즉시 해제한다. grace 틱 안에 되울림이 없을 때만 `Reinstall`. 쿨다운 유지.
- `WindowsLivePointerRelay.CheckHookLiveness`: `Probe`면 현위치로 태그 이동을 1회 주입(보이지 않고 아무것도 누르지 않음), `Reinstall`이면 재설치·버튼 재동기화. **진행 중 입력 보호** — `_relaying/_intercepting/_draining/_state.IsPressed/_leftHeld/OtherHeld` 중 하나라도면 재설치·프로브를 하지 않는다(드래그를 끊지 않는다). 세션당 자동 재설치 `MaxAutoReinstalls=12` 초과 시 자동 재설치를 끄고 기록만 남긴다(지속 결함은 폭주 대신 표면화).
- `HookLivenessMonitorTests.cs`: 8사례 — 프로브 후 무응답→재설치, 프로브 되울림→해제, 활동 초기화, 정지 무시, 버튼 변화, 쿨다운, 자체 주입, 상시 주입 200틱 무폭주.

## 검증

- `dotnet build Magnifier.slnx --nologo`: 경고 0·오류 0.
- `dotnet test Magnifier.slnx --nologo`: Core 60·Infrastructure 20·App 48, 총 128 통과.
- publish 단일 파일 135,991,844 bytes, `C:\app` 복사본 SHA-256 일치.
- **단일 인스턴스 실증**: 실행본을 두 번 띄우니 두 번째가 종료 코드 5로 거부되고 1개만 남았다.
- **폭주 해소 관찰**: 새 프로세스(47140)는 진입 창에서 6초 넘게 stop·hook-reinstall 이벤트가 0(이전 빌드는 초당 1회 재설치). 진입 창에서도 워치독은 매 50ms 샘플하므로, 0은 훅이 살아있고 오탐이 없음을 뜻한다.
- 실제 렌즈 조작 드래그·클릭 전달: **NEEDS_USER_UI_CHECK**. 판정 근거는 (1) 드래그가 1초 넘게 유지되는가 (2) `relay-stop-<pid>.jsonl`에 hook-reinstall 폭주가 없는가 (3) 클릭이 대상에 전달되는가.

사용자 확인: 교체한 `C:\app\Magnifier.App.exe`를 한 번만 실행하고(두 번째 실행은 거부됨), 렌즈에서 드래그가 끊기지 않고 클릭이 전달되는지 확인한다.
