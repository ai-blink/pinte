# 컴팩트 렌즈·원본 표시 정책 실행 기록

후속 사용자 보고에서 첫 원본 화면 무한 대기가 확인됐다. 첫 비트맵과 좌표 준비 사이의 순환 대기를 수정하고 표준 build 0/0으로 갱신했다. 테스트 3사례는 추가·컴파일만 했다. 실제 확인은 [첫 프레임 수정 기록](2026-09-12-first-frame-wait-fix.md)을 따르며 사용자 재확인 전 PASS로 판정하지 않는다.

## 범위와 보존

- delivery workspace: `C:/ai/projects/magnifier`. 기존 dirty 작업공간에서 작업했다. 커밋·스테이징·푸시·브랜치 생성·기존 변경 정리는 하지 않았다. 최종 staged 파일은 0개다.
- `Magnifier.App.csproj`, `MainWindow.xaml`, `Assets/Pinte.ico`, `Assets/Pinte.png`는 기존 상태를 보존한다. `SettingsWindow.xaml`에는 Pinte/PINTE 문자열을 유지하면서 이번 기능 항목만 추가했다.
- 미추적 계획서 `notes/plans/2026-09-12-compact-lens-source-indicator-plan.md`는 읽기만 했다. 보호한 csproj/Main XAML/두 자산/계획서 5개 파일의 작업 전후 SHA-256 일치를 확인했다. Settings XAML의 기존 Pinte/PINTE 두 변경도 그대로 남아 있다.
- 변경은 INTENT AC1~4·AC6·AC8~9에 연결한다. 이전 원본 편집창 상시 표시 요구는 Hidden 기본/Brief/Always로 대체했다.

## 구현

- `MagnifierSettings`: Normal/Compact와 Hidden/Brief/Always를 추가했다. 구 JSON의 새 필드 누락은 Normal/Hidden이며 미지원 새 enum은 해당 필드만 기본값으로 정규화한다. 기존 설정 숫자 enum과 파일 임시 교체 저장 방식을 유지한다.
- 설정 화면: 렌즈 보기, 원본 표시 정책, 큰 렌즈 크기 조절 진입을 추가했다. 상단/하단은 기존 ToolbarPlacement 하나로 일반·컴팩트에 공통 적용한다. 표시·모양 설정은 RememberLayout과 별도로 기억한다.
- `SourceIndicatorWindow`: 편집창과 별개인 source 내부 1 DIP 윤곽선이다. 비활성·클릭 통과·Topmost 스타일은 Core 계약 `SetPassiveOverlay`와 Infrastructure가 소유한다. HWND를 relay Configure 목록에 넣지 않는다. 표시 전에 캡처 제외를 확인하고 실패 시 Hide와 오류 상태를 유지한다.
- `MainWindow.SourceEditing`: 편집 시작 시 외부 보류·release와 손 도구 종료를 마친 뒤 원본 편집창을 연다. 편집 중 원본 초안은 캡처에 반영하지 않으며 완료 시 창을 숨기고 원본·geometry를 확정한 뒤 새 프레임을 기다린다. 편집 중 모달 종료는 편집 보류를 유지한다. 복귀·종료는 session/capture 버전과 표시 타이머를 무효화한다.
- `SourceIndicatorLifetime`: 확대 시작/편집 완료/정책 선택 모달 종료 때만 5초 기한을 만든다. frame·zoom·렌즈 이동은 기한을 연장하지 않는다. 오래된 Tick은 현재 기한을 확인하며 복귀/편집/정책 변경은 취소한다.
- 렌즈는 일반·컴팩트 chrome, 8방향 외곽 리사이즈, 큰 손잡이와 완료, SizeChanged 후 pan/crop/입력 geometry 갱신을 연결했다. 리사이즈·이동의 창 배치/해제 실패는 Stop으로 요청을 취소한다. Stop 성공 뒤 cached 렌즈의 손/이동/리사이즈 상태와 capture를 정리한다.
- Core LensViewport.TryCreate는 실제 물리 화면의 소수 이미지 위치/크기를 보존한다. crop의 정수 반올림 결과를 다시 확대하지 않아 0.25×와 비정수 배율에서 같은 원본 변환을 유지한다. App의 PointToScreen 측정 결과를 이 순수 계산에 연결했다.

## 계획 수치 조정

컴팩트 필수 버튼에 닫기 44 DIP가 추가되어 계획의 복귀 100 DIP 고정 폭으로는 최소 창 440 DIP에 맞지 않는다. 7개 버튼×44 + 배율48 + 복귀 최소56 + 패딩8 = 420 DIP로 배치하고 바깥 resize/테두리 공간을 확보한다. 복귀는 `복귀` 글자를 유지하고 여유 폭에서는 최대100 DIP로 늘어난다. 최소 창 440×280, 각 hit target 44 이상, Toolbar 높이48을 우선한다. 계획서 자체는 수정하지 않았다.

## 검증 상태

- 표준 `dotnet build Magnifier.slnx --nologo`: **성공, 경고 0/오류 0**. 통합 빌드와 마지막 예외 처리/회귀 기대값 수정 후 최종 빌드 모두 성공했다. 최종 소요 2.17초. 출력은 `C:/ai/projects/magnifier/src/Magnifier.App/bin/Debug/net9.0-windows/Magnifier.App.exe`다. 격리 출력이나 별도 worktree를 사용하지 않았다.
- `dotnet test Magnifier.slnx --nologo`: **미실행 — 사용자 담당**. 테스트 추가/컴파일과 실행 통과를 구분한다.
- 실제 앱 실행·UI·대상 앱 클릭/드래그: **NEEDS_USER_UI_CHECK**. 사용 중인 앱을 종료하거나 마우스·포커스를 조작하지 않았다.
- 신규 App fake/STA 34사례는 설정 호환, 5초/취소, 편집·모달 보류, 오래된 완료와 release 실패, 표시창 제외 실패, 모드/상하단과 손/resize 보류를 다룬다. Core에는 crop/DPI 100·125·150·200%와 배율0.25·1.375·2.5·8, 중앙/모서리·음수 원점·우하단 제외·viewport resize에 대한 19사례를 추가했다. **총 추가53사례는 컴파일 확인만 했고 실행 결과는 없다.**
- App 상태 테스트는 창 Show 없이 fake 계약을 호출한다. 표시창 실패 테스트만 숨은 HWND를 만들고 fake 캡처 제외 실패로 Show 전에 중단한다. 실제 원본 편집 진입과 resize 이벤트→PointToScreen→Configure→새 프레임의 전체 결합은 사용자 UI 확인 대기다.
- `git diff --check` 통과. src의 C#/XAML 500줄 미만, 이번 갱신 문서 8KB 미만 확인. LF/CRLF 안내는 Git의 줄바꿈 경고이며 빌드 경고와 구분한다.
- 읽기 전용 보조 검토: review-router→ollama-router의 설치 태그 `kimi-k2.7-code:cloud`를 사용했다. 보조 지적은 실제 코드를 기준으로 검토하며 추측만으로 변경하지 않았다.

## 사용자 확인 대기

[수동 검증 행렬](../../doc/compact-lens-validation.md): 표시 3종과 선 위 클릭/포커스, 편집 8방향·모달 중첩, 일반/컴팩트×상하단, 리사이즈·큰 손잡이·pan, 비정수 배율·혼합 DPI·1픽셀 좌표, 드래그 복귀·겹침·실패, 설정 기억 켬/끔과 구 JSON 재실행.

이전 사용자의 “아주 잘됨”은 영역 지정·드래그 유지 수정에만 적용한다. 이번 기능이나 M5 전체의 PASS로 확대하지 않는다.
