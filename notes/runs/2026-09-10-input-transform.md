# OS 입력 변환의 UIAccess 차단 확인

- 2026-09-10, delivery workspace `C:/ai/projects/magnifier`, branch `main`, 기준 HEAD `725cd05`.
- 사용자 “ㄱ”에 따라 드래그 중 단일 커서 후보인 MagSetInputTransform 실행 경로를 진행했다.
- 결과: **BLOCKED_UIACCESS_ACCESS_DENIED**. 표준 실행본의 실제 API 호출이 오류 5를 반환했다. 드래그 중 이중 포인터 해결이나 두 WPF 창의 OS 입력 변환 완료가 아니다.
- 기존 후속 수정과 이번 수정은 미커밋이다. 인증서·신뢰 저장소·Program Files·UAC 정책은 변경하지 않았다. `notes/transfers/` 미변경, 스테이징·커밋·푸시 없음.

## 구현

- `WindowsInputTransformDiagnostic.cs`: Infrastructure에서 실제 토큰의 UIAccess/상승 여부, Magnification 초기화·변환 조회·설정·종료를 실행한다. 기존 `LensViewport`의 원본·렌즈 물리 영역을 native RECT로 변환하되, 이번 진단은 source=destination인 항등 변환으로 제한했다. 클릭·드래그·커서 이동을 주입하지 않는다.
- 기존 변환이 켜져 있거나 버튼이 눌려 있으면 설정하지 않는다. 성공한 자기 항등 변환만 해제하고 종료 상태를 다시 읽는다. 실제 대상 반응과 단일 커서 확인 값은 항상 false이며 API 수락과 구분한다.
- App의 `--diagnose-input-transform`은 MainWindow/기존 hook 엔진을 생성하기 전에 진단하고 JSON을 `%LOCALAPPDATA%/Magnifier/diagnostics/input-transform-<PID>.json`에 기록한다. 일반 진입 창의 StartupUri는 일반 실행 경로에서만 설정한다.
- `InputTransformTrial=true`는 선택형 `app.uiaccess.manifest`와 진단 전용 컴파일 상수를 선택한다. 기본은 기존 `asInvoker/uiAccess=false`다. 실험 빌드는 진단 인수가 없으면 코드 4로 종료하며 일반 SendInput 엔진을 열지 않는다.
- `scripts/uiaccess/Install-InputTransformTrial.ps1`, `Remove-InputTransformTrial.ps1`을 준비했다. 기본 실행은 계획 조회뿐이며 `-Apply`는 실행하지 않았다.

## 표준 실행본의 결과

실행 전 Magnifier 프로세스가 없는 것을 확인했다. 사용자 실행본을 임의 종료하지 않았다.

`C:/ai/projects/magnifier/src/Magnifier.App/bin/Debug/net9.0-windows/Magnifier.App.exe --diagnose-input-transform`

| 항목 | 실제 관찰 |
|---|---|
| 프로세스/OS | X64, Windows NT 10.0.28120.0 |
| TokenUIAccess / TokenElevated | false / false |
| MagInitialize | true |
| MagGetInputTransform(before) | true, enabled=false |
| 요청 RECT | source=destination=(0,0,3840,2160) |
| MagSetInputTransform(identity) | **false, Win32 오류 5** |
| MagGetInputTransform(cleanup) | true, enabled=false |
| MagUninitialize | true |
| 진단 종료 코드 | 2 (차단 결과) |
| 대상 반응 / 단일 커서 | 미검증 / 미검증 |

최종 engine MVID: `409965da-5e4c-4bf1-ae50-ef2ddcec1215`.
원본 JSON: `C:/Users/user/AppData/Local/Magnifier/diagnostics/input-transform-59644.json`.

첫 실행은 StartupUri에 null을 설정한 WPF 예외로 API 전 종료했다. .NET Runtime 이벤트의 예외 위치를 확인해 진입점을 수정한 뒤 위 결과를 얻었다. 초기 예외를 UIAccess 실패 근거로 사용하지 않았다.

## 검증 범위

- `dotnet build Magnifier.slnx --nologo`: 경고 0, 오류 0.
- `dotnet test Magnifier.slnx --nologo`: Core 34개 통과, 실패·건너뜀 0.
- 설치·제거 PowerShell Parser 오류 0. 기본 계획 조회 실행, 설치 폴더 없음 확인.
- MSBuild 속성 조회: true/false 모두 동일 표준 Debug 출력, manifest·컴파일 상수 선택 분리 확인. UIAccess 빌드/서명/설치/제거의 실제 실행 성공을 뜻하지 않는다.
- 제거 코드 점검 후 설치 계정 SID 일치 검사와 CurrentUser 전용 인증서의 `-DeleteKey` 정리를 보완했다.
- 표준 일반 실행에서 진입 창·확대 시작·아래쪽 배치 버튼 표시를 실제 UI로 확인했다. 확인용으로 연 창은 X로 닫았고 프로세스 종료를 확인했다.
- 기존 native 6개는 앞선 커서 수정 때의 통과 기록이다. 이번에는 다시 입력하지 않았다. 브라우저/Blender/Windows 앱의 확대 조작, 혼합 DPI·겹침·복귀는 새 검증하지 않았다.

## 다음 실행에 필요한 승인과 조건

공식 API는 UIAccess를 요구한다. 일반 관리자 실행만으로 대체할 수 없다. 보호 경로 설치·신뢰되는 Authenticode 서명·uiAccess manifest가 필요하며, 현재 일반 표준 실행본은 이 조건을 갖추지 않는다.

준비한 설치의 정확한 범위:

1. 같은 원본 작업공간·표준 출력에서 선택형 실험본을 빌드한다.
2. `CN=Magnifier Input Transform Trial` 전용 코드 서명 인증서를 CurrentUser/My에 새로 만든다. 유효기간 30일, 개인 키는 비내보내기다. 기존 FlowType 등의 인증서는 사용하지 않는다.
3. 새 인증서의 공개 부분을 LocalMachine/Root에 등록한다. 이 단계는 Windows가 해당 테스트 서명을 신뢰하도록 바꾸므로 명시적 승인·관리자 작업이 필요하다.
4. 실행에 필요한 파일 전체를 `C:/Program Files/MagnifierInputTransformTrial`에 설치하고 그 EXE를 서명한다. 기존 폴더는 덮어쓰지 않는다.
5. 성공·실패 뒤 원본 표준 출력은 `InputTransformTrial=false`로 재빌드한다. 설치본 자동 실행·자동 권한 상승·보안 정책 변경은 없다.
6. 승인 후 설치본의 실제 토큰과 API 호출을 먼저 재검사한다. API가 열려야 두 창의 클릭·곡선/왕복·경계 release·단일 커서 반응을 시험할 수 있다. 아직 해당 입력 엔진을 제품 경로에 연결하지 않았다.

제거 스크립트는 설치 기록의 인증서 thumbprint/생성 계정·고정 경로를 확인하고 전용 Root/My 인증서·개인 키·설치 폴더만 정리한다. 설치/제거는 정적 검사까지만 완료했다. 설치 승인은 원래 요청의 “UIAccess … 자동 도입하지 않는다” 조건 때문에 별도로 받아야 한다.

공식 근거: [MagSetInputTransform](https://learn.microsoft.com/en-us/windows/win32/api/magnification/nf-magnification-magsetinputtransform), [UIAccess 요건](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-securityoverview), [Certificate Provider의 DeleteKey](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.security/about/about_certificate_provider?view=powershell-5.1).
