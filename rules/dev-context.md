# 재개 컨텍스트

- delivery workspace: `C:/ai/projects/magnifier`, branch `main`. 후속 사용자 요청 「문서 갱신 및 커밋」에 따라 이번 구현·테스트·문서를 단일 커밋으로 보존한다. 푸시는 요청 범위 밖이다. `notes/transfers/`는 외부 자료이므로 수정·삭제·스테이징하지 않는다.
- 사용자 확정 요구: 마우스만으로 정밀 조작·복귀. 실제 원본 테두리의 이동/크기 조절로 캡처 영역을 정하고, 확대 렌즈는 독립 이동한다. 직접 Down→연속 Move→Up이 주력, A/B는 보조다. 고정 작업창+개요·Esc 중심 복귀·목업만 수정은 이전 단계다.
- 최신 상태: **두 창·직접 입력 중계 후보 구현, M5/R0 수락 BLOCKED / NEEDS_USER_UI_CHECK**. 문서·목업·A/B 실행·API 반환만으로 완료 처리하지 않는다.
- 구현: SelectionOverlayWindow의 투명 내부/8개 손잡이, SelectionPreviewWindow의 독립 제목·고정 중지/원래 화면·배율·가상 포인터. Main은 캡처·진입/복귀·배치 저장. 재진입 입력은 꺼짐이다.
- 엔진: WindowsLivePointerRelay 전용 WH_MOUSE_LL 스레드 + 자기 태그 SendInput. 원본/렌즈 물리 좌표 독립, 중계 중 두 layered 창 입력 통과, 경계 Up 후 복원, 물리 해제까지 재무장 차단. Up 실패는 상태를 남겨 재시도하며 StopAsync 실패를 UI에 전달한다. UI는 Win32 입력/캡처를 직접 호출하지 않는다.
- 검증: 최종 `dotnet build Magnifier.slnx --nologo` 경고 0/오류 0, `dotnet test Magnifier.slnx --nologo` Core 23개 통과. 좌표 독립·음수/비정수 배율·저장 왕복·입력 기본 off·경계 release·실패/재시작을 검증했다.
- 표준 실행본: `C:/ai/projects/magnifier/src/Magnifier.App/bin/Debug/net9.0-windows/Magnifier.App.exe`. 시작 때 앱 프로세스는 없었다. UI 충돌 감지 뒤 표준 앱을 새로 실행하거나 조작하지 않았다.
- UI 차단: 별도 native probe가 target Down `(288,230)`을 관측한 뒤 외부 입력을 감지해 중단. Move/Down `(1017,302)` 원인과 실제 최종 Up은 미확정이다. 사용자에게 그 시각 마우스/창 조작 여부를 질문했다. **답변 전 UI 자동 재시도 금지.**
- 다음: 사용자 답변 확인 → `scripts/probes/RelayProbe.cs`의 첫 충돌 flags/tag/직전 명령 진단으로 R0 재개 → 표준 앱에서 원본/렌즈 독립 이동·곡선/왕복·경계 해제·마우스 복귀 확인. 프로브는 제품 실행본 검증의 대체가 아니다.
- 남은 범위: 실제 상대 마우스·보조 절대 입력·모니터 끝 탈출, 재귀/겹침, 혼합 DPI/음수 좌표/다중 모니터, 브라우저 마커/Blender/Windows 앱. 렌즈를 놓을 때 화면 안 보정, 모니터 변경 시 입력 해제·진입 창 복원을 구현했으며 R2 실측 대상이다. 우클릭·스크롤 등 R3는 핵심 검증 뒤 진행한다.
- 최신 run: `notes/runs/2026-09-10-two-window-implementation.md`. UX: `notes/brainstorm/2026-09-10_magnifier-redesign_design.md`, 계획: `notes/plans/2026-09-08-magnifier-m5.md`. 기존 목업·조사 자료와 `notes/runs/2026-09-09-m5-realtime-magnifier-handoff.md`의 과거 관찰은 보존했다.
