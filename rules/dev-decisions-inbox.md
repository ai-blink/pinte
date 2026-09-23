# 결정 수신함

| ID | Date | One-liner | Section | Status |
|---|---|---|---|---|
| D-032 | 2026-09-24 | 입력 전달 타이밍은 환경마다 달라 고급 설정 조절 창으로 노출한다. 범위 도착0~300·누름0~200·체류0~300·간격0~300ms, relay 시퀀스 idle 경계에서만 전체 교체, 별도 `pointer-timing.json`에 저장. 기본값 100/35/60/0 유지, 사용자 확인값 5/1/5/0은 「저지연」 프리셋 | Layer boundaries + Executor policy (코드) | 반영 |
| D-031 | 2026-09-17 | 화면 밖 렌즈 배치는 유지하고, 숨김은 복귀 대신 클릭 지점의 캡처 제외 펼치기 아이콘으로 전환 | Layer boundaries (코드) | 반영 |
| D-028 | 2026-09-15 | 단일 인스턴스 Mutex(제품 경로) + 훅 워치독 자기검증(태그 프로브 되울림 확인 후에만 재설치)·진행 중 입력 보호·재설치 상한 12. D-027 폭주/드래그 끊김 회귀 수정 | Layer boundaries + Executor policy (코드) | 반영 |
| D-027 | 2026-09-14 | WH_MOUSE_LL 훅 조용한 제거 대응: 50ms마다 OS 커서/버튼 대조 3회 무응답이면 같은 스레드에서 재설치·버튼 재동기화·release 후 자동 재개, `hook-reinstall` 진단 기록, relay 스레드 Highest | Executor policy (코드) | D-028이 보완 |
