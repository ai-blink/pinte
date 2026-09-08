# M3 입력 gate·release 경로 실행 기록

## 결과

- 상태: `DONE`
- 완료선: 기본 비활성 input gate와 취소·오류에도 release를 시도하는 단일 미리보기 드래그 세션을 만든다.
- 수락 증거: 솔루션 build 경고·오류 0, Core 테스트 7개 통과, 사용자가 checkbox 기본 해제와 비활성 드래그 안내의 지속을 확인했다 (2026-09-08).

## 범위와 변경

- `src/Magnifier.Core/IPointerInput.cs`, `PointerInputSession.cs`: 기본 비활성 gate, Down·Move·Up, 취소와 오류의 단일 release 계약을 추가했다.
- `src/Magnifier.Infrastructure/WindowsPointerInput.cs`: 물리 화면 픽셀을 가상 데스크톱 절대 좌표로 정규화하는 `SendInput` 구현을 추가했다.
- `src/Magnifier.App/*`: checkbox 기본 해제, 미리보기 드래그·WPF mouse capture·Esc·창 닫기·새 선택·캡처 실패의 세션 취소를 연결했다.
- `src/Magnifier.Core.Tests/PointerInputSessionTests.cs`: gate 기본값, 정상 순서, gate 해제, Move 오류 release를 검증했다.
- `docs/diagrams/input-session.md`: 기본 비활성·Pressed·release 전이를 기록했다.

## 검증

- `dotnet build Magnifier.slnx --nologo` — 통과, 경고 0, 오류 0.
- `dotnet test Magnifier.slnx --nologo` — 통과, 7개 통과·0개 실패.
- `git diff --check` — 통과.
- 사용자 수동 확인 — checkbox는 기본 해제이고, 비활성 상태 미리보기 드래그 안내가 실시간 프레임 갱신 뒤에도 유지됨을 확인했다.
- secret scan — 변경 문서에서 통과.
- 로드맵 형태 검사 — 자동 validator의 `harness` 모듈은 이 환경에 없어 실행하지 못했다. 로드맵은 6개 비어 있지 않은 줄, milestone 수준, 중첩 체크리스트 없음으로 수동 확인했다.

## 상태와 후속

- `NEEDS_USER_UI_CHECK`: checkbox를 켠 실제 대상 앱 드래그는 전역 포인터를 조작한다. 대상 앱과 권한 상태를 정한 명시적 사용자 승인 전에는 실행하지 않았다.
- `FOLLOW_UP`: M4에서 길게 누름 수치 제어와 A→B 단일 직선 획을 같은 세션 위에 추가한다.
- `IGNORE_FOR_NOW`: 자유 경로 재생, 대상 창 자동 식별, 권한 상승, 브러시·레이어 제어.
- 재사용 후보: `skip`. 이번 결과는 Magnifier의 입력 안전 정책과 Windows 구현에 한정된다.

## 다음 세션 프롬프트

```text
C:\ai\projects\magnifier 에서 이어서 작업해줘.

먼저 확인:
1. cwd가 C:\ai\projects\magnifier 인지 확인한다.
2. git status를 읽고 기존 변경을 되돌리지 않는다. notes\transfers\는 외부 핸드오프 자료이므로 건드리지 않는다.
3. 새 변경이 있는지 구현 전 diff를 다시 확인한다. 새 변경이 없다면 스테이징·커밋하지 않는다.
4. M4의 실제 변경 대상은 src\Magnifier.App·src\Magnifier.Core이며, `SendInput` 변경은 M4 완료선에 필요할 때만 src\Magnifier.Infrastructure에서 한다.

먼저 읽을 문서:
- C:\ai\projects\magnifier\CLAUDE.md
- C:\ai\projects\magnifier\doc\INTENT.md
- C:\ai\projects\magnifier\rules\dev-context.md
- C:\ai\projects\magnifier\rules\dev-progress.md
- C:\ai\projects\magnifier\rules\dev-roadmap.md
- C:\ai\projects\magnifier\notes\runs\2026-09-08-m3-input-gate.md
- C:\ai\projects\magnifier\notes\plans\2026-09-08-magnifier-m3-input-gate.md

검증된 읽을 파일:
- C:\ai\projects\magnifier\Magnifier.slnx
- C:\ai\projects\magnifier\src\Magnifier.App\MainWindow.xaml.cs
- C:\ai\projects\magnifier\src\Magnifier.App\SelectionPreviewWindow.xaml.cs
- C:\ai\projects\magnifier\src\Magnifier.Core\PointerInputSession.cs
- C:\ai\projects\magnifier\src\Magnifier.Infrastructure\WindowsPointerInput.cs
- C:\ai\projects\magnifier\docs\diagrams\input-session.md

없는 파일 / 구현 후보:
- C:\ai\projects\magnifier\notes\plans\2026-09-08-magnifier-m4.md — 현재 없음. M4 완료선을 정한 뒤 생성한다.

최근 완료:
- M3 기본 비활성 input gate와 release 경로 — build 경고/오류 0, Core 테스트 7개, 사용자 비활성 UI 확인 통과 (2026-09-08).

다음 목표:
- M4: 길게 누름 수치 제어와 A→B 단일 직선 획을 계획·구현한다. 실제 활성 입력 확인은 별도 `NEEDS_USER_UI_CHECK`로 유지한다.

스코프 모드:
- scope_mode: delivery
- answer_shape: roadmap-first
- active_constraints: .NET 9 WPF만 사용; 범용 Windows 오버레이; 실제 입력은 기본 비활성; 자유 경로 재생과 대상 앱 브러시·레이어·색상 제어는 제외.
- stale_constraints: M3의 기본 비활성 UI 확인 대기 상태는 종료됐다.

작업 방식:
- M4 완료선·수락 확인·scope limit·stop rule을 먼저 고정한다.
- BLOCKER만 현재 run에서 수정한다.
- 구현 목표에서는 계획·탐색·문서·handoff만으로 완료 선언하지 않는다.
- focused tests 후 필요한 전체 tests 실행한다.
- 결과를 실행 기록, rules/dev-context.md, rules/dev-progress.md, roadmap checklist에 남긴다.
```
