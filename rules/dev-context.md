# 재개 컨텍스트

- delivery workspace: `C:/ai/projects/magnifier`, branch `main`. 두 창 구현은 `6b5c0df`, 입력 중복·창 숨김 수정은 `725cd05`다. 이후 구현·진단·검증 구성과 이번 인계 문서를 사용자 요청으로 함께 커밋한다. 이 문서를 포함한 커밋이 인계 기준이며 푸시는 하지 않는다. `notes/transfers/`는 수정·삭제·스테이징 금지다.
- 사용자 확정 요구: 마우스만으로 정밀 조작·복귀. 실제 원본 테두리의 이동/크기 조절로 캡처 영역을 정하고, 확대 렌즈는 독립 이동한다. 직접 Down→연속 Move→Up이 주력, A/B는 보조다. 고정 작업창+개요·Esc 중심 복귀·목업만 수정은 이전 단계다.
- 최신 상태: **USER_REPORTED_FAIL / BLOCKED_REPEAT_DRAG_RELEASE**. 두 차례 수정 뒤에도 드래그가 풀린다는 사용자 확인과 최신 실행 로그가 있다. 드래그 중 이중 포인터도 BLOCKED다. 과거 native 6개·현재 단위 테스트 44개 통과는 실사용 수락이 아니며 M5 완료를 선언하지 않는다.
- 최신 요구: “항상 조작 시작된 상태이어야해 근데 자꾸 풀려”. 확대 열기 자체가 조작 요청이다. 창·영역·배율·경계·화면 갱신 일시 정지는 요청을 유지하고 버튼 해제·최신 화면 뒤 재개한다. 명시적 중지·Esc·복귀·닫기·대상 capture 손실·입력/해제 실패는 자동 재개하지 않는다. 이전의 별도 조작 시작·재진입 보기 기본값은 D-020으로 대체했다.
- 구현: SelectionOverlayWindow의 투명 내부/8개 손잡이, SelectionPreviewWindow의 독립 제목·고정 중지/원래 화면·배율·가상 포인터. Main은 캡처·진입/복귀·배치 저장을 관리한다. 설정에 입력 허용은 저장하지 않으며, 재진입할 때 새 조작 요청을 만든다.
- 엔진: WindowsLivePointerRelay 전용 WH_MOUSE_LL 스레드 + 자기 태그 SendInput. hook 안 동기 주입의 중복 Down/Up을 재현해 실제 전달·창 스타일 변경을 hook 반환 뒤 FIFO 큐로 옮겼다. 분리/겹침 곡선·왕복, 경계 마지막 위치 Up·재무장 차단을 실제 native 수신으로 확인했다. 합성 절대 입력 프로브 결과이며 실제 상대 장치 검증은 남았다. UI는 Win32 입력/캡처를 직접 호출하지 않는다.
- 앞선 검증: hover 커서 수정까지 표준 build 경고 0/오류 0, 솔루션 test Core 34개 통과. native 6개에 클릭 전·Up 후 실제 커서 좌표 일치를 추가해 통과했다. 별도 `RelayProbe.exe --cursor-diagnostic`은 진단 완료지만 `Fixed=false`다. 당시 engine MVID는 `7657f0fb-a4ae-4f37-9c3c-d5d530956387`.
- 표준 실행본: `C:/ai/projects/magnifier/src/Magnifier.App/bin/Debug/net9.0-windows/Magnifier.App.exe`. 원본 main에서 재빌드·실행했다. 메인 Hide가 보조 창도 숨기는 Owner 관계를 제거했다. 사용자가 두 창 모두 보임을 확인했다.
- UI 상태: 손잡이 누름을 중계 해제 대기로 오인해 Thumb를 비활성화하던 경로를 수정했다. 초기 배치를 내리고 `두 창 아래쪽에 배치`를 추가했다. 이후 일시 정지와 요청 취소를 분리하고 조작 유지 상태를 표시한다. 실제 누름·해제 대기 잠금은 유지한다.
- 사용자 확인: **“두 창 모두 아래에 있고 이동됨”**, 조작 유지 수정 후 **“조작이 유지되고 클릭·드래그됨”**. 두 번째 답변은 별도 재개 없이 렌즈 제목부를 옮긴 뒤 조작하는 흐름에 대한 확인이다. 대상 앱 이름·곡선/왕복 경로·정밀 좌표·복귀까지 확인한 것으로 확대하지 않는다. 보조 창은 자동화 목록에 반환되지 않아 사용자 확인으로 검증한다.
- 최신 피드백: “노란색 가상 포인터 갑자기 왜 생김 실제 마우스 좌표 하고도 다름”. 기존 엔진은 hover부터 실제 커서를 원본으로 옮겼다. 이제 Down에서만 중계를 시작하고 정상 Up 직후 커서를 렌즈로 복원한다. 조작 요청은 유지하며 가상 표시는 누름 중에만 남는다. 클릭 전·Up 후 실제 앱 표시 확인은 대기 중이다.
- 확인된 차단: 드래그 중 렌즈 `(1209,451)`와 실제 커서 `(392,318)` 불일치. 복귀 SendInput을 hook에서 막으면 커서도 복귀하지 않고, SetCursorPos로 복귀하면 대상이 `(1209,451)`의 추가 pressed Move를 받는다. 표시만 지우거나 이 복귀를 제품에 넣어 해결 처리하지 않는다.
- OS 변환: 사용자 go 승인 뒤 전용 인증서/Program Files 설치를 적용했다. UIAccess=true·Elevated=false에서 API 수락/해제 성공. WPF 수동 검사는 원본 전달 0/렌즈 Down 4, native 합성 검사는 원본 Down/Up 0/0·렌즈 Down 1로 BLOCKED다. 전후 해제 확인. 일반 제품의 OS 엔진 연결은 보류했고 전용 설치/인증서는 유지 중이다.
- 남은 범위: 실제 상대 마우스·보조 절대 입력·모니터 끝 탈출, 재귀/겹침, 혼합 DPI/음수 좌표/다중 모니터, 브라우저 마커/Blender/Windows 앱. 렌즈를 놓을 때 화면 안 보정, 모니터 변경 시 입력 해제·진입 창 복원을 구현했으며 R2 실측 대상이다. 우클릭·스크롤 등 R3는 핵심 검증 뒤 진행한다.
- 최신 지시: 사용자가 관리자 모드로 직접 검증한다. 자동 UI 검사는 중단했다. `Start-Magnifier-Admin.cmd`는 표준 앱의 사용자 RunAs 진입점, `Start-InputTransform-Manual.cmd`는 별도 UIAccess 수동 검사다. `doc/manual-validation.md` 참조. 관리자 실행은 UIAccess나 커서 문제 해결을 뜻하지 않는다.
- 2026-09-11 재실패: FIFO 수정 뒤 relay-stop-28180에서 누름 중 중지 3회. 원본 thread/root 감시로 재수정한 뒤에도 relay-stop-32236에서 03:04:52·56 KST에 2회 중지했다. PhysicalLeftHeld=true, PendingLeftRelease=false, ReadSucceeded=true이며 원본 root 17045840에서 현재 capture/root 3214510으로 바뀌었다. capture=0이나 조회 실패가 아닌 다른 root 전환이 중지 분기를 실행했다. 정상 인계인지 실제 손실인지는 미확정이다.
- 다음 구현: `PointerCaptureMonitor`의 다른 root 중지 조건을 실제 창 관계·드래그 과정과 대조한다. 현재 테스트도 이 조건을 손실로 가정한다. OLE/보조 창 인계는 가설이며 HWND별 class/owner·이벤트 순서 근거가 필요하다. Up 후 자동 재개만 바꾸어 누름 중 단절을 해결 처리하지 않는다.
- 최신 인계: `notes/runs/2026-09-11-drag-blocked-handoff.md`. 동일 소스의 표준 build 0/0, Core 34+Infrastructure 10=44개 통과. 이번 문서 정리에서 no-build 테스트 44개 재통과. 실행 로그 MVID `fb7a90a8-2e63-470c-b5e7-1fb2f92a1db7` 일치. PID 32236 실행 중이므로 종료·재빌드·UI 조작 없이 인계했다. 다른 미검증 UI 항목은 NEEDS_USER_UI_CHECK다.
