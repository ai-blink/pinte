# 개발 진행

- 2026-09-08: Git 작업공간, .NET 9 WPF 솔루션, 프로젝트 라이브 문서와 템플릿을 초기화하고 `644e343`으로 커밋했다. `dotnet build Magnifier.slnx --nologo`는 경고·오류 없이 통과했다.
- 2026-09-08: M1 영역 선택 오버레이와 확대 미리보기를 완료했다. `dotnet build Magnifier.slnx --nologo`는 경고·오류 0으로 통과했고, 사용자가 실제 UI 동작이 정상임을 확인했다. 실행 기록은 `notes/runs/2026-09-08-m1-region-selection.md`에 남겼다.
- 2026-09-08: M2 화면 캡처·좌표 변환을 시작했다. 완료선과 수락 확인은 `notes/plans/2026-09-08-magnifier-m2.md`에 고정한다.
- 2026-09-08: M2 Core·Infrastructure·App 연결과 Core 단위 테스트를 구현했다. `dotnet build Magnifier.slnx --nologo`는 경고·오류 0, `dotnet test Magnifier.slnx --nologo`는 3개 통과했고, 사용자가 실제 화면 캡처 성공을 확인했다.
- 2026-09-08: M2.1 실시간 미리보기를 완료했다. App이 최대 10fps의 비중첩 백그라운드 캡처를 미리보기에 적용하며, 새 선택·창 닫기·실패 시 갱신을 멈춘다. 솔루션 build 경고·오류 0, Core 테스트 3개 통과, 사용자가 실제 화면 변화 반영을 확인했다. 실행 기록은 `notes/runs/2026-09-08-m2-capture-live-preview.md`에 남겼다.
- 다음: M3 입력 gate·단일 release 계약의 완료선과 구현 계획을 고정한다. 실제 입력 활성화는 사용자 승인 전까지 구현·검증하지 않는다.
- 차단: 실제 입력 전달은 대상 앱별 수동 확인 전에는 열지 않는다.
