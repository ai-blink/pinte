# Magnifier UI·설정 구현 기록

날짜: 2026-09-12 · 범위: delivery · 상태: **리뷰 P0/P1 보완·자동 검증 완료 / 실제 UI·대상 앱 반응은 NEEDS_USER_UI_CHECK**

## 완료선

승인된 두 창 UX를 유지하면서 1번 상단/3번 하단 도구막대, 원본 크기·비율 빠른 설정, 전체 설정과 저장을 실제 WPF 코드에 연결한다. 설정창이 열려 있는 동안에는 직접 입력 요청을 자동 재무장하지 않으며, 닫은 뒤에 시작한 새 캡처와 물리 버튼 해제 조건을 다시 만족할 때만 중계가 재개된다.

## 구현

- 렌즈의 기본 `조작 재개`·`조작 중지` 버튼을 제거했다. 확대 시작이 조작 요청이고, 복귀·닫기·입력 실패는 기존 release/요청 취소 경로를 사용한다.
- 렌즈 도구막대는 설정에서 1번 상단 또는 3번 하단으로 전환한다. 원본 테두리와 렌즈 모두 `크기·비율`과 앱 설정 진입점을 갖고, 원래 화면은 항상 글자로 표시한다.
- `QuickRegionSettingsWindow`는 16:9·9:16·4:3·3:4·1:1 및 승인된 픽셀 프리셋, 가로/세로 전환, 비율 고정을 제공한다. 프리셋 선택은 프리셋 자체의 비율을 적용하므로 4:3 선택이 기존 16:9로 재계산되지 않는다. 현재 가상 화면보다 큰 프리셋은 이유와 함께 비활성화하고, 방향·비율 적용 결과는 경계에 맞춘 실제 크기로 즉시 다시 표시한다. `ScreenRegionSizing`이 고정·비고정 8개 손잡이와 이동 모두에서 가상 데스크톱 음수 좌표·경계·최소 크기를 물리 픽셀로 계산한다.
- `SettingsWindow`는 화면(도구막대 배치·시스템/밝게/어둡게), 영역·배율(기억 여부·기본 원본 크기·기본 배율), 앱 정보(사용법·진단 폴더)를 제공한다. 설정은 `%LocalAppData%/Magnifier/settings.json`에 저장하며 기존 `layout.json`은 배치·배율·비율 고정을 추가 필드와 함께 계속 읽는다. 입력 허용·누름 상태는 저장하지 않는다.
- `ILivePointerRelay.SetSuspendedAsync`를 추가했다. 빠른 설정·전체 설정을 여는 동안에는 이 명시적 보류가 기존 일시 정지의 자동 재개를 막는다. 두 설정창은 캡처에서 제외한다. `FrameFreshnessGate`는 suspend 해제 시 기존 프레임을 무효화하고, Main의 capture revision은 닫기 전 시작된 캡처를 버리므로 닫은 뒤 캡처한 새 프레임 전에는 중계가 재무장하지 않는다. 설정 창은 Esc로 닫을 수 있다.

## 변경 파일

- App UI·상태: `App.xaml`, `MainWindow.*`, `SelectionOverlayWindow.*`, `SelectionPreviewWindow.*`, `LensLayoutStore.cs`, `MagnifierSettings.cs`, `QuickRegionSettingsWindow.*`, `SettingsWindow.*`
- Core·중계: `FrameFreshnessGate.cs`, `ScreenRegionSizing.cs`, `ILivePointerRelay.cs`, `WindowsLivePointerRelay.cs`
- 회귀: `FrameFreshnessGateTests.cs`, `ScreenRegionSizingTests.cs`

## 검증 증거

- `dotnet build Magnifier.slnx --nologo` — 경고 0, 오류 0.
- `dotnet test Magnifier.slnx --nologo` — Core 40개, Infrastructure 11개, 총 51개 통과.
- 새 Core 회귀는 suspend 해제 경계 전 프레임 무효화·새 프레임 필요성, 고정/비고정 비율의 모서리/변 손잡이, 반대 기준점, 음수 가상 데스크톱 보정을 확인한다. 기존 입력 세션·capture 감시·release 회귀도 전체 테스트에 포함된다.

## 실제 UI 상태와 차단

- 표준 실행본은 `C:/ai/projects/magnifier/src/Magnifier.App/bin/Debug/net9.0-windows/Magnifier.App.exe`다. 이번 시작 시 실행 프로세스는 없었다.
- 네이티브 UI 자동화 채널이 제어 가능한 앱을 반환하지 않았고(`apps: []`), 표준 실행본을 대상 창으로 열기 위한 지원 API도 노출하지 않았다. 따라서 창 생성, 상단/하단 실제 배치, 크기 패널 클릭 차단, 캡처 제외, 실제 클릭·곡선/왕복 드래그, 브라우저·Blender·Windows 앱 반응은 확인하지 않았다.
- 위 항목은 `NEEDS_USER_UI_CHECK`다. `SendInput` 수락·빌드·테스트만으로 대상 반응이나 범용 호환을 완료로 기록하지 않는다.

## 다음 사용자 확인

1. 표준 실행본에서 `화면 영역 지정` → 테두리 이동/8개 손잡이 → `이 영역 확대`를 수행한다.
2. 렌즈와 원본의 `크기·비율`을 각각 열어 프리셋, 16:9·4:3·1:1, 고정, 가로/세로를 적용한다. 렌즈 이동은 원본 영역을 바꾸지 않는지 확인한다.
3. 앱 설정에서 상단/하단 배치와 밝게/어둡게를 바꾼 뒤 원래 화면 복귀와 재진입을 확인한다.
4. 설정을 원본 영역 위에 열어도 렌즈에 설정 UI가 재귀하지 않는지, 닫은 직후에는 새 대상 프레임이 온 뒤 버튼을 놓은 상태에서만 조작이 재개되는지 확인한다.
5. 브라우저 마커·Blender·일반 Windows 앱에서 작은 목표 클릭과 곡선·왕복 드래그, 경계·복귀·닫기 뒤 release를 확인한다.

## 다음 세션 프롬프트

```text
C:/ai/projects/magnifier 에서 이어서 작업해줘.

먼저 확인:
1. cwd가 C:/ai/projects/magnifier 인지, branch와 dirty 상태가 무엇인지 확인한다.
2. 기존 문서·목업·소스 변경을 되돌리지 않고, notes/transfers/는 수정·삭제·스테이징하지 않는다.
3. 자동 커밋·푸시는 하지 않는다.

먼저 읽을 문서:
- C:/ai/projects/magnifier/rules/dev-context.md
- C:/ai/projects/magnifier/rules/dev-progress.md
- C:/ai/projects/magnifier/rules/dev-roadmap.md
- C:/ai/projects/magnifier/notes/runs/2026-09-12-magnifier-ui-settings-implementation.md
- C:/ai/projects/magnifier/notes/plans/2026-09-12-magnifier-ui-settings.md

검증된 읽을 파일:
- C:/ai/projects/magnifier/src/Magnifier.App/MainWindow.xaml.cs
- C:/ai/projects/magnifier/src/Magnifier.App/SelectionOverlayWindow.xaml.cs
- C:/ai/projects/magnifier/src/Magnifier.App/SelectionPreviewWindow.xaml.cs
- C:/ai/projects/magnifier/src/Magnifier.App/QuickRegionSettingsWindow.xaml.cs
- C:/ai/projects/magnifier/src/Magnifier.App/SettingsWindow.xaml.cs
- C:/ai/projects/magnifier/src/Magnifier.Core/ScreenRegionSizing.cs
- C:/ai/projects/magnifier/src/Magnifier.Infrastructure/WindowsLivePointerRelay.cs

없는 파일 / 구현 후보:
- 없음. 우클릭·더블클릭·스크롤은 R3 검증 전 신규 구현 후보로만 취급한다.

최근 완료:
- UI·설정 구현·리뷰 보완은 build 경고/오류 0, 총 51개 테스트 통과. 실제 UI·대상 앱 반응은 아직 NEEDS_USER_UI_CHECK다.

다음 목표:
- 사용자 수동 결과에서 재현되는 BLOCKER만 수정하고, 브라우저·Blender·Windows 앱의 실제 직접 조작·복귀 증거를 분리해 기록한다.

스코프 모드:
- scope_mode: delivery
- answer_shape: roadmap-first
- active_constraints: .NET 9 WPF, 독립 원본·렌즈, 마우스 직접 조작/복귀, release, UI와 Infrastructure 경계, 기존 변경 보존.
- stale_constraints: UI 목업·계획만 작성하고 WPF 구현을 미루는 단계, 기본 입력 시작/중지 토글, A/B 한 획을 핵심 완료 근거로 삼는 단계.

작업 방식:
- 계획·탐색·문서만으로 제품 완료를 선언하지 않는다.
- focused tests 후 `dotnet test Magnifier.slnx --nologo`와 `dotnet build Magnifier.slnx --nologo`를 실행한다.
- 실제 확인 결과를 이 run 기록과 rules/dev-context.md·dev-progress.md·dev-roadmap.md에 남긴다.
```

재사용 검토: `skip` — 이번 구현은 Magnifier의 두 창·중계 보류 계약에 국한되며 새 범용 스킬 후보는 만들지 않는다.
