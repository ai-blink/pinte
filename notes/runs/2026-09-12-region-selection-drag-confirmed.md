# 영역 지정·드래그 유지 수정 사용자 확인

- 날짜: 2026-09-12, workspace `C:/ai/projects/magnifier`, branch `main`.
- 사용자 요청: 핵심 기능·화면을 단순하게 구성하고 테스트는 직접 진행. 영역 지정 과정 누락과 드래그 중 제어 해제 수정.
- 결과: 수정 실행본 안내 뒤 사용자 **“아주 잘됨 문서 갱신 및 커밋”**. 이번 두 수정은 **USER_CONFIRMED_PASS**로 기록한다.

## 변경

- `화면 영역 지정` → 테두리 이동·크기 조절 → `이 영역 확대` → 렌즈 조작. 선택 중 입력은 꺼지고 취소하면 진입 창으로 돌아온다. 렌즈를 열 때 확정한 원본을 옮기지 않는다.
- 같은 원본 GUI thread의 다른 root capture 인계나 원본 전면 유지 중 capture=0만으로 드래그를 끊지 않는다. 원본 닫힘·조회 실패·관찰한 capture의 외부 이탈·입력 실패 시 release는 유지한다.
- 회귀 테스트는 내부 인계·capture=0 상태의 연속 이동·정상 Up 뒤 재개·외부 이탈을 구분하도록 수정했다. 새로운 프레임워크나 앱별 분기는 추가하지 않았다.

## 검증과 범위

- 같은 소스로 `dotnet build Magnifier.slnx --nologo`: 경고 0, 오류 0. 테스트 소스도 컴파일됐지만 사용자 지시에 따라 `dotnet test`와 자동 UI/입력 프로브는 실행하지 않았다.
- 표준 실행본: `C:/ai/projects/magnifier/src/Magnifier.App/bin/Debug/net9.0-windows/Magnifier.App.exe`.
- 문서·커밋 시 Magnifier PID 29252 실행 중. 종료·재실행·추가 빌드·UI 조작 없이 유지했다.
- 보조 로그 `relay-stop-29252.jsonl`에는 00:32:31 KST 명시적 원래 화면 복귀 1회가 있다. WasPressed/IsPressed/PhysicalLeftHeld=false. EngineBuild는 `5f6dd693-b858-4792-9ca2-5ecc261d8091`. 로그 자체로 드래그 성공을 증명하지 않으며 사용자 확인이 수락 근거다.
- 사용자 답변은 이번 핵심 수정에 대한 종합 확인이다. 대상 앱 이름·세부 드래그 경로·이중 포인터·경계/오류별 복귀·혼합 DPI까지 개별 통과로 확대하지 않는다. M5 전체는 진행 중이다.
- 이전 반복 해제 실패와 로그는 [이전 인계](2026-09-11-drag-blocked-handoff.md)에 보존한다.

## 저장

사용자 요청으로 소스 8개와 관련 안내·상태·설계 문서를 원본 main의 한 커밋으로 저장한다. 이 파일을 포함한 커밋이 기준이며 해시는 최종 응답과 Git 이력에서 확인한다. 푸시는 하지 않는다.

`notes/transfers/`, `.codex/config.toml`, `.codex-finalizer/`, `.deck-build/`, `output/`은 제외·보존한다. 다음 작업도 핵심 기능·화면 중심으로 진행하며 테스트는 사용자가 직접 한다.
