# UIAccess 입력 실험과 사용자 관리자 검증 구성

- workspace: `C:/ai/projects/magnifier`, branch `main`, 기준 HEAD `725cd05`.
- 결과: **UIAccess API 수락 확인 / OS 마우스 전달 BLOCKED / 사용자 관리자 검증 준비 완료**.
- 최신 지시: “그냥 내가 관리자 모드로 검증 할게 빌드 및 구성 완성 해”. 이후 자동 UI 입력·앱 실행을 중단하고 표준 빌드와 사용자 실행 진입 파일을 준비했다.
- 드래그 중 이중 포인터는 해결되지 않았다. 관리자 실행이나 이 실험의 API 수락을 M5 완료로 계산하지 않는다.

## 실제 적용한 UIAccess 설치

앞선 신뢰 등록·보호 경로 설치 질문에 사용자가 “go”로 승인했다. 승인 뒤 다음 범위만 적용했다.

- 설치: `C:/Program Files/MagnifierInputTransformTrial`, 프로브는 그 아래 `probe/RelayProbe.exe`.
- 전용 인증서: `CN=Magnifier Input Transform Trial`, thumbprint `923D9B7848D948F11D35FB65C84DADAD3792910A`.
- CurrentUser/My의 비내보내기 개인 키와 LocalMachine/Root의 공개 신뢰 인증서. 만료 2026-10-10 22:15 KST. 기존 다른 제품 인증서는 사용하지 않았다.
- 서명 상태 Valid. 런타임 DLL·deps·runtimeconfig 전체를 복사했다. 일반 표준 출력은 `InputTransformTrial=false`로 복원했다.
- 실제 설치본 진단 `input-transform-20140.json`: TokenUIAccess=true, TokenElevated=false, 항등 변환 설정·조회·해제 성공, 전후 enabled=false. 결과는 `API_ACCEPTED_NOT_UI_VERIFIED`.
- 설치/제거 스크립트 기본 호출은 계획 조회다. 설치는 실행했지만 제거는 실행하지 않았다. 현재 전용 설치와 신뢰는 유지 중이며 일반 관리자 실행과 별개다.

설치된 프로브 manifest가 증분 빌드 캐시 때문에 처음에는 uiAccess=false였던 문제도 수정했다. `-t:Rebuild`, 실험 전용 컴파일 상수·WinExe 설정을 사용하며, 설치된 실제 EXE manifest를 mt.exe로 추출해 asInvoker/uiAccess=true인지 확인한 뒤 서명한다. MSBuild 속성 조회만으로 판정하지 않는다.

## 구현한 경계

- `WindowsInputTransform`: OS 원본/목적지 물리 RECT를 설정·조회·해제한다. 버튼 눌림과 기존 외부 변환을 검사하고 자기 변환을 확인해 정리한다. 커서 이동이나 버튼 입력을 주입하지 않는다. Windows API에는 소유자 ID/원자적 설정이 없어 동일 RECT의 외부 변환·프로세스 간 경쟁은 완전히 구분할 수 없다.
- `WindowsMagnifierSurface`: native Magnifier child, 배율 행렬, 원본 RECT, 부모/자기 창 제외 필터와 갱신·해제. 실험의 네이티브 부모는 프로브가 소유한다.
- `RelayProbe.InputTransform*`와 `RelayProbe.NativeHost`: WPF/네이티브 렌즈의 실제 수신과 물리 커서를 비교한다. 자동 모드는 외부 입력/포커스 충돌에 중단하고 자기 Up만 정리한다. 수동 모드는 자동 입력을 보내지 않는다.
- 일반 제품은 기존 `WindowsLivePointerRelay`를 사용한다. OS 변환 후보를 제품 입력 엔진에 연결하지 않았다.

## 실제 입력 결과

원본 JSON 폴더: `C:/Users/user/AppData/Local/Magnifier/diagnostics/`.

| 검사 | 원본 수신 / 렌즈 수신 | 판정 |
|---|---|---|
| WPF 렌즈 합성 입력, `input-transform-pointer-20248.json` | 원본 Down/Up 0/0, 렌즈 Down 1 | BLOCKED_OS_MOUSE_ROUTING |
| 사용자 직접 검사, `input-transform-manual-21512.json` | 렌즈에서 원본으로 전달 0, 렌즈 자체 Down 4 | BLOCKED_OS_MOUSE_ROUTING |
| native Magnifier, `input-transform-pointer-40744.json` | 원본 Down/Up 0/0, 렌즈 Down 1 | BLOCKED_OS_MOUSE_ROUTING |

사용자는 파란 렌즈 조작 뒤 **검증 종료**를 눌렀다고 확인했다. 원본 창을 직접 누른 Down/Up 1/1은 FromLens=false여서 전달 성공에서 제외했다. 이 검사는 사용자의 현재 입력 경로이며, 원시 하드웨어 상대 입력만 검사했다고 주장하지 않는다.

최종 native 실행은 사용자 “재시작 / 재시도” 뒤 외부 충돌 없이 끝났다. 렌즈 물리 위치 `(1017,302)`에서 기대 원본 `(288,230)`으로 클릭이 도착하지 않았다. 커서는 렌즈에 남았고 렌즈 부모 HWND가 Down을 받았다. Down 전달 실패로 곡선·왕복 이동은 실행하지 않았다. 이 모드는 합성 절대 입력 결과이며 native 컨트롤의 실제 사용자 장치 검증을 대체하지 않는다.

모든 위 유효 실행은 Released=true, TransformDisabled=true로 종료했다. 최신 engine MVID는 `5ceb9788-90f3-4ee0-a3c1-45d7f8fc9355`다.

native 실험의 중간 실패도 구분한다. PID40976은 WPF layered 부모 설정 실패로 입력 전 종료했다. PID40684는 STATIC 부모의 위치 조회 문제로 클릭 전 중단했다. 두 결과는 OS 입력 전달 실패의 근거가 아니다. 일반 native class로 교체한 후 최종 PID40744를 얻었다. PID20216/42236의 외부 이동 감지 중단은 NEEDS_USER_UI_CHECK이며 사용자 재개 요청 뒤에만 재시도했다.

## 사용자에게 전달하는 구성

- `Start-Magnifier-Admin.cmd`: 사용자가 더블클릭하면 표준 Magnifier.App.exe에 RunAs를 요청한다. 설치·자동 클릭 없이 실제 앱을 연다.
- `Start-InputTransform-Manual.cmd`: 이미 승인·설치된 UIAccess WPF 수동 프로브를 연다. 자동 검사 모드를 호출하지 않는다.
- `scripts/Start-Magnifier.ps1`: 경로·서명(실험본)·중복 프로세스 확인, CheckOnly, 실행 해시/PID 기록. 사용자 클릭으로 시작한 프로세스만 실행하며 기존 앱을 종료하지 않는다.
- 표준 실행본: `C:/ai/projects/magnifier/src/Magnifier.App/bin/Debug/net9.0-windows/Magnifier.App.exe`.
- 사용 순서와 확인표: [수동 검증 안내](../../doc/manual-validation.md).

## 빌드와 남은 범위

- `dotnet build Magnifier.slnx --nologo`: 경고 0, 오류 0.
- `dotnet test Magnifier.slnx --nologo`: Core 34개 통과, 실패/건너뜀 0.
- `dotnet build scripts/probes/RelayProbe.csproj --nologo`: 경고 0, 오류 0.
- PowerShell 구문 검사와 진입점 3개 모드의 CheckOnly 통과. CMD가 사용하는 PowerShell 7 경로 확인. 사용자 인수 후 앱을 실행하지 않았고 최종 Magnifier/RelayProbe 프로세스 0개, staged 파일 0개다.
- 일반 표준 EXE 내장 manifest는 asInvoker/uiAccess=false. UIAccess 설치본의 성공과 일반 관리자 실행을 구분한다.
- 다음 검증은 사용자가 관리자 모드의 실제 제품에서 수행한다. 실제 대상 반응·드래그 중 커서·마우스 복귀·브라우저/Blender/Windows 앱·혼합 DPI는 사용자 확인 전까지 미완료다.
- OS 변환의 API 권한 차단은 해소됐지만 두 시험 경로의 전달이 실패했다. 이 후보의 제품 연결을 보류한다. 이후 입력 엔진을 선택할 때 실제 단일 커서 요구와 표시 대체 방식을 구분해야 한다.
- 변경은 미커밋이다. 자동 스테이징·커밋·푸시 없음. `notes/transfers/`, `.codex/config.toml`, `.deck-build/`, 작업 중 나타난 `output/`과 기존 계획·조사 변경을 보존했다.

공식 근거: [MagSetInputTransform](https://learn.microsoft.com/en-us/windows/win32/api/magnification/nf-magnification-magsetinputtransform), [UIAccess](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-securityoverview), [Magnification API](https://learn.microsoft.com/en-us/windows/win32/winauto/magapi/magapi-intro), [WindowFromPoint의 STATIC 예외](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-windowfrompoint).
