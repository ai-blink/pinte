# 실제 커서와 노란 가상 포인터 불일치

- 날짜: 2026-09-10, workspace `C:/ai/projects/magnifier`, branch `main`.
- 피드백: **“노란색 가상 포인터 갑자기 왜 생김 실제 마우스 좌표 하고도 다름”**.
- 결과: 클릭 전·Up 뒤 실제 커서 유지 **부분 수정**. 드래그 중 단일 커서는 **BLOCKED**. 커서 문제 전체 완료가 아니다.
- 기준 HEAD `725cd05`, 이번 및 앞선 후속 수정은 미커밋. 푸시 없음. `notes/transfers/` 미변경.

## 원인

WindowsLivePointerRelay는 렌즈에 진입하기만 해도 실제 커서를 원본 물리 좌표로 옮겼다. SelectionPreviewWindow의 노란 원은 별도 논리 렌즈 위치를 그린다. 조작 자동 시작·유지 전환으로 이 표시가 즉시 나타나게 됐다. 사용자에게 필요한 입력 위치와 OS 커서를 분리한 구현상의 문제이며 사용자 설정 탓이 아니다.

## 이번 수정

- hook은 렌즈 안의 새 왼쪽 Down에서만 중계를 시작한다. 클릭하지 않은 이동은 실제 커서를 그대로 둔다.
- 정상 Up을 원본에 보낸 뒤 중계를 끝내고 실제 커서를 마지막 렌즈 위치로 복원한다. 조작 요청은 유지·재개한다.
- 렌즈의 기본 커서는 화살표다. 노란 가상 표시는 중계 누름 중에만 표시한다.
- 누름 중 강제 커서 복귀나 시스템 커서 숨김은 도입하지 않았다. 가상 표시만 지워 해결한 것으로 처리하지 않는다.

변경 소스: `WindowsLivePointerRelay.cs`, `SelectionPreviewWindow.xaml`, `SelectionPreviewWindow.xaml.cs`. `RelayProbe.cs`는 partial로 분리하고 새 `RelayProbe.Cursor.cs`에 커서 진단을 추가했다. App은 Win32 입력을 직접 호출하지 않는다.

## 실행 검증

사용자가 앱 종료와 진단 중 대기를 확인한 뒤 실행했다. 기존 사용자 앱을 임의 종료하지 않았고, 원본 작업공간·표준 출력으로 빌드했다.

| 검증 | 결과 |
|---|---|
| `dotnet build Magnifier.slnx --nologo` | 경고 0·오류 0 |
| `dotnet test Magnifier.slnx --nologo` | Core 34개 통과, 실패·건너뜀 0 |
| `dotnet build scripts/probes/RelayProbe.csproj --nologo` | 경고 0·오류 0 |
| `RelayProbe.exe` | 기존 6개 통과. 분리/겹침 시 클릭 전·Up 후 실제 커서 일치 검사 추가 |
| `RelayProbe.exe --cursor-diagnostic` | DIAGNOSTIC_COMPLETE, **Fixed=false** |

표준 실행본: `C:/ai/projects/magnifier/src/Magnifier.App/bin/Debug/net9.0-windows/Magnifier.App.exe`.

### 수정 전 진단

engine MVID `6b36f680-2e25-44f3-89d8-898e58e2d19d`.

- 렌즈 논리 위치 `(1209,451)`, 실제 커서 `(392,318)`. 클릭 전부터 불일치.
- 진단 자체 태그의 복귀 SendInput을 hook에서 억제: 억제 1회, 실제 커서도 복귀하지 않음.
- 드래그 중 SetCursorPos로 `(1209,451)` 복귀: 실제 커서는 복귀했지만 대상이 그 위치의 **pressed Move**를 받아 추가 획 발생.

### 수정 후 진단

engine MVID `7657f0fb-a4ae-4f37-9c3c-d5d530956387`.

- 클릭 전 실제 커서 `(1209,451)`로 렌즈 위치와 일치.
- Down 뒤 실제 커서 `(392,318)`, 렌즈 논리 위치 `(1209,451)`로 여전히 다름.
- 복귀 SendInput 억제는 커서도 막고, SetCursorPos는 추가 pressed Move를 보냄. 두 경로 모두 드래그 중 단일 커서 해법으로 채택하지 않음.
- 분리/겹침 곡선·왕복 수신, 정확히 1회 Down/Up, 경계 해제·재개, 진입/손잡이 재개, 프레임 지연 복구·명시적 중지 유지 통과. 정상 Up 뒤 실제 커서는 마지막 렌즈 위치와 일치.

진단은 검증 앱의 capture 안에서 수행했고 마지막 유효 원본 위치로 돌아온 뒤 Up을 보냈다. 사용자 입력 충돌은 없었으며 진단 종료 뒤 대상 누름 잔류가 없었다. 실제 장치·브라우저/Blender 결과로 확대하지 않는다.

## 남은 차단과 다음 결정

현재 전역 SendInput 경로는 드래그 중 실제 커서를 원본 위치에 둔다. UI 표시를 바꾸거나 커서를 매번 복귀시키는 것만으로는 사용자 요구를 충족하지 못했다. 드래그 중 단일 커서를 위한 입력 경로를 다시 검증해야 한다.

공식 MagSetInputTransform은 UIAccess를 요구하며 Windows 10 1703 이후 마우스 입력 라우팅도 설명한다. 후보 조사 근거일 뿐, 현재 두 WPF 창에서 성공한다는 증거는 아니다. 서명·신뢰 설치 경로를 수용할지와 해당 경로의 실험 여부가 다음 결정이다. 일반 관리자 실행과 UIAccess를 동일시하지 않는다. manifest는 `asInvoker/uiAccess=false`로 유지했고, 인증서 신뢰 설치·권한 상승·UIAccess 자동 도입은 하지 않았다.

공식 근거:

- [LowLevelMouseProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc): hook에서 대상 메시지를 억제하는 경계.
- [Mouse movement](https://learn.microsoft.com/en-us/windows/win32/learnwin32/mouse-movement): capture 동안 커서가 창 밖에 있어도 이동 메시지를 받음.
- [SetCursorPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setcursorpos): 공유 시스템 커서 좌표 이동.
- [MagSetInputTransform](https://learn.microsoft.com/en-us/windows/win32/api/magnification/nf-magnification-magsetinputtransform): UIAccess 요구와 입력 변환 설명.

## 실제 앱 확인

표준 수정본을 실행하고 `확대 시작`을 눌렀다. 클릭 전·버튼 해제 후 실제 커서 표시의 사용자 확인은 대기 중이다. 드래그 중 이중 포인터는 이미 확인된 차단이며 NEEDS_USER_UI_CHECK만으로 완화하지 않는다. 다른 정밀 경로·복귀·앱별/DPI 검증은 별도로 남는다.
