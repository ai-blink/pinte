# M5 직접 입력 세션 상태도

```mermaid
stateDiagram-v2
    [*] --> Disabled
    Disabled --> Armed: 명시적 조작 시작 / 최신 프레임·버튼 해제 확인
    Armed --> Hover: 렌즈 진입 / 두 창 입력 통과
    Hover --> Pressed: Down / 원본 좌표로 전달
    Pressed --> Pressed: 연속 Move / 원본 좌표로 전달
    Pressed --> Hover: 물리 Up / 원본 Up
    Armed --> Disabled: 중지·복귀·프레임 지연
    Hover --> Disabled: 경계·중지·복귀
    Pressed --> Waiting: 경계·중지·취소 / 마지막 유효 위치 Up 수락
    Pressed --> ReleasePending: 입력 해제 실패 / gate off
    ReleasePending --> ReleasePending: Up 재시도 실패
    ReleasePending --> Waiting: Up 재시도 수락 / 물리 버튼 아직 누름
    ReleasePending --> Disabled: Up 재시도 수락 / 물리 버튼 해제됨
    Waiting --> Disabled: 모든 물리 버튼 해제 관측
    Disabled --> [*]: 두 창 숨김 또는 종료
```

- 재진입도 Disabled다. 물리 버튼을 놓은 사실만으로 Armed가 되지 않는다.
- 경계 Up 뒤에만 커서를 렌즈 쪽으로 복원한다. Waiting에서는 이전 누름의 추가 클릭·Up 전달을 억제한다.
- Stop/취소에는 capture 손실·닫기·Esc·입력 실패·화면 구성 변경·프레임 지연도 포함한다. 물리 버튼이 이미 해제됐다면 Waiting을 거치지 않는다.
- Up 실패는 누름 상태를 보존한다. 명시적 StopAsync는 미해제 오류를 반환하며, 성공한 복귀로 처리하지 않는다.
- 직접 조작은 WPF mouse capture를 사용하지 않는다. 전용 스레드가 상태를 소유한다. hook callback은 원본 억제 여부를 반환하고 주입·창 스타일 변경은 반환 뒤 FIFO 큐에서 수행한다. UI는 최신 상태를 표시한다.
- A/B는 별도 PointerInputSession이다. 접힌 도구를 열면 직접 중계를 끄고, 별도 허용·카운트다운 뒤 렌즈를 잠시 숨겨 단일 획을 실행한다. 취소·완료 뒤 A/B 허용도 끈다.
- 이 상태도는 구현을 설명한다. 분리/겹침 곡선·왕복과 경계 Up·재무장은 native 프로브에서 확인했다. 실제 장치·표준 앱의 전체 조작·복귀는 NEEDS_USER_UI_CHECK다. [최신 검증](../../notes/runs/2026-09-10-r0-relay-retest.md)을 따른다.
