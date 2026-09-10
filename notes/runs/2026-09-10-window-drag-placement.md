# 창 이동·아래쪽 배치 수정

- 날짜: 2026-09-10
- 작업공간: `C:/ai/projects/magnifier`, `main`, 기준 HEAD `725cd05`.
- 요청: 두 창이 너무 왼쪽 위에 있어 조작하기 어렵고, 제목부를 드래그해도 창이 이동하지 않음.
- 연결 기준: INTENT AC3 위치·배율, AC4 입력 상태. 이번 수정은 미커밋이며 푸시하지 않았다. `notes/transfers/`는 건드리지 않았다.

## 원인과 수정

물리 버튼 상태에는 WPF 창 손잡이 누름도 포함된다. 창 조절 시 Stop/Configure가 이 누름을 대상 입력의 해제 대기로 처리했고, UI가 Thumb를 비활성화해 창 이동 capture를 끊었다. 입력이 켜져 있기만 해도 창 손잡이를 잠그던 조건도 이동을 막았다.

- Core Stop에 중계가 누름을 소유하는지 전달한다. 아래 앱에 전달되지 않은 손잡이 누름은 해제 대기를 만들지 않는다. 실제 대상 누름은 소유권 인자가 false여도 Up·물리 해제 대기를 유지한다.
- Infrastructure는 relaying/intercepting/draining/실제 누름으로 소유권을 판단한다. 기존 hook 반환 뒤 FIFO 전달은 유지한다.
- 렌즈·원본 손잡이는 실제 대상 누름·해제 대기 중 잠근다. 누름 없이 입력만 활성인 경우 창 조절 시작 시 보기로 전환한다. 배율·A/B는 계속 입력 off에서 조절한다.
- 최초 배치는 제목부를 포함한 전체 물리 창 크기를 사용해 작업 영역 아래쪽에 계산한다. 진입 창에 `두 창 아래쪽에 배치`를 추가했다. 일반 재진입은 저장 배치를 사용한다. 통상 렌즈 이동은 원본 영역을 변경하지 않는다.

## 검증

사용자가 앱을 닫았다고 확인한 뒤 프로세스가 없는 것을 확인하고 원본 작업공간의 표준 출력으로 빌드했다. 사용자 실행본을 종료하거나 임시 출력으로 대체하지 않았다.

| 검증 | 결과 |
|---|---|
| `dotnet build Magnifier.slnx --nologo` | 경고 0, 오류 0 |
| `dotnet test Magnifier.slnx --nologo` | Core 28개 통과, 실패·건너뜀 0 |
| `dotnet build scripts/probes/RelayProbe.csproj --nologo` | 경고 0, 오류 0 |
| `scripts/probes/bin/Debug/net9.0-windows/RelayProbe.exe` | 4개 PASS, exit 0 |

추가 Core 회귀 테스트는 손잡이 누름 중 반복 Stop, 실제 대상 누름의 해제 대기 유지 2개와 전체 창 크기·음수 모니터 좌표·좁은 작업 영역 배치 3개다. 혼합 DPI의 실제 화면 검증을 대신하지 않는다.

native 프로브에서 외부 합성 Move를 감지한 첫 시도는 중단했다. 사용자 `다시 해` 요청 뒤 남은 프로브가 없는 것을 확인하고 재실행해 통과했다. 이 실행의 engine MVID는 `23c53fb8-66ad-4279-baaa-d658c9ea356b`다.

- 창 손잡이 누름 중 배치 갱신: 원본 렌즈 Down 1회, 입력 off/unpressed, 불필요한 WaitingForRelease 없음.
- 분리 창 곡선·왕복: 실제 native 대상 Down 1회·Up 1회, 연속 Move 수신, 렌즈 Down 0회.
- 겹친 창 곡선·왕복: 동일한 실제 대상 수신 조건 통과.
- 경계 중지: 마지막 유효 위치 `(517,353)`에서 Up 1회, 버튼 누른 채 재무장 차단, 늦은 원본 Up 억제.

이는 합성 절대 입력과 native 검증 창의 수신 결과다. 실제 WPF 제목부 이동, 브라우저 마커 반응, 물리 상대 마우스 동작을 자동으로 입증하지 않는다.

## 표준 앱과 사용자 확인

표준 실행본: `C:/ai/projects/magnifier/src/Magnifier.App/bin/Debug/net9.0-windows/Magnifier.App.exe`.

수정본을 실행하고 진입 창의 `두 창 아래쪽에 배치`를 눌렀다. 두 보조 창은 자동화 도구 목록에 반환되지 않아 직접 표시/이동을 사용자에게 확인했다. 답변은 **“두 창 모두 아래에 있고 이동됨”**이었다. 따라서 두 창의 아래쪽 표시·각 제목부 이동은 사용자 확인 PASS다.

다음은 여전히 `NEEDS_USER_UI_CHECK`다.

- 렌즈만 이동할 때 같은 확대 지점의 원본 좌표 유지, 원본 테두리 크기 변경과 캡처 반영.
- 실제 마우스로 확대된 작은 마커 클릭·곡선/왕복 드래그, 실시간 대상 변화.
- 중지·원래 화면·닫기·오류 뒤 해제, 복귀 입력의 추가 클릭 방지, 재진입 기본 off.
- 브라우저·Blender·Windows 앱, 겹침·배율·혼합 DPI·다중 모니터의 위치 일치.

브라우저 확인 표면은 `http://127.0.0.1:18764/pointer-target.html`이다. 실제 마커 입력 수신 결과는 아직 확인하지 않았다. 사용자에게 조작을 넘긴 뒤 자동 입력을 보내지 않았다. M5 전체 완료는 선언하지 않는다.

## 변경 범위

- App: `MainWindow.xaml`, `MainWindow.xaml.cs`, `SelectionPreviewWindow.xaml.cs`.
- Core/Infrastructure: `LensInputState.cs`, `WindowsLivePointerRelay.cs`, 새 `WindowPairPlacement.cs`.
- 회귀 검증: `LensInputStateTests.cs`, 새 `WindowPairPlacementTests.cs`, `scripts/probes/RelayProbe.cs`.
- 문서: 이 run, INTENT, dev-arch/context/progress/roadmap/ux, 입력 세션 상태도.
