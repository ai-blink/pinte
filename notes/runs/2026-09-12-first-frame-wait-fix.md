# 첫 원본 화면 무한 대기 수정

- 사용자 보고: 컴팩트/표시 정책 구현 실행본에서 `원본 화면을 기다리는 중`이 끝나지 않는다.
- 작업공간: `C:/ai/projects/magnifier`. 기존 dirty/브랜딩/계획서 보존, 커밋·스테이징 없음.

## 원인 근거

`SetSourceAsync`는 CapturedImage.Source를 비운다. WPF Image는 Source가 없을 때 Measure/Arrange에 0 크기를 반환한다. `ConfigureGeometryAsync`는 실제 이미지 크기가 양수여야 완료할 수 있는데, 기존 `UpdateCapture`가 `_geometryPending` 동안 첫 비트맵 자체를 거부했다. 따라서 첫 이미지 → 좌표 계산의 순서가 역전된 순환 대기다.

WPF 동작 근거는 [dotnet/wpf Image.cs의 MeasureArrangeHelper](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/Image.cs)다. 실제 사용자 환경의 자동 UI 재현은 하지 않았으며, 코드 의존 관계와 공식 구현으로 확인했다.

## 변경

- `SelectionPreviewWindow.xaml.cs`: 좌표 대기 중에도 현재 원본의 캡처를 비트맵에 넣고 대기 문구를 숨긴다. 비트맵 연결 뒤 geometry 갱신을 요청한다.
- 좌표 대기는 `_relay.RefreshFrame()`만 막는다. 최초 표시 프레임은 입력 재개 근거로 소급 인정하지 않고, geometry 확정 뒤 도착한 프레임만 인정한다.
- 종료/캡처 중지/잘못된 원본 프레임의 기존 거부 조건은 유지한다. 새 Start 요청을 추가하지 않는다.
- `FirstFrameTests.cs`: Source 없는 WPF Image와 pending 상태의 첫 프레임 표시, 확정 후 새 프레임만 relay에 인정, 이전 원본 거부의 3사례를 추가했다. 창 표시·실제 입력 없이 기존 fake/STA 호스트를 사용한다.

## 검증

- `dotnet build Magnifier.slnx --nologo`: 성공, 경고 0/오류 0, 3.58초.
- 표준 실행본: `C:/ai/projects/magnifier/src/Magnifier.App/bin/Debug/net9.0-windows/Magnifier.App.exe`.
- 새 회귀 테스트 3사례 컴파일 성공. `dotnet test`는 사용자 방침에 따라 **실행하지 않았다**. 전후 테스트 PASS나 실제 UI 재현 성공으로 보고하지 않는다.
- 실제 UI·클릭/드래그: **NEEDS_USER_UI_CHECK**. 앱 강제 종료·실행·포커스/마우스 조작 없음.
- 앱 프로젝트·Main XAML·Pinte 두 자산·미추적 계획서의 기존 SHA-256 일치 확인.

사용자 확인: 업데이트된 표준 실행본에서 영역 지정→확대를 열어 첫 화면 표시, 영역 재편집 완료 후 화면 갱신, 일반/컴팩트 양쪽에서 다음 클릭·드래그를 확인한다.
