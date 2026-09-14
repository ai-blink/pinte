# 결정 수신함

| ID | Date | One-liner | Section | Status |
|---|---|---|---|---|
| D-028 | 2026-09-15 | 단일 인스턴스 Mutex(제품 경로) + 훅 워치독 자기검증(태그 프로브 되울림 확인 후에만 재설치)·진행 중 입력 보호·재설치 상한 12. D-027 폭주/드래그 끊김 회귀 수정 | Layer boundaries + Executor policy (코드) | 반영 |
| D-027 | 2026-09-14 | WH_MOUSE_LL 훅 조용한 제거 대응: 50ms마다 OS 커서/버튼 대조 3회 무응답이면 같은 스레드에서 재설치·버튼 재동기화·release 후 자동 재개, `hook-reinstall` 진단 기록, relay 스레드 Highest | Executor policy (코드) | D-028이 보완 |
