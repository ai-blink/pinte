# M2 화면 캡처·좌표 변환 계획

## 1. 목표

무엇을 끝내야 하는가:
- 사용자가 고른 화면 사각 영역을 물리 픽셀 프레임으로 한 번 캡처해 확대 미리보기에 표시하고, 미리보기 좌표를 원본 화면 좌표로 환산하는 계약을 만든다.

완료 기준:
- 실제 선택 화면이 자리표시자 대신 미리보기 창에 표시된다.
- Core의 좌표 환산은 경계·확대 비율 단위 테스트로 검증된다.
- 캡처 실패는 성공처럼 보이지 않고 원인을 표시한다. 실제 입력 전달은 생성하지 않는다.

## 2. 범위

이번에 할 것:
- `Magnifier.Core`에 물리 화면 영역·BGRA32 프레임·미리보기 좌표 환산 계약을 만든다.
- `Magnifier.Infrastructure`에 Windows GDI 화면 캡처 구현을 둔다.
- App이 선택 직후 오버레이를 닫고 캡처 결과 또는 오류를 미리보기에 표시한다.
- Core 단위 테스트와 M2 흐름 다이어그램을 추가한다.

이번에 하지 않을 것:
- Down·Move·Up 입력 전달, 대상 창 해석, 연속 캡처·스트리밍, 자유 경로 재생, 대상 앱별 제어.

## 3. 접근

큰 단계:
1. WPF 선택 지점의 화면 좌표를 물리 픽셀 `ScreenRegion`으로 확정한다.
2. Core에 프레임·좌표 환산 계약과 단위 테스트를 만든다.
3. Infrastructure에서 선택 영역을 BGRA32로 복사하고 App이 WPF 이미지로 표시한다.
4. 캡처 실패 표기, 빌드·테스트·실제 창 확인으로 결과를 검증한다.

중요한 판단 기준:
- GDI의 화면 복사는 Infrastructure에만 두고 App은 Core 계약으로만 결과를 소비한다.
- WPF DIPs와 GDI 물리 픽셀을 섞지 않는다. 오버레이의 `PointToScreen` 결과를 물리 화면 영역으로 변환한다.
- App·Core·Infrastructure 세 경계의 데이터 흐름이 있으므로 구현 전 Mermaid 흐름도를 추가한다.

## 4. 검증

통과해야 할 확인:
- `dotnet test Magnifier.slnx --nologo`
- `dotnet build Magnifier.slnx --nologo`
- 실제 창에서 선택한 화면 이미지가 미리보기에 보이고, 캡처할 수 없는 상태는 원인을 표시한다. 자동화할 수 없으면 `NEEDS_USER_UI_CHECK`로 기록한다.

## 5. 남는 리스크

주의할 점:
- 보호된 창·권한 경계·일부 하드웨어 표면은 캡처가 실패하거나 빈 이미지로 보일 수 있다.

후속으로 넘길 것:
- M3에서 미리보기 Down·Move·Up을 `ScreenRegion`에 환산하고, 기본 비활성 입력 gate와 release 경로를 추가한다.

## 실행 규율

- Finish Line: 선택된 실제 화면 프레임과 검증된 좌표 환산 계약을 실행 가능한 코드·테스트로 남긴다.
- Scope Limit: `src/Magnifier.App`, 새 Core·Infrastructure·Core 테스트 프로젝트, M2 계획·상태·캡처 흐름 문서만 변경한다.
- Review Budget: 빌드·테스트·수동 확인에서 발견된 M2 차단 문제만 최대 두 번 수정한다.
- Stop Rule: 수락 확인이 통과하면 입력 전달·연속 캡처·대상 창 통합을 시작하지 않는다.
- Completion Impact: 확대창이 사용자가 고른 실제 화면 정보를 보여 주어 M3 입력 안전성 검증의 기준 좌표를 제공한다.

## 실행 증거

- 2026-09-08: `dotnet build Magnifier.slnx --nologo`가 경고·오류 0으로 통과했다.
- 2026-09-08: `dotnet test Magnifier.slnx --nologo`에서 Core 좌표 계약 테스트 3개가 통과했다.
- 2026-09-08: 사용자가 실제 화면 캡처 성공을 확인했다.
