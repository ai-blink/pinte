# 재개 컨텍스트

- delivery workspace: `C:/ai/projects/magnifier`, branch `main`. 두 창 구현·테스트·문서는 `6b5c0df`에 커밋했다. 이후 R0 재검증과 수정은 최신 run을 따른다. 푸시는 요청 범위 밖이다. `notes/transfers/`는 외부 자료이므로 수정·삭제·스테이징하지 않는다.
- 사용자 확정 요구: 마우스만으로 정밀 조작·복귀. 실제 원본 테두리의 이동/크기 조절로 캡처 영역을 정하고, 확대 렌즈는 독립 이동한다. 직접 Down→연속 Move→Up이 주력, A/B는 보조다. 고정 작업창+개요·Esc 중심 복귀·목업만 수정은 이전 단계다.
- 최신 상태: **R0 native 프로브 3개 통과, 표준 앱 두 창 표시 사용자 확인. M5 실사용 수락 NEEDS_USER_UI_CHECK**. 프로브·문서·목업·A/B 실행·API 반환만으로 제품 완료 처리하지 않는다.
- 구현: SelectionOverlayWindow의 투명 내부/8개 손잡이, SelectionPreviewWindow의 독립 제목·고정 중지/원래 화면·배율·가상 포인터. Main은 캡처·진입/복귀·배치 저장. 재진입 입력은 꺼짐이다.
- 엔진: WindowsLivePointerRelay 전용 WH_MOUSE_LL 스레드 + 자기 태그 SendInput. hook 안 동기 주입의 중복 Down/Up을 재현해 실제 전달·창 스타일 변경을 hook 반환 뒤 FIFO 큐로 옮겼다. 분리/겹침 곡선·왕복, 경계 마지막 위치 Up·재무장 차단을 실제 native 수신으로 확인했다. 합성 절대 입력 프로브 결과이며 실제 상대 장치 검증은 남았다. UI는 Win32 입력/캡처를 직접 호출하지 않는다.
- 검증: 최종 `dotnet build Magnifier.slnx --nologo` 경고 0/오류 0, `dotnet test Magnifier.slnx --nologo` Core 23개 통과. 좌표 독립·음수/비정수 배율·저장 왕복·입력 기본 off·경계 release·실패/재시작을 검증했다.
- 표준 실행본: `C:/ai/projects/magnifier/src/Magnifier.App/bin/Debug/net9.0-windows/Magnifier.App.exe`. 원본 main에서 재빌드·실행했다. 메인 Hide가 보조 창도 숨기는 Owner 관계를 제거했다. 사용자가 두 창 모두 보임을 확인했다.
- UI 상태: 이전 및 재검사 외부 이동에 사용자가 직접 조작했다고 답해 이후 재개했고 프로브가 통과했다. 제품 보조 창은 자동화 목록에 반환되지 않아 사용자 확인으로 검증한다. 마커 페이지를 준비했으며 실제 렌즈 드래그·복귀 결과는 대기 중이다. 사용자 조작 중에는 자동 입력을 보내지 않는다.
- 다음: Chrome `http://127.0.0.1:18764/pointer-target.html`의 실제 마커 수신·복귀 결과 확인 → 표준 앱에서 원본/렌즈 독립 이동·크기·실제 장치·오류/닫기 해제 확인. 프로브는 제품 실행본 검증의 대체가 아니다.
- 남은 범위: 실제 상대 마우스·보조 절대 입력·모니터 끝 탈출, 재귀/겹침, 혼합 DPI/음수 좌표/다중 모니터, 브라우저 마커/Blender/Windows 앱. 렌즈를 놓을 때 화면 안 보정, 모니터 변경 시 입력 해제·진입 창 복원을 구현했으며 R2 실측 대상이다. 우클릭·스크롤 등 R3는 핵심 검증 뒤 진행한다.
- 최신 run: `notes/runs/2026-09-10-r0-relay-retest.md`. 최초 구현: `notes/runs/2026-09-10-two-window-implementation.md`. UX: `notes/brainstorm/2026-09-10_magnifier-redesign_design.md`, 계획: `notes/plans/2026-09-08-magnifier-m5.md`. 기존 목업·조사·과거 관찰은 보존했다.
