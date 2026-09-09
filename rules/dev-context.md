# 재개 컨텍스트

- 브랜치: `main`.
- 현재 상태: M5는 `BLOCKED`다. 최신 App은 A/B 표식·한 획·입력 허용 유지까지 build/test했지만, 사용자가 원하는 확대 화면의 실시간 지속 조작은 동작하지 않는다. A/B 전용 흐름을 실제 돋보기 완료로 취급하지 않는다.
- 관찰된 차단: 미리보기 클릭을 실시간 입력으로 쓰면 자신의 창이 전역 입력을 가로채거나 실패 경로에서 gate가 풀린다. 현재 App은 이를 막기 위해 A/B 지정과 `실제 A→B 실행`만 입력 경로로 남겼다.
- 다음 목표: `notes/plans/2026-09-08-magnifier-m5.md`의 실시간 조작 모드를 별도 설계한다. 확대 좌표를 계속 받으면서 아래 보이는 화면에 Down → Move → Up을 전달하고, 미리보기·capture·Esc release가 충돌하지 않는 구조를 브라우저와 Blender에서 확인한다.
- 최근 증거: 최신 표준 실행본 `dotnet build Magnifier.slnx --nologo` 경고·오류 0, `dotnet test Magnifier.slnx --nologo` Core 테스트 7개 통과. 사용자 관찰은 `notes/runs/2026-09-09-m5-realtime-magnifier-handoff.md`에 기록한다.
- 주의: `notes/transfers/`는 외부 자료이므로 수정·스테이징하지 않는다. 현재 실행 중인 `Magnifier.App.exe`를 닫기 전에는 표준 경로 build를 다시 실행하지 않는다.
