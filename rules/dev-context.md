# 재개 컨텍스트

- delivery workspace는 `C:/ai/projects/magnifier`의 현재 브랜치다. 이번 소스·테스트·문서 변경만 하나의 로컬 커밋으로 남기며, `notes/transfers/`와 도구 산출물은 스테이징·수정하지 않는다. 푸시는 하지 않는다.
- 2026-09-17 구현: 렌즈를 화면 밖으로 옮기거나 그 방향으로 키워도 작업 영역으로 되돌리지 않는다. 8방향 외곽 리사이즈는 하단 모서리 48 DIP·변 20 DIP이고, 이동 손잡이와 겹치던 상단 모서리는 24 DIP로 줄여 `SizeAll` hover를 우선한다.
- `렌즈 숨김`은 복귀가 아니다. 클릭 지점의 캡처 제외 `⌕` 오버레이로 접고, 그 아이콘을 누르면 기존 렌즈 위치·크기·배율을 유지한 채 새 프레임과 입력을 다시 연다. 아이콘 표시 또는 펼치기 실패는 렌즈/입력을 안전하게 중지하거나 기존 렌즈 표시를 유지한다.
- 좁은 영역 지정 도구막대는 이동 설명을 숨기고 축약 버튼을 사용해 `이 영역 확대`와 복귀가 잘리지 않게 했다. 일반·컴팩트 렌즈 모두 숨김 제어를 제공한다.
- 배율 `−/+`는 0.1× 단위이며, 일반의 세밀 입력과 컴팩트의 중앙 숫자 입력 모두 Enter 또는 포커스 이탈 때 같은 정규화·입력 보류 경로로 적용한다.
- 자동 검증: `dotnet build Magnifier.slnx --nologo` 경고 0/오류 0, `dotnet test Magnifier.slnx --nologo` Core 60·Infrastructure 31·App 52, 총 143개 통과. 실제 UI·대상 앱 입력은 `NEEDS_USER_UI_CHECK`이며 [수동 시나리오](../doc/compact-lens-validation.md)의 화면 밖 배치·리사이즈·이동 hover·접기/펼치기·직접 입력·좁은 선택 폭을 확인한다.
- 핵심 제약: UI는 Win32 캡처·입력 주입을 직접 호출하지 않는다. 창·영역·배율·리사이즈·숨김/펼치기 전후에는 release와 새 프레임을 우선하고, 명시적 중지·복귀·닫기·입력 실패는 자동 재개하지 않는다.
- 현재 공개 기준은 [Pinte v0.1.0](https://github.com/ai-blink/pinte/releases/tag/v0.1.0)이다. 개발용 표준 출력은 `src/Magnifier.App/bin/Debug/net9.0-windows/Magnifier.App.exe`, 이전 공개 실행본은 `C:\app\Magnifier.App.exe`다. 이번 커밋은 새 공개 배포를 만들지 않는다.
- 별도 미해결 범위: 실제 상대 마우스의 이중 포인터, 경계/오류별 release·복귀, 혼합 DPI·음수 좌표·다중 모니터, 브라우저/Blender/Windows 앱 호환. 기존 훅 워커 복구(D-030) 장기 확인도 `NEEDS_USER_UI_CHECK`다.
