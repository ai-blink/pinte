# 사용자 수동 검증

**컴팩트 렌즈·원본 표시 정책의 최신 검증은 [추가 시나리오](compact-lens-validation.md)를 따른다.** 아래의 “아주 잘됨”은 이전 영역 지정·드래그 수정 확인이며 이번 기능의 통과 근거가 아니다. 현재 기능의 UI 상태는 `NEEDS_USER_UI_CHECK`다.

2026-09-12. delivery workspace는 `C:\ai\projects\magnifier`, branch는 `main`이다. 영역 지정 단계·드래그 유지 수정 뒤 사용자가 **“아주 잘됨”**으로 확인했다. 표준 빌드는 경고 0/오류 0이다. 아래 순서는 이후 재확인용이며 테스트 실행·앱 실행·마우스 조작은 사용자가 직접 한다.

## 실제 앱을 관리자 모드로 열기

1. 실행 중인 Magnifier가 있으면 렌즈의 **원래 화면**을 누르고 진입 창의 X로 닫는다.
2. 저장소 루트의 **`Start-Magnifier-Admin.cmd`를 더블클릭**한다.
3. Windows 권한 확인에 동의한다. 진입 창에서 **화면 영역 지정**을 누른다. 먼저 원본 테두리만 나타난다.

표준 실행본:

`C:\ai\projects\magnifier\src\Magnifier.App\bin\Debug\net9.0-windows\Magnifier.App.exe`

이 EXE를 우클릭해 **관리자 권한으로 실행**해도 된다. 기본 manifest는 `asInvoker / uiAccess=false / PerMonitorV2`이며, 관리자 요청은 진입 파일을 사용자가 실행할 때만 한다. `Start-Magnifier.ps1`은 중복 실행을 막고 실행 경로·해시·PID를 `artifacts/manual-validation/launch-<PID>.json`에 남긴다. 이 기록의 관리자 요청 값은 실제 토큰이나 조작 성공 판정이 아니다.

## 확인할 순서

| 순서 | 조작과 확인 |
|---|---|
| 1 | 원본 테두리를 옮기고 손잡이로 크기를 맞춘다. **이 영역 확대**를 누르면 선택한 영역 그대로 확대 창이 열려야 한다. |
| 2 | 확대 창 안에서 버튼·마커를 클릭하고, 누른 채 곡선·왕복 드래그한다. 도중에 조작이 풀리는지 확인한다. |
| 3 | 버튼을 놓고 바로 다음 클릭·드래그를 한다. **조작 재개**를 다시 누를 필요가 없어야 한다. |
| 4 | **원래 화면**으로 복귀한다. 다시 영역 지정을 열어 확정 전에도 **원래 화면**으로 취소할 수 있어야 한다. |

핵심 흐름 확인 뒤 창 이동·영역 크기·배율·경계 해제·다른 모니터·앱별 호환을 확인한다.

알려진 차단: 일반 앱은 클릭 전·Up 뒤 실제 커서를 렌즈에 유지하지만 **누르는 동안 실제 커서와 노란 렌즈 포인터가 갈라진다**. 관리자 실행만으로 이 현상이 해결됐다고 간주하지 않는다. 화면·입력 실패 시 중지/복귀가 해제하는지도 별도로 확인한다. 실제 검증 전 항목은 `NEEDS_USER_UI_CHECK`다.

## UIAccess 실험은 별도 경로

UIAccess는 일반 관리자 실행과 다르다. 사용자의 앞선 승인으로 전용 인증서와 보호 경로에 설치했으며, 입력 API 수락까지 확인했다. 그러나 WPF 렌즈의 사용자 직접 검사와 네이티브 확대 컨트롤의 합성 입력 검사에서 원본 클릭 전달이 실패했다. 이 엔진은 일반 앱에 연결하지 않았다.

`Start-InputTransform-Manual.cmd`는 설치된 UIAccess 프로브를 연다. 파란 렌즈의 빨간 원을 클릭·짧게 드래그한 뒤 **검증 종료**를 누른다. 이 모드는 자동 클릭·커서 이동을 주입하지 않는다. 원본 창을 직접 누른 클릭은 렌즈 전달 성공으로 세지 않는다. 네이티브 확대 컨트롤 모드가 아닌 WPF 렌즈의 수동 검사다.

- 설치 경로: `C:\Program Files\MagnifierInputTransformTrial`
- 수동 검사 실행본: `probe\RelayProbe.exe --input-transform-manual`
- API/토큰 진단: 설치 폴더의 `Magnifier.App.exe --diagnose-input-transform`
- 결과: `%LOCALAPPDATA%\Magnifier\diagnostics\input-transform-manual-<PID>.json` 및 `input-transform-<PID>.json`
- 네이티브 자동 입력 모드 `--input-transform-native`는 이번 사용자 수동 검증 진입 파일에서 호출하지 않는다.

기존 실험을 제거하려면 관리자 PowerShell에서 `scripts/uiaccess/Remove-InputTransformTrial.ps1 -Apply`를 실행한다. 기본 호출은 제거 계획만 표시한다. 현재 실험 설치와 전용 신뢰 인증서는 유지 중이다.

## 재빌드와 결과 전달

앱을 닫은 뒤 저장소 루트에서 실행한다.

```powershell
dotnet build Magnifier.slnx --nologo
dotnet test Magnifier.slnx --nologo
dotnet build scripts/probes/RelayProbe.csproj --nologo
```

일반 빌드는 표준 출력에 생성되며 UIAccess 설치본을 갱신하지 않는다. 설치된 프로브 갱신은 승인된 별도 설치 스크립트가 서명·실제 EXE manifest를 확인하고 처리한다.

결과는 **대상 앱 이름 / 성공한 단계 / 실패한 조작 / 실제 커서와 노란 포인터가 갈라지는 시점 / 누름 잔류 여부**를 알려주면 된다. [실행 기록](../notes/runs/2026-09-10-uiaccess-pointer-trial.md)에 API 결과와 실제 대상 반응을 분리해 기록했다.

이전 빌드의 반복 드래그 해제는 capture 변경만으로 입력을 끊던 조건을 좁힌 뒤 **USER_CONFIRMED_PASS**로 갱신했다. 사용자 “아주 잘됨”이 수락 근거이며, 앱 이름·세부 경로·이중 포인터까지 따로 확인한 것으로 기록하지 않는다. [최신 확인 기록](../notes/runs/2026-09-12-region-selection-drag-confirmed.md)을 따르고, 과거 실패 로그는 [이전 인계](../notes/runs/2026-09-11-drag-blocked-handoff.md)에 보존한다.
