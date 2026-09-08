# M1 영역 선택·확대 미리보기 실행 기록

## 결과

- 상태: `DONE`
- 완료선: `영역 선택`으로 반투명 오버레이를 열고, 사각 선택 결과를 이동·크기 조절 가능한 미리보기 창에 표시한다.
- 수락 증거: `dotnet build Magnifier.slnx --nologo` 경고·오류 0, 사용자의 실제 UI 동작 확인 통과 (2026-09-08).

## 범위와 변경

- `src/Magnifier.App/MainWindow.*`: 큰 `영역 선택` 진입점과 선택 상태를 추가했다.
- `src/Magnifier.App/SelectionOverlayWindow.*`: 가상 화면 반투명 오버레이, 사각 선택, Esc 취소와 WPF pointer capture 해제를 추가했다.
- `src/Magnifier.App/SelectionPreviewWindow.*`: 표시용 선택 좌표·크기와 캡처 미연결 상태를 resizable 창에 표시했다.
- M1 계획과 `rules/dev-context.md`, `rules/dev-progress.md`, `rules/dev-roadmap.md`를 현재 상태로 갱신했다.

## 검증

- `dotnet build Magnifier.slnx --nologo` — 통과, 경고 0, 오류 0.
- `dotnet test Magnifier.slnx --nologo` — 통과; 현재 테스트 프로젝트는 없다.
- `git diff --check` — 통과.
- 사용자 수동 확인 — `영역 선택` 흐름이 정상 동작한다고 확인했다.
- 로드맵 형태 검사 — 자동 validator의 `harness` 모듈은 이 환경에 없었다. 7개 비어 있지 않은 줄, milestone 수준, 중첩 체크리스트 없음은 수동으로 확인했다.

## 후속과 제외 범위

- `FOLLOW_UP`: M2에서 화면 캡처와 원본 화면 좌표 변환 계약을 설계한다. `Magnifier.Core`와 `Magnifier.Infrastructure`는 해당 책임 분리가 필요하다는 근거가 생길 때만 제안한다.
- `FOLLOW_UP`: 실제 대상 앱 입력과 release 경로는 M3 이후 범위다.
- 재사용 후보: `skip`. 이번 결과는 Magnifier에 한정된 WPF 화면 흐름이다.

## 다음 세션 프롬프트

```text
C:\ai\projects\magnifier 에서 이어서 작업해줘.

먼저 확인:
1. cwd가 C:\ai\projects\magnifier 인지 확인한다.
2. git status를 읽고 기존 변경을 되돌리지 않는다.
3. M1 변경은 아직 커밋되지 않았으므로, 구현 전 현재 diff를 다시 확인한다.
4. 다음 목표의 실제 변경 대상은 먼저 src\Magnifier.App이며, Core/Infrastructure 프로젝트 신설은 M2 계약에 필요하다는 근거가 생길 때만 제안한다.

먼저 읽을 문서:
- C:\ai\projects\magnifier\CLAUDE.md
- C:\ai\projects\magnifier\doc\INTENT.md
- C:\ai\projects\magnifier\rules\dev-context.md
- C:\ai\projects\magnifier\rules\dev-progress.md
- C:\ai\projects\magnifier\rules\dev-roadmap.md
- C:\ai\projects\magnifier\notes\runs\2026-09-08-m1-region-selection.md
- C:\ai\projects\magnifier\notes\plans\2026-09-08-magnifier-m1.md

검증된 읽을 파일:
- C:\ai\projects\magnifier\Magnifier.slnx
- C:\ai\projects\magnifier\src\Magnifier.App\Magnifier.App.csproj
- C:\ai\projects\magnifier\src\Magnifier.App\MainWindow.xaml
- C:\ai\projects\magnifier\src\Magnifier.App\MainWindow.xaml.cs
- C:\ai\projects\magnifier\src\Magnifier.App\SelectionOverlayWindow.xaml
- C:\ai\projects\magnifier\src\Magnifier.App\SelectionOverlayWindow.xaml.cs
- C:\ai\projects\magnifier\src\Magnifier.App\SelectionPreviewWindow.xaml
- C:\ai\projects\magnifier\src\Magnifier.App\SelectionPreviewWindow.xaml.cs

없는 파일 / 구현 후보:
- C:\ai\projects\magnifier\src\Magnifier.Core — 현재 없음. M2에서 좌표·선택 영역 계약이 UI에서 분리돼야 할 때 후보로 검토한다.
- C:\ai\projects\magnifier\src\Magnifier.Infrastructure — 현재 없음. 화면 캡처 또는 입력 전달 경계가 시작될 때 후보로 검토한다.
- C:\ai\projects\magnifier\docs\diagrams\input-session.md — 현재 없음. 실제 입력 세션 상태 전이가 구현될 때 후보로 검토한다.

최근 완료:
- M1 영역 선택 오버레이와 확대 미리보기 — build 경고/오류 0, 사용자 수동 UI 확인 통과 (2026-09-08).

다음 목표:
- M2: 화면 캡처·원본 화면 좌표 변환 계약의 최소 설계를 확정한다. 실제 입력 전달은 구현하지 않는다.

스코프 모드:
- scope_mode: delivery
- answer_shape: roadmap-first
- active_constraints: .NET 9 WPF만 사용; 범용 Windows 오버레이; 실제 입력은 기본 비활성; 자유 경로 재생과 대상 앱 브러시·레이어·색상 제어는 제외.
- stale_constraints: M1의 docs-only 또는 UI 수동 확인 대기 상태는 종료됐다.

작업 방식:
- 현재 diff와 테스트 상태를 먼저 확인한다.
- BLOCKER만 현재 run에서 수정한다.
- 구현 목표에서는 계획·탐색·문서·handoff만으로 완료 선언하지 않는다.
- focused 검사 뒤 필요한 전체 빌드를 실행한다.
- 결과를 실행 기록, rules/dev-context.md, rules/dev-progress.md, roadmap checklist에 남긴다.
```
