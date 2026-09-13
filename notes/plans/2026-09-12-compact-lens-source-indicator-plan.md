# 컴팩트 렌즈·원본 표시 계획
## UX 개요
목표: 마우스 사용자에게 가림 없는 확대·정밀 조작·복귀.
문제: 원본 UI 가림·렌즈 resize 조작 부재.
흐름: 영역 지정→확대→숨김/선→영역 편집→완료→자동 재개→복귀.
Must: 표시3종·편집·resize·컴팩트/상하단. Should: 오류·접근성. Could: 아이콘 조정.
안 함: 별도 위치 설정(중복), 입력 엔진 교체(범위 밖).
지표: 기본 원본 UI0·편집 전달 Down0·좌표 오차≤1 원본 픽셀·마우스 복귀 성공.

## 근거·결정
경로: A=src/Magnifier.App, I=src/Magnifier.Infrastructure, AT=src/Magnifier.App.Tests(신규).
파일명 치환: M=MainWindow, P=SelectionPreviewWindow, O=SelectionOverlayWindow, S=SettingsWindow.

|직접 확인 위치|사실/결정|
|---|---|
|CLAUDE.md·doc/INTENT.md AC1~4|AGENTS.md 실파일 없음. 기존 상시 테두리/재지정 제거는 최신 요구로 대체|
|A/MagnifierSettings.cs:8,23; S.xaml Top/BottomToolbarButton; S.xaml.cs:64|ToolbarPlacement Top/Bottom 재사용|
|A/M.xaml.cs:134,268; P.xaml.cs:150 SetToolbarPlacement|같은 Toolbar를 1/4행으로 이동; 일반/컴팩트 모두 이 경로 사용|
|A/O.xaml ContentAperture; O.xaml.cs ApplyRegion:83/PublishRegion:207|chrome 56×108 DIP 제외 source; 접기 대신 Hide|
|A/P.xaml:6; P.ImageLayout.cs:41,136; P.Pan.cs:83|NoResize; ResizeLens는 이미지 크기만. 교집합 crop 유지·viewport SizeChanged 추가|
|I/WindowsLivePointerRelay.cs:40,71,313,327|보류/새 frame gate·savedStyles 전체 복원; Indicator HWND는 relay 목록 제외|

## UX 수치·표시
WPF DIP: 일반 기본=제목52/선1/여백8/최소640×360. 컴팩트=제목·설명·A/B 접기/선1/반경4/그림자·이미지 여백0/최소440×280. 모드 전환은 외곽 rect/zoom 유지, 최소 미달만 확장.
컴팩트 Toolbar: 높이48(2+44+2), 좌우4. 이동44/−44/배율48/+44/손44/편집44/설정44/복귀100=412, 패딩 포함420. 아이콘16·버튼44×44 이상·복귀 글자·한국어 툴팁/접근성 이름. 상하단 공통·자동 숨김 없음.
일반 복원은 설정, 원본 크기는 편집창. 컴팩트는 손 도구로 pan(스크롤바 숨김). 오류 줄은 입력 제외·복귀 유지.
resize 8방향: 외곽 띠8/모서리16, 이미지 밖. 설정 ‘렌즈 크기 조절’로 별도 여백에 손잡이44×44·완료44를 펼침(최소528×368). 반대 변 고정·작업영역/최소 크기 제한·종료 capture 해제.
표시: 숨김 기본/5초/항상 선. 5초는 확대·편집 완료/정책 선택 모달 종료부터, frame/zoom/렌즈 이동은 재시작 없음.
SourceIndicatorWindow=source 내부 선1만·layered·Topmost·ShowActivated=false·Focusable=false·작업표시줄 제외. SetPassiveOverlay(Core 계약→Infrastructure)가 영구 클릭통과/비활성 소유. 캡처 제외 실패=Hide+오류. 선 위도 클릭/포커스 통과. [Microsoft 근거](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features).

## 상태·호환
편집→손 도구 종료→보류/release→Indicator 숨김→편집창. 완료→source 확정/편집창 숨김→표시→layout/Configure→_captureVersion 증가→보류 해제→새 frame+물리 Up→재개. resize/배치도 동일. await·버전으로 오래된 완료/capture/timer 폐기.
Main 편집||모달=외부 보류, 렌즈 외부||손도구||resize=relay 보류. 모달 닫기도 편집 보류 유지. RegionChanged IsVisible guard와 숨은 원본 크기 적용 분리.
Stop/복귀/닫기/실패=요청 취소+release. Up 실패=재개 금지·재시도. 복귀=_sessionVersion 증가/timer 취소/전체 Hide. timeout은 표시만. 제어 UI는 Destination 밖; 재개는 요청 부활/Down 없음.
settings.json 추가: SourceIndicatorPreference {Hidden=0,Brief=1,Always=2}, LensDisplayMode {Normal=0,Compact=1}. 누락=기본, 미지원 새 enum만 필드별 정규화 후 IsValid. 기존 숫자 enum/ToolbarPlacement·임시 교체 저장·실패 안내 유지; 손상 JSON=기존 기본값.
표시/모양/상하단은 RememberLayout과 별도 저장. layout.json Source/Lens/Zoom/LockedAspectRatio 재사용; 수동 크기는 Lens. 기억 끔=다음 위치/크기/zoom 기본값. 입력/편집/timer 비저장.

## 검증·위험
B=dotnet build Magnifier.slnx --nologo, 원본 C:/ai/projects/magnifier·출력 A/bin/Debug/net9.0-windows. T=사용자 dotnet test Magnifier.slnx --nologo. 테스트 실행/UI는 사용자 소유(D-022), NEEDS_USER_UI_CHECK.
Core: LensViewport crop 순수계산·중앙/모서리/음수/우하단 제외/비정수 zoom/DPI100·125·150·200%·release/freshness.
App: fake relay/capture·STA·구 JSON·가짜 시계5초/취소·편집 중 모달·resize Configure→새 frame. 실입력 없음.
사용자: 구 JSON 재실행; 표시3종 선 위 클릭/포커스; 편집8방향→완료→곡선/왕복; 모드2×상하단×크기 변경 중앙/모서리 클릭; pan/resize·혼합 DPI; 드래그 복귀/중첩/캡처 오류; 기억 켬/끔.
위험: 보류/스타일/DPI. 일반+숨김 복구, 역순 revert·저장파일 보존. 코드500줄·문서8KB 제한.

## 원자 태스크
각5~14분, 초과 시 분할. 번호순·files는 위 약기. 공통값은 YAML 상속(tier 없음).
```yaml
공통: &d {layer: A, docs: [본계획], validation: [B], implementation_path: Codex}
태스크:
- {<<: *d, what: "1 enum·정규화", files: [MagnifierSettings.cs], acceptance: "구 JSON 유지·숨김 기본"}
- {<<: *d, what: "2 선택 UI·RefreshControls", files: [S.xaml, S.xaml.cs], acceptance: "상하단 중복 없음"}
- {<<: *d, what: "3 SetPassiveOverlay", layer: src, files: [Magnifier.Core/IWindowEnvironment.cs, Magnifier.Infrastructure/WindowsWindowEnvironment.cs], acceptance: "native는 Infrastructure"}
- {<<: *d, what: "4 표시창·캡처 제외", files: [SourceIndicatorWindow.xaml, SourceIndicatorWindow.xaml.cs], acceptance: "선1 DIP·source 불변"}
- {<<: *d, what: "5 생성/종료·표시 타이머", files: [M.xaml.cs, M.SourceEditing.cs], acceptance: "Hide 기본·늦은 timer 무효"}
- {<<: *d, what: "6 편집/완료·보류 합산", files: [M.SourceEditing.cs, M.xaml.cs, O.xaml.cs, P.xaml.cs], acceptance: "release 후 편집·중첩 보류"}
- {<<: *d, what: "7 편집완료·컴팩트 Toolbar", files: [O.xaml, P.xaml, P.Chrome.cs, P.xaml.cs], acceptance: "상하단 동일 Toolbar·복귀 노출"}
- {<<: *d, what: "8 resize 손잡이·설정 진입", files: [P.Resize.cs, P.xaml, S.xaml, S.xaml.cs], acceptance: "8방향·capture 해제"}
- {<<: *d, what: "9 crop 순수 계산·회귀", layer: src, files: [Magnifier.Core/LensViewport.cs, Magnifier.Core.Tests/LensViewportTests.cs, Magnifier.App/SelectionPreviewWindow.ImageLayout.cs], validation: [B, T], acceptance: "오차≤1픽셀"}
- {<<: *d, what: "10 viewport SizeChanged·pan·frame 경계", files: [P.ImageLayout.cs, P.Pan.cs, P.Resize.cs, M.SourceEditing.cs], validation: [B, T], acceptance: "원본/zoom 유지·구 frame 폐기"}
- {<<: *d, what: "11 ApplySettings·배치 복원", files: [M.xaml.cs, M.SourceEditing.cs, P.Chrome.cs], acceptance: "기억 켬/끔·예외 복귀"}
- {<<: *d, what: "12 STA 테스트 연결", layer: ".", files: [src/Magnifier.App.Tests/Magnifier.App.Tests.csproj, src/Magnifier.App.Tests/TestDoubles.cs, src/Magnifier.App/AssemblyInfo.cs, Magnifier.slnx], acceptance: "fake·실입력 없음"}
- {<<: *d, what: "13 JSON·타이머·보류·resize 회귀", layer: AT, files: [SettingsTests.cs, SourceEditingTests.cs, LensResizeTests.cs], validation: [B, T], acceptance: "실패/취소/중첩 검증"}
- {<<: *d, what: "14 UX/AC·수동 시나리오 동기화", layer: ".", files: [doc/INTENT.md, rules/dev-arch.md, rules/dev-decisions.md, doc/manual-validation.md], validation: ["요구 추적·8KB"], acceptance: "구 문구 대체·사용자 결과만 통과"}
```

## 범위·인계
이번 작업은 계획서만 작성, 코드 미수정. 정책 미결정 없음; hit/최소 크기는 사용자 확인.
PRD 필요(다단계 기능): prd-writer 스킬로 PRD 생성을 권장합니다.
