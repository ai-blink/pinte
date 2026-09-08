# 개발 진행

- 2026-09-08: Git 작업공간, .NET 9 WPF 솔루션, 프로젝트 라이브 문서와 템플릿을 초기화하고 `644e343`으로 커밋했다. `dotnet build Magnifier.slnx --nologo`는 경고·오류 없이 통과했다.
- 2026-09-08: M1 영역 선택 오버레이와 확대 미리보기를 완료했다. `dotnet build Magnifier.slnx --nologo`는 경고·오류 0으로 통과했고, 사용자가 실제 UI 동작이 정상임을 확인했다. 실행 기록은 `notes/runs/2026-09-08-m1-region-selection.md`에 남겼다.
- 2026-09-08: M2 화면 캡처·좌표 변환을 시작했다. 완료선과 수락 확인은 `notes/plans/2026-09-08-magnifier-m2.md`에 고정한다.
- 2026-09-08: M2 Core·Infrastructure·App 연결과 Core 단위 테스트를 구현했다. `dotnet build Magnifier.slnx --nologo`는 경고·오류 0, `dotnet test Magnifier.slnx --nologo`는 3개 통과했고, 사용자가 실제 화면 캡처 성공을 확인했다.
- 2026-09-08: M2.1 실시간 미리보기를 완료했다. App이 최대 10fps의 비중첩 백그라운드 캡처를 미리보기에 적용하며, 새 선택·창 닫기·실패 시 갱신을 멈춘다. 솔루션 build 경고·오류 0, Core 테스트 3개 통과, 사용자가 실제 화면 변화 반영을 확인했다. 실행 기록은 `notes/runs/2026-09-08-m2-capture-live-preview.md`에 남겼다.
- 2026-09-08: M3 입력 gate·release 경로를 시작했다. 완료선과 수락 확인은 `notes/plans/2026-09-08-magnifier-m3-input-gate.md`에 고정한다.
- 2026-09-08: M3 입력 gate·release 경로를 완료했다. Core는 기본 비활성·정상 Down→Move→Up·gate 해제·오류 release를 테스트하고, App은 기본 해제 checkbox와 capture 손실·Esc·창 닫기 취소를 연결했다. 솔루션 build 경고·오류 0, Core 테스트 7개 통과, 사용자가 비활성 드래그 안내가 유지됨을 확인했다. 실행 기록은 `notes/runs/2026-09-08-m3-input-gate.md`에 남겼다.
- 다음: M4 길게 누름 수치 제어와 A→B 단일 직선 획의 완료선과 최소 입력 세션 확장을 계획한다.
- `NEEDS_USER_UI_CHECK`: 실제 대상 앱에서 checkbox를 켠 포인터 입력은 사용자 승인과 대상 창 준비 뒤에만 확인한다.
