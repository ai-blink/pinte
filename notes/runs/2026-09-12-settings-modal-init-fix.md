# 설정 모달 초기화 오류 수정

- 사용자 보고: 설정 모달이 열리지 않는다.
- 작업공간: `C:/ai/projects/magnifier`. 기존 dirty 변경을 보존하며 커밋·스테이징·푸시·브랜치 생성은 하지 않았다.

## 원인 근거

로컬 진단 로그 `relay-stop-48124.jsonl`의 2026-09-12 17:56:27 KST 및 `relay-stop-54512.jsonl`의 18:28:22 KST에 `앱 설정 실패: Object reference not set to an instance of an object.`가 남아 있다. 두 기록 모두 입력 요청·누름·해제 대기 상태는 false다.

`SettingsWindow.xaml`의 초기 화면 메뉴에 있던 `IsChecked="True"`가 XAML 로딩 중 Checked 이벤트를 발생시킨다. 연결된 `ShowPage`는 뒤에 생성되는 AppearancePanel·DefaultsPanel·AboutPanel을 참조하므로 아직 null인 패널에 접근한다. 생성된 WPF 연결 코드에서도 메뉴 이벤트 연결이 패널 필드 연결보다 앞선다. 실제 UI 자동 재현은 수행하지 않았다.

## 변경

- XAML의 초기 IsChecked를 제거하고 모든 컨트롤 생성과 설정 반영을 마친 생성자 마지막에서 화면 메뉴를 선택한다.
- `_settings`·`_capture`는 InitializeComponent 전에 할당하고 `_syncing` 초기값을 true로 두어 초기화 도중 설정 변경 이벤트를 처리하지 않는다. 초기화 완료 후 사용자 변경 이벤트는 기존대로 전달한다.
- Pinte 설정 제목·PINTE 브랜딩 및 기존 설정 항목을 보존한다. 입력 보류·release·모달 종료 후 새 프레임 확인 흐름은 변경하지 않는다.
- `SettingsWindowTests.cs`에 일반/컴팩트 생성 2사례, 세 메뉴 전환, 초기화 후 설정 변경 전달의 총 4사례를 추가했다. 기존 STA 호스트와 캡처 대역을 사용하며 창을 표시하거나 실제 입력을 보내지 않는다.

## 검증

- 빌드 직전 실행 중인 Magnifier.App 프로세스가 없음을 확인했다.
- 원본 작업공간의 `dotnet build Magnifier.slnx --nologo`: 성공, 경고 0개·오류 0개, 3.60초.
- 표준 실행본: `C:/ai/projects/magnifier/src/Magnifier.App/bin/Debug/net9.0-windows/Magnifier.App.exe`, 갱신 시각 2026-09-12 18:33:19 KST.
- 신규 회귀 테스트 4사례는 컴파일만 완료했다. 사용자 방침에 따라 `dotnet test`는 실행하지 않았다.
- 실제 UI: **NEEDS_USER_UI_CHECK**. 앱 실행·강제 종료·포커스 변경·마우스 조작은 하지 않았다. 테스트 및 UI PASS로 기록하지 않는다.

사용자 확인 범위는 진입 창 및 일반/컴팩트 렌즈의 설정 열기, 화면·시작 설정·정보 메뉴 전환, 설정 변경 반영, 닫은 뒤 렌즈 조작 재개다. 모달이 열린 동안 대상 앱으로 입력이 전달되지 않는지도 확인한다.
