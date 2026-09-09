# M5 실시간 돋보기 차단·핸드오프

날짜: 2026-09-09

## 현재 목표

선택한 보이는 화면 영역을 확대해서 보며, 미리보기에서 지속 조작한 Down → Move → Up을 같은 물리 화면 좌표의 아래 앱으로 실시간 전달한다. A/B 한 번 직선 획은 이 기능의 보조 기능이다.

## DONE

- 미리보기는 캡처에서 제외되며, A/B 표식은 클릭 당시 정규화 좌표로 고정되고 두 점의 직선 계획을 보인다. 사용자가 표식 일치를 확인했다.
- App은 A/B 실행 직전에 미리보기를 숨겨 전역 입력이 자기 창을 받지 않게 한다.
- 입력 허용 상태는 영역 선택을 거쳐도 유지하고, 일반 미리보기 클릭은 실제 입력을 보내지 않으며 A/B 지정과 실행 버튼만 입력 경로로 남겼다.
- `dotnet build Magnifier.slnx --nologo`: 경고 0, 오류 0. `dotnet test Magnifier.slnx --nologo`: Core 7개 통과.

## BLOCKED

사용자 확인: 실제 돋보기처럼 확대된 화면을 계속 조작하는 기능은 동작하지 않는다. 현재 App은 A/B 한 획만 보장하며, 일반 미리보기 클릭을 실시간 전역 입력으로 전달하지 않는다.

이전 일반 클릭 경로는 미리보기 자신이 전역 입력을 가로채거나 입력 실패 뒤 safety gate가 꺼지는 문제를 만들었다. 이를 막기 위해 경로를 제거했으므로, 이번 결과를 실시간 조작 성공으로 기록하지 않는다.

## 결정 사항

- 입력 대상은 특정 창이 아니라 선택한 `ScreenRegion` 아래 현재 보이는 화면이다. 브라우저와 Blender는 호환성 검증 표면일 뿐이다.
- 실시간 조작은 A/B 지정과 별도 명시 모드로 설계한다. 입력 표면, 실제 전역 전달, 미리보기 겹침, mouse capture, Esc release를 한 설계에서 다룬다.
- `notes/transfers/`는 외부 핸드오프 자료다. 이 인계는 그 경로가 아닌 현재 run 문서에만 남긴다.

## 핵심 파일 경로

- `C:\ai\projects\magnifier\src\Magnifier.App\SelectionPreviewWindow.xaml`
- `C:\ai\projects\magnifier\src\Magnifier.App\SelectionPreviewWindow.xaml.cs`
- `C:\ai\projects\magnifier\src\Magnifier.App\MainWindow.xaml.cs`
- `C:\ai\projects\magnifier\src\Magnifier.Core\PointerInputSession.cs`
- `C:\ai\projects\magnifier\src\Magnifier.Infrastructure\WindowsPointerInput.cs`
- `C:\ai\projects\magnifier\notes\plans\2026-09-08-magnifier-m5.md`

## 다음 단계

1. 브라우저 또는 Blender에서 현재 실시간 조작 실패를 한 번 재현하고, 미리보기 입력과 전역 `SendInput` 수신자가 충돌하는 정확한 시점을 관찰한다.
2. 확대 조작 표면을 유지하면서 실제 화면으로 지속 입력을 전달할 구조를 설계한다. 대상 창 제한·권한 상승·앱별 분기는 제외한다.
3. 확인된 차단 지점에만 App/Core/Infrastructure를 수정하고, build/test 뒤 브라우저와 Blender에서 짧은 실시간 드래그와 Esc release를 수동 확인한다.

## 다음 세션 프롬프트

```text
C:\ai\projects\magnifier 에서 이어서 작업해줘.

먼저 확인:
1. cwd가 C:\ai\projects\magnifier 인지 확인한다.
2. git status를 읽고 기존 변경을 되돌리지 않는다. notes\transfers\는 외부 자료이므로 건드리거나 스테이징하지 않는다.
3. 구현 전 현재 diff를 다시 확인한다.
4. 현재 목표는 A/B 한 획이 아니라 실제 돋보기의 실시간 지속 조작이다. 계획·문서만으로 완료 선언하지 않는다.

먼저 읽을 문서:
- C:\ai\projects\magnifier\CLAUDE.md
- C:\ai\projects\magnifier\doc\INTENT.md
- C:\ai\projects\magnifier\rules\dev-context.md
- C:\ai\projects\magnifier\rules\dev-progress.md
- C:\ai\projects\magnifier\rules\dev-roadmap.md
- C:\ai\projects\magnifier\notes\plans\2026-09-08-magnifier-m5.md
- C:\ai\projects\magnifier\notes\runs\2026-09-09-m5-realtime-magnifier-handoff.md

검증된 읽을 파일:
- C:\ai\projects\magnifier\src\Magnifier.App\SelectionPreviewWindow.xaml
- C:\ai\projects\magnifier\src\Magnifier.App\SelectionPreviewWindow.xaml.cs
- C:\ai\projects\magnifier\src\Magnifier.App\MainWindow.xaml.cs
- C:\ai\projects\magnifier\src\Magnifier.Core\PointerInputSession.cs
- C:\ai\projects\magnifier\src\Magnifier.Infrastructure\WindowsPointerInput.cs

최근 완료:
- A/B 표식·직선 계획·A→B 전달 중 미리보기 비간섭·입력 허용 유지 — build 경고/오류 0, Core 테스트 7개 통과. 실시간 돋보기 조작은 미완료.

다음 목표:
- 확대된 미리보기에서 지속 Down → Move → Up을 선택한 보이는 화면 아래 앱으로 실시간 전달하고, 브라우저와 Blender에서 실제 짧은 드래그·Esc release를 확인한다.

스코프 모드:
- scope_mode: delivery
- answer_shape: roadmap-first
- active_constraints: .NET 9 WPF; 범용 Windows 오버레이; 대상 창 제한·앱별 제어·권한 상승·자유 경로 재생 제외; 실제 입력은 Esc와 release를 보장한다.
- stale_constraints: A/B 한 획만으로 M5를 완료 처리하는 흐름.

작업 방식:
- 미리보기 입력과 전역 SendInput 충돌을 먼저 재현·관찰한다.
- 확인된 BLOCKER만 src\Magnifier.App·src\Magnifier.Core·src\Magnifier.Infrastructure에서 수정한다.
- 표준 출력 경로 build/test와 실제 브라우저·Blender 수동 검증 뒤에만 결과를 run report, rules/dev-context.md, rules/dev-progress.md, roadmap에 남긴다.
```

## 변경/커밋

- 현재 run의 App·계획·문서·목업 변경을 하나의 커밋으로 닫는다. `notes/transfers/`는 커밋 대상에서 제외한다.
- 재사용 후보: `skip` — 이번 결과는 프로젝트 고유의 UI 차단·인계 내용이다.
