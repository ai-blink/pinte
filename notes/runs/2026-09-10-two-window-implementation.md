# 두 창 돋보기 구현과 검증

날짜: 2026-09-10 · delivery: `C:/ai/projects/magnifier` · branch: `main`

## 판정

**구현 후보와 표준 빌드 전달. M5/R0 실사용 수락은 BLOCKED / NEEDS_USER_UI_CHECK.**
계획·목업과 별개로 App/Core/Infrastructure를 수정했다. 제어 창에서 실제 목표 창으로 전달된 native Down은 관측했지만, 프로브가 외부 입력을 감지해 연속 드래그 검증을 중단했다. 실제 사용자 장치와 브라우저·Blender의 성공을 주장하지 않는다.

## 구현된 경로

- 확대 시작 → 상시 원본 테두리와 독립 렌즈 표시. 시작과 재진입은 보기 모드다.
- 원본 테두리: 투명 내부, 제목 이동, 8개 크기 손잡이. 내부 경계를 PointToScreen 물리 픽셀로 캡처한다. 렌즈 위치와 별도로 보관한다.
- 렌즈: 고정 제목부·조작 시작/중지·원래 화면·배율 버튼. 원본 전체를 표시하고 공간이 부족하면 실제 맞춤 배율을 표시한다. 원본 영역 조절은 렌즈 위치를 변경하지 않는다.
- 직접 입력 후보: dedicated thread의 WH_MOUSE_LL 관측 → 논리 렌즈 포인터 → 원본 물리 좌표 SendInput. WPF mouse capture를 직접 조작에 사용하지 않아 대상 앱이 capture를 소유할 수 있다.
- 중계 중 두 layered 창을 WS_EX_TRANSPARENT로 전환한다. 창을 숨기지 않고 원본 목표를 누를 수 있도록 하는 경로다. 모든 자기 출력은 고유 ExtraInfo 태그로 재중계를 피한다. 타 장치의 injected flag를 전역 차단하지 않는다.
- 물리 마우스는 hook 좌표와 현재 OS 커서 차이를 논리 포인터에 누적한다. 외부 injected 이동은 절대 렌즈 포인터로 해석한다. **실제 상대 장치·절대 장치·화면 끝의 동작은 미검증**이며 상대 방식 합성 장치까지 검증된 것은 아니다.
- 경계에서 마지막 유효 원본 위치에 Up → 입력 끄기 → 렌즈 쪽 커서 복원. 이전 물리 버튼 Up까지 재무장과 추가 클릭을 막는다. 다른 기본 버튼도 개별 추적한다.
- 중지·복귀·닫기·capture 변경·입력 실패·화면 변경·750ms 캡처 지연에서 release를 시도한다. Up 실패는 누름 상태를 보존하고 timer/중지로 재시도하며, 명시적 Stop은 미해제 오류를 반환한다.
- 원래 화면: 입력 해제 → 비활성화 → 두 창 숨김 → 진입 창 복원. `%LOCALAPPDATA%/Magnifier/layout.json`에 원본 물리 영역·렌즈 물리 배치·배율만 저장한다. 입력 허용은 저장하지 않는다.
- PerMonitorV2/asInvoker/uiAccess=false manifest. UI의 물리 화면 변환과 Win32 입력·캡처 구현은 분리했다. 자동 권한 상승·대상 HWND 고정·앱별 분기는 없다.
- 캡처는 비중첩 33ms 타이머 요청과 WriteableBitmap 재사용. UI 포인터 상태는 16ms마다 최신 값만 적용한다. 이 값은 실측 fps/반응 시간의 증거가 아니다.
- A/B 표식·카운트다운·직선 실행은 접힌 보조 도구에 보존하고 직접 입력과 상호 배제한다. 우클릭·휠 등 미지원 직접 입력은 보기로 전환한다. R3 확장은 핵심 실측 후 진행한다.

## 실행 근거

| 검증 | 결과 |
|---|---|
| 시작 cwd/branch/status/diff | 원본 main 확인; 기존 계획·문서·목업·조사 자료 보존 |
| 시작 Magnifier.App 프로세스 | 실행 중인 프로세스 없음; 사용자 앱 종료 없음 |
| `dotnet build Magnifier.slnx --nologo` | 최종 경고 0, 오류 0 |
| `dotnet test Magnifier.slnx --nologo` | 23/23 통과, 실패·건너뜀 0 |
| 회귀 내용 | 원본/렌즈 독립 변환, 음수/비정수 물리 배율, 경계 제외, 저장 좌표 왕복, 기본 off, 물리 해제 대기, 실패 gate off, Up 실패 재시도 |
| UI/Infrastructure 경계 | App P/Invoke 없음, Main composition root에서만 Windows 구현 생성 |
| native 프로브 | probe 창 대상 Down `(288,230)` 수신; 후속 외부 이벤트 감지로 중단 |
| native ABI 검사 | x64 INPUT=40, MOUSEINPUT=32, Mouse offset=8 |

표준 실행본: `C:/ai/projects/magnifier/src/Magnifier.App/bin/Debug/net9.0-windows/Magnifier.App.exe`.
공유 UI 충돌 이후 표준 실행본의 새 실행·UI 조작을 진행하지 않았다. 프로브는 입력 엔진을 검사하는 별도 테스트 창이며 표준 제품 실행본의 수락 검증을 대체하지 않는다.

## 남은 차단과 다음 확인

첫 프로브에는 native Move·Down `(1017,302)`도 나타났고 외부 입력 guard가 작동했다. 그 실행의 guard에는 첫 이벤트의 flags/tag 세부 기록이 없어 사용자 조작·다른 보조 입력·엔진 재진입 중 어느 원인인지 확정하지 못한다. 중단 당시 엔진 상태는 enabled=false, relaying=false, pressed=false, waiting=true였으며 이는 **대상 앱의 최종 release 확인과 다르다**.

`scripts/probes/RelayProbe.cs`에 이후 실행용 첫 충돌 이벤트·직전 명령·수신 태그·viewport·assembly MVID 진단을 추가했다. 비UI `--describe`만 확인했고 UI를 자동 재시도하지 않았다. 사용자에게 해당 시각 마우스/창 조작 여부를 질문했다. 답변 전 UI 재개 금지.

사용자 확인 뒤 표준 실행본에서 원본 이동/크기 → 렌즈만 이동 → 작은 목표 클릭 → 곡선·왕복 드래그 → 경계 Up → 마우스 복귀/재진입을 확인한다. 다음 항목 모두 NEEDS_USER_UI_CHECK다:

1. 브라우저 마커·Blender·일반 Windows 앱의 실제 목표 반응.
2. 원본·렌즈·제어부 캡처 재귀 및 겹침 입력 통과.
3. 상대 물리 마우스와 절대 보조 포인터의 연속성, 모니터 가장자리 탈출.
4. 혼합 DPI·음수 좌표·다중 모니터와 창 이동 후 1px 이내 일치.
5. 경계·닫기·화면 변경·실패 뒤 대상의 최종 Up과 재진입 기본 off.

배치 보완: 사용자가 렌즈 제목을 놓으면 해당 모니터 작업 영역 안으로 보정한다. 원본 영역 변경에는 이 보정을 적용하지 않는다. 모니터 구성이 바뀌면 입력을 해제하고 두 창을 숨긴 뒤 진입 창을 연결된 화면 안에 복원한다. 이 경로와 화면 끝 포인터 이동은 R2 실측 대상이다. UIPI·보호된 화면의 제한은 자동 상승으로 우회하지 않는다.

## 변경과 참고

App의 두 창/진입 흐름·배치 저장·manifest, Core의 LensViewport/LensInputState/relay·window 계약, Infrastructure의 relay·window 환경과 기존 입력/캡처 오류 처리, Core 테스트 및 독립 probe를 변경했다. 최초 구현 응답에서는 커밋하지 않았다. 후속 사용자 요청 「문서 갱신 및 커밋」으로 같은 main checkout의 구현·테스트·계획·목업·조사·상태 문서를 단일 커밋 대상으로 정했다. 푸시는 하지 않는다. `notes/transfers/`는 읽기·수정·삭제·스테이징 대상에서 제외했다.

레이어 창 입력 통과 근거: [Microsoft Window Features](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features). hook의 스레드/시간 제한: [LowLevelMouseProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc). 두 문서의 API 설명을 이 앱의 전달 성공 증거로 쓰지 않는다.

`review-router` → `ollama-router`의 설치 태그 `kimi-k2.7-code:cloud`로 입력 경로를 보조 검토했다. 32비트 창 스타일 entry point와 예외 뒤 원본 이벤트 소유권은 코드 확인 후 보완했다. 실행으로 확인되지 않은 상대 이동 관련 모델 주장은 채택하지 않았다.
