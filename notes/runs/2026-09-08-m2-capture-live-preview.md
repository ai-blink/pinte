# M2 캡처·M2.1 실시간 미리보기 실행 기록

## 결과

- 상태: `DONE`
- 완료선: 선택 영역을 물리 화면 픽셀로 캡처하고, 같은 영역을 비중첩 백그라운드 캡처로 주기 갱신해 미리보기에 표시한다.
- 수락 증거: 솔루션 build 경고·오류 0, Core 테스트 3개 통과, 사용자가 실제 화면 캡처와 실시간 변화 반영을 확인했다 (2026-09-08).

## 범위와 변경

- `src/Magnifier.Core/*`: 물리 픽셀 `ScreenRegion`, BGRA32 `CapturedFrame`, 캡처 계약과 미리보기 좌표 환산을 추가했다.
- `src/Magnifier.Infrastructure/WindowsScreenCapture.cs`: GDI BitBlt 기반 Windows 화면 캡처를 `IScreenCapture`로 구현했다.
- `src/Magnifier.App/*`: WPF 선택 결과를 화면 픽셀로 변환하고, 미리보기 수명과 최대 10fps 비중첩 갱신을 소유한다.
- `src/Magnifier.Core.Tests/*`: 좌표 환산·범위 제한·프레임 불변 조건 테스트 3개를 추가했다.
- `docs/diagrams/capture-flow.md`: App·Core·Infrastructure 캡처 경계와 실시간 갱신 루프를 기록했다.

## 검증

- `dotnet build Magnifier.slnx --nologo` — 통과, 경고 0, 오류 0.
- `dotnet test Magnifier.slnx --nologo` — 통과, 3개 통과·0개 실패.
- `git diff --check` — 통과.
- 사용자 수동 확인 — 실제 화면 캡처 성공과 선택 영역의 화면 변화가 미리보기에 반영됨을 확인했다.
- secret scan — 변경 문서에서 통과.
- 로드맵 형태 검사 — 자동 validator의 `harness` 모듈은 이 환경에 없어 실행하지 못했다. 로드맵은 6개 비어 있지 않은 줄, milestone 수준, 중첩 체크리스트 없음으로 수동 확인했다.

## 제외 범위와 후속

- `FOLLOW_UP`: M3에서 실제 입력을 기본 비활성으로 둔 gate와 단일 release 계약을 설계·구현한다.
- `IGNORE_FOR_NOW`: 연속 입력, 자유 경로 재생, 프레임 파일 저장, 대상 앱별 브러시·레이어 제어.
- 재사용 후보: `skip`. 이번 결과는 Magnifier의 Windows 화면 캡처 수명에 한정된다.

## 다음 세션 프롬프트

```text
C:\ai\projects\magnifier 에서 이어서 작업해줘.

먼저 확인:
1. cwd가 C:\ai\projects\magnifier 인지 확인한다.
2. git status를 읽고 기존 변경을 되돌리지 않는다.
3. 커밋 후 새 변경이 있는지 구현 전 diff를 다시 확인한다.
4. M3의 실제 변경 대상은 입력 계약이 필요해질 때의 src\Magnifier.Core·src\Magnifier.Infrastructure와 입력 gate를 노출할 src\Magnifier.App이다.

먼저 읽을 문서:
- C:\ai\projects\magnifier\CLAUDE.md
- C:\ai\projects\magnifier\doc\INTENT.md
- C:\ai\projects\magnifier\rules\dev-context.md
- C:\ai\projects\magnifier\rules\dev-progress.md
- C:\ai\projects\magnifier\rules\dev-roadmap.md
- C:\ai\projects\magnifier\notes\runs\2026-09-08-m2-capture-live-preview.md

검증된 읽을 파일:
- C:\ai\projects\magnifier\Magnifier.slnx
- C:\ai\projects\magnifier\src\Magnifier.App\MainWindow.xaml.cs
- C:\ai\projects\magnifier\src\Magnifier.Core\IScreenCapture.cs
- C:\ai\projects\magnifier\src\Magnifier.Infrastructure\WindowsScreenCapture.cs

없는 파일 / 구현 후보:
- C:\ai\projects\magnifier\docs\diagrams\input-session.md — 현재 없음. M3 입력 세션 상태 전이가 구현될 때 생성한다.

최근 완료:
- M2 캡처·좌표 변환과 M2.1 실시간 미리보기 — build 경고/오류 0, Core 테스트 3개, 사용자 수동 확인 통과 (2026-09-08).

다음 목표:
- M3: 실제 입력 기본 비활성을 지키는 input gate와 취소·창 닫기·capture 손실의 단일 release 계약을 구현한다.

스코프 모드:
- scope_mode: delivery
- answer_shape: roadmap-first
- active_constraints: .NET 9 WPF만 사용; 범용 Windows 오버레이; 실제 입력은 기본 비활성; 자유 경로 재생과 대상 앱 브러시·레이어·색상 제어는 제외.
- stale_constraints: M2.1의 실시간 미리보기 사용자 수동 확인 대기 상태는 종료됐다.

작업 방식:
- 현재 diff와 테스트 상태를 먼저 확인한다.
- BLOCKER만 현재 run에서 수정한다.
- 구현 목표에서는 계획·탐색·문서·handoff만으로 완료 선언하지 않는다.
- focused 검사 뒤 필요한 전체 tests 실행한다.
- 결과를 실행 기록, rules/dev-context.md, rules/dev-progress.md, roadmap checklist에 남긴다.
```
