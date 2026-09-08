# 개발 진행

- 2026-09-08: Git 작업공간, .NET 9 WPF 솔루션, 프로젝트 라이브 문서와 템플릿을 초기화하고 `644e343`으로 커밋했다. `dotnet build Magnifier.slnx --nologo`는 경고·오류 없이 통과했다.
- 2026-09-08: M1 영역 선택 오버레이와 확대 미리보기를 완료했다. `dotnet build Magnifier.slnx --nologo`는 경고·오류 0으로 통과했고, 사용자가 실제 UI 동작이 정상임을 확인했다. 실행 기록은 `notes/runs/2026-09-08-m1-region-selection.md`에 남겼다.
- 2026-09-08: M2 화면 캡처·좌표 변환을 시작했다. 완료선과 수락 확인은 `notes/plans/2026-09-08-magnifier-m2.md`에 고정한다.
- 2026-09-08: M2 Core·Infrastructure·App 연결과 Core 단위 테스트를 구현했다. `dotnet build Magnifier.slnx --nologo`는 경고·오류 0, `dotnet test Magnifier.slnx --nologo`는 3개 통과했고, 사용자가 실제 화면 캡처 성공을 확인했다.
- 2026-09-08: M2.1 실시간 미리보기를 완료했다. App이 최대 10fps의 비중첩 백그라운드 캡처를 미리보기에 적용하며, 새 선택·창 닫기·실패 시 갱신을 멈춘다. 솔루션 build 경고·오류 0, Core 테스트 3개 통과, 사용자가 실제 화면 변화 반영을 확인했다. 실행 기록은 `notes/runs/2026-09-08-m2-capture-live-preview.md`에 남겼다.
- 2026-09-08: M3 입력 gate·release 경로를 시작했다. 완료선과 수락 확인은 `notes/plans/2026-09-08-magnifier-m3-input-gate.md`에 고정한다.
- 2026-09-08: M3 입력 gate·release 경로를 완료했다. Core는 기본 비활성·정상 Down→Move→Up·gate 해제·오류 release를 테스트하고, App은 기본 해제 checkbox와 capture 손실·Esc·창 닫기 취소를 연결했다. 솔루션 build 경고·오류 0, Core 테스트 7개 통과, 사용자가 비활성 드래그 안내가 유지됨을 확인했다. 실행 기록은 `notes/runs/2026-09-08-m3-input-gate.md`에 남겼다.
- 2026-09-08: M4 길게 누름 수치 제어와 A→B 단일 직선 획을 시작했다. 완료선과 수락 확인은 `notes/plans/2026-09-08-magnifier-m4.md`에 고정한다.
- 2026-09-08: M4를 완료했다. App 미리보기 창에 1~10초 `RepeatButton` 카운트다운, A/B 좌표 지정, 기본 비활성 실행 차단과 Esc 취소를 연결했다. 상단은 한 줄 상태 Grid, 캡처 상태는 이미지 오버레이, 하단은 얇은 툴바로 재구성했다. 솔루션 build 경고·오류 0, Core 테스트 7개 통과, 사용자가 A/B 제어와 미리보기 우선 비율을 확인했다. 실행 기록은 `notes/runs/2026-09-08-m4-straight-stroke.md`에 남겼다.
- 2026-09-08~09: M5에서 보고된 미리보기 크기 변경·Blender 빈 프레임 차단 문제를 수정했다. 선택 오버레이·미리보기의 항상 위를 제거하고, 10fps 캡처마다 미리보기를 강제 활성화하던 호출을 제거했다. 미리보기는 처음 선택 영역 밖에 배치되고, 이후 원본 영역과 겹치면 마지막 정상 프레임을 유지하며 갱신을 보류한다. 솔루션 build 경고·오류 0, Core 테스트 7개 통과, 사용자가 Blender 5.2 미리보기의 정상 표시를 확인했다. 활성 입력 검증은 `NEEDS_USER_UI_CHECK`다. 실행 기록은 `notes/runs/2026-09-08-m5-window-layering.md`에 남겼다.
- 다음: M5 실제 브라우저 마스크 페인팅 화면과 Blender 5.2에서 기본 비활성·활성 입력의 수동 호환성 확인을 계획한다.
- `NEEDS_USER_UI_CHECK`: 실제 대상 앱에서 checkbox를 켠 짧은 드래그와 A→B 획은 사용자 승인과 대상 창 준비 뒤에만 확인한다.
