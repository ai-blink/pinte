# R0 입력 중복 수정과 표준 앱 재검증

날짜: 2026-09-10 · delivery: `C:/ai/projects/magnifier` · branch: `main`

## 현재 판정

**전용 native 프로브 3개 통과. 표준 앱의 두 창 표시는 사용자 확인. 전체 실사용 수락은 NEEDS_USER_UI_CHECK.**

기준 구현은 `6b5c0df`다. 시작 당시 tracked 변경과 실행 중인 Magnifier.App/RelayProbe는 없었다. 사용자가 이전 검증 때 직접 마우스를 조작했다고 답해 UI 검증을 재개했다. `notes/transfers/`는 변경 대상에서 제외했다.

## 재현과 수정

1. 외부 충돌 없는 첫 프로브에서 목표 Down/Up이 각각 2회 수신됐다. 원본 프로브 태그 `0x50524F42`와 중계 태그 `0x4D41474E`가 모두 대상에 도착했다. 곡선·왕복 좌표 자체는 전달됐다.
2. 프로브 송신을 Task.Run으로 옮기는 가설은 동일한 2/2 중복으로 실패했다. 해당 변경은 되돌렸다.
3. Infrastructure의 hook callback에서 직접 수행하던 SendInput·창 스타일 변경을 같은 전용 스레드의 FIFO 명령 큐로 옮겼다. callback은 원본 이벤트 억제 여부와 버튼/좌표 관측만 처리하고 반환한다. 물리 이동 기준 커서는 callback에서 채집한다. 중지·해제도 같은 큐에서 순서를 유지한다.
4. 수정 후 첫 실행은 외부 합성 이동(flags=1, tag=`0xABCDEF01`)을 감지해 대상 클릭 전에 중단했다. 자동 재시도하지 않고 사용자에게 확인했고, 직접 조작했다는 답변 뒤 재개했다.
5. 다음 실행에서 세 시나리오 모두 PASS. 엔진 assembly MVID: `fe2ecbd3-dd54-403b-b094-2cbdf8c67800`.

Microsoft는 hook이 원본 전달을 막을 때 0이 아닌 값을 반환하며 빠르게 복귀해야 한다고 설명한다. [LowLevelMouseProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc). 동기 주입 중 원본과 중계 버튼이 중복되는 경로를 분리한 효과는 위 전후 실행으로 확인했다. OS 내부 timeout 발생 자체를 측정하거나 단정하지 않는다.

## 실제 목표 수신 범위

| 시나리오 | 수신 결과 |
|---|---|
| 분리된 렌즈·대상 창 곡선/왕복 | 대상 Down 1, Up 1, 중간 6개 좌표 순서 일치, 렌즈 Down 0 |
| 겹친 layered 렌즈·대상 창 곡선/왕복 | 대상 Down 1, Up 1, 같은 좌표 순서 일치, 렌즈 Down 0 |
| 경계 중지와 재무장 | 마지막 유효 `(517,353)`에서 Up 1회, 물리 Up 전 시작 거부, 남은 Up 추가 전달 없음, 해제 뒤 명시 재시작 가능 |

곡선·왕복 기준 원본 물리 좌표: `(288,230) → (350,247) → (413,318) → (475,389) → (413,318) → (350,247) → (288,230)`.

Down/Up의 수신 태그는 모두 중계 태그였다. 프로브는 실제 Windows 메시지를 받는 WPF 테스트 창이며 입력은 태그가 있는 합성 절대 이동이다. 실제 상대 마우스·보조 장치·브라우저/Blender·제품 캡처 재귀 검증을 대체하지 않는다.

## 표준 실행본

`C:/ai/projects/magnifier/src/Magnifier.App/bin/Debug/net9.0-windows/Magnifier.App.exe`

표준 앱을 실행해 ‘확대 시작’을 클릭했다. 소유 창까지 숨기는 WPF Owner/Hide 관계를 제거하고 Main에서 두 창의 반환·종료 수명을 명시적으로 관리하도록 수정했다. 최초 자동 실행한 보기 모드 프로세스만 종료하고 표준 경로로 재빌드했다. 사용자 실행본을 임의 종료하거나 임시 제품 출력으로 대체하지 않았다.

자동화 도구는 확대 진입 후 두 보조 창을 목록에 반환하지 않아 화면을 판정할 수 없었다. 사용자가 수정 실행본에서 **‘둘 다 보여’ / ‘두 창 모두 보임’**을 확인했다. 두 창은 캡처 제외를 사용하며, 도구의 목록 누락 원인은 별도로 확정하지 않았다.

`scripts/probes/pointer-target.html`은 작은 마커·이동 궤적과 Down/Move/Up/Click·capture 손실을 표시하는 실제 브라우저 검증 표면이다. 자체 입력 주입이나 돋보기 모의 구현은 없다. localhost:18764에서 Chrome에 열고 사용자의 실제 렌즈 조작 결과를 기다린다. 아직 마커 이동·복귀를 통과 처리하지 않았다.

## 검증과 남은 일

- `dotnet build Magnifier.slnx --nologo`: 경고 0, 오류 0.
- `dotnet build scripts/probes/RelayProbe.csproj --nologo`: 경고 0, 오류 0.
- `dotnet test Magnifier.slnx --nologo`: Core 23개 통과, 실패/건너뜀 0.
- `git diff --check`: 통과. 소스 파일은 500줄 미만.
- `review-router` → `ollama-router`, 설치 태그 `kimi-k2.7-code:cloud`로 큐·종료 경로 보조 검토. 동일 스레드 가시성 경쟁, 큐에 넣지 않는다는 설명, 이미 존재하는 IsPressed 검사 누락 등의 모델 지적은 코드와 모순돼 채택하지 않았다. 모델 출력은 통과 근거가 아니다.
- 다음: 표준 앱의 마커 드래그·마우스 복귀 → 원본 이동/크기와 렌즈 독립 이동 → 실제 장치·겹침·DPI/모니터·닫기/실패 해제 → Blender/일반 Windows 앱. R3는 핵심 수락 뒤 진행한다.
