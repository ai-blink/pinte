# 드래그 해제 뒤 조작 요청 취소 경로 수정

- workspace `C:/ai/projects/magnifier`, branch `main`, 기준 HEAD `725cd05`.
- 사용자: “잘되는데 드래그 하면 입력 재개 버튼으로 전환되”. 추가 확인: **“드래그하다 버튼을 놓은 직후”**.
- 후속 판정: **사용자 문제 미해결**. 아래 큐 경합 수정·회귀 테스트는 통과했지만 누름 중 capture 중지가 재발했고, 후속 감시 범위 수정도 실패했다. 최종 상태는 [인계 문서](2026-09-11-drag-blocked-handoff.md)를 따른다.

## 확인한 경로와 한계

정상 ProcessMouse Up은 Complete → Pause → TryResume으로 조작 요청을 유지한다. 그러나 hook이 관찰한 Up이 FIFO에 남은 상태에서 timer의 capture 감시가 먼저 실행되면, IsPressed는 아직 true다. 이때 이전 capture와 다른 값이 보이면 StopInternal이 요청을 취소한다. 나중에 Up을 처리해도 요청은 복원되지 않는다.

기존 메시지별 처리 순서를 추출한 회귀 테스트에서 이 조건과 Up 실패 처리 순서의 테스트 2개가 실패했다. 이는 조건부 상태 불일치의 재현이다. 실제 사용자 환경에서 이 순서가 발생했다는 로그는 없으므로 현상의 유일한 원인으로 확정하지 않는다. Windows는 보통 posted message를 timer보다 먼저 처리하며, GetGUIThreadInfo(0)는 현재 foreground thread를 조회한다. activation 전환 중 조회값이나 실제 입력 API 실패도 추가 후보다.

## 구현

- Infrastructure의 `RelayCommandPump`가 타이머 검사 전에 이미 관찰한 입력·명령을 FIFO 순서로 처리한다. native GetMessage가 반환하는 동안 hook이 큐에 넣은 Up도 먼저 완료한다.
- pending 왼쪽 Up을 별도 개수로 관리한다. 빠른 다음 Down이나 다른 버튼이 앞선 Up 관찰을 지우지 않는다. 콜백 완료·예외 모두 finally에서 개수를 정리한다.
- capture 조회 중 hook이 Up을 관찰한 경우에도 pending Up 완료까지 capture 손실 중지를 보류한다. 실제 누름 중 capture 손실은 기존처럼 해제 후 요청을 끈다.
- `WindowsLivePointerRelay.Diagnostics`는 요청 취소·입력 예외 사유와 상태/MVID를 `%LOCALAPPDATA%/Magnifier/diagnostics/relay-stop-<PID>.jsonl`에 비동기로 기록한다. 로그 실패는 입력 상태를 변경하지 않는다. 종료 직전 프로세스가 끝나면 기록이 남지 않을 수 있는 best-effort 진단이다.
- `Magnifier.Infrastructure.Tests`를 솔루션에 추가했다. native hook/실제 입력 없이 큐와 실제 Core 상태 전이를 검사한다.

## 검증

| 검사 | 결과 |
|---|---|
| 수정 전 회귀 | 4개 중 2개 실패: pending Up보다 timer 우선, 실패 Up 처리 지연 |
| 수정 후 Core | 34개 통과 |
| 수정 후 Infrastructure | 4개 통과: 정상 Up 요청 유지, 실제 capture 손실 중지, Up→다음 Down FIFO, Up 실패 해제 잔류 |
| dotnet build Magnifier.slnx --nologo | 경고 0, 오류 0 |
| dotnet test Magnifier.slnx --nologo | 전체 38개 통과, 실패·건너뜀 0 |
| 프로브 build / --describe | build 0/0, MVID 확인만 실행. UI 입력 없음 |

최종 engine MVID: `0dbeda53-3808-48bd-a240-aa790243cb90`.

표준 실행본: `C:/ai/projects/magnifier/src/Magnifier.App/bin/Debug/net9.0-windows/Magnifier.App.exe`.
사용자 진입 파일: `C:/ai/projects/magnifier/Start-Magnifier-Admin.cmd`.

수정 전에 기존 앱 프로세스가 없는 것을 확인했다. 사용자 앱을 종료하거나 자동 UI 검사를 실행하지 않았다. UIAccess 실험 설치본은 이번 일반 앱 수정과 별도다. 독립 읽기 검토에서 FIFO/pending Up 변경의 BLOCKER는 발견되지 않았다. 검토가 지적한 입력 실패 로그 누락은 failed 인수로 보완한 뒤 최종 빌드·38개 테스트를 다시 통과했다.

사용자는 같은 대상에서 짧은 드래그를 놓은 뒤 재개 버튼을 누르지 않고 다음 드래그가 되는지 확인한다. 동일 현상이 남으면 새 relay-stop 로그의 Reason과 MVID를 먼저 확인한다. 실제 대상 반응·노란 포인터 문제·혼합 DPI/앱 호환 완료를 이번 테스트로 대신 판정하지 않는다.

기존 변경과 외부 자료는 보존했으며 스테이징·커밋·푸시는 하지 않았다.

공식 근거: [GetGUIThreadInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getguithreadinfo), [GetMessage 메시지 우선순위](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getmessagew).
