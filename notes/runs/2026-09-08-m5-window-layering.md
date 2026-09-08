# M5 창 계층·크기 변경 차단 이력

## 결과

- 상태: `PARTIAL_PASS`
- 관찰: 사용자가 미리보기 창 크기를 바꾸면 캡처 화면이 사라지거나 잘린다고 보고했고, 선택 오버레이와 미리보기 창 모두의 항상 위 해제를 요청했다.

## 원인과 수정

- 선택 영역은 최대 10fps로 화면을 다시 캡처한다. 미리보기 창이 `Topmost`이고 매 갱신마다 `Activate()`되면, 선택 영역과 겹친 미리보기 자신을 재캡처할 수 있다.
- `SelectionPreviewWindow`와 `SelectionOverlayWindow`의 `Topmost`를 제거했다.
- `MainWindow.ShowPreview()`의 반복 `Activate()` 호출을 제거했다.
- `new-alt`는 같은 GDI 데스크톱 캡처 위에 캡처 제외·비활성 오버레이를 올린다. Magnifier에는 항상 위를 되살리지 않고, 새 미리보기를 선택 영역 밖에 먼저 배치하는 정책을 적용했다.
- 미리보기 창이 선택 영역과 겹치면 마지막 정상 프레임을 유지하고 갱신만 보류한다. 창을 다시 밖으로 옮기면 다음 tick에서 갱신이 재개된다.

## 검증

- `dotnet build Magnifier.slnx --nologo` — 통과, 경고 0, 오류 0.
- `dotnet test Magnifier.slnx --nologo` — 통과, 7개 통과·0개 실패.
- 수정된 표준 실행본 `src/Magnifier.App/bin/Debug/net9.0-windows/Magnifier.App.exe` 실행을 확인했다.
- 사용자 수동 확인 (2026-09-09) — Blender 5.2 미리보기가 다시 정상 표시됨을 확인했다.

## 남은 확인

- 미리보기를 원본 영역과 겹치면 갱신 보류 안내와 마지막 정상 프레임이 남고, 다시 밖으로 옮기면 갱신이 재개되는지 확인한다.
- checkbox를 켠 실제 브라우저·Blender 짧은 드래그와 A→B 획은 사용자 승인 뒤에 확인한다.
