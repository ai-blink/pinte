# M5 두 창 캡처·좌표 흐름

```mermaid
flowchart LR
    A[원본 테두리 이동·크기 조절] --> B[PointToScreen 물리 ScreenRegion]
    B --> C[Main 비중첩 캡처 요청 33ms]
    C --> D[Infrastructure GDI 캡처]
    D --> E[CapturedFrame BGRA32]
    E --> F[독립 렌즈 WriteableBitmap]
    G[렌즈 제목 이동·배율 조절] --> H[렌즈 표시 영역의 물리 좌표]
    B --> I[Core LensViewport 원본]
    H --> J[Core LensViewport 표시 영역]
    I --> K[논리 포인터를 원본 좌표로 변환]
    J --> K
    K --> L[Infrastructure 입력 중계 후보]
    F --> M[최신 프레임 시각 갱신]
    M --> L
```

- 원본 테두리만 Source를 바꾸며, 렌즈 이동·배율은 Destination만 바꾼다.
- 두 창과 제어부는 캡처 제외를 요청한다. 렌즈의 입력 통과는 별도 중계 경로이며 캡처 제외로 대체하지 않는다.
- Main은 이전 캡처가 끝나야 다음 요청을 처리한다. 영역 변경·복귀 이후 도착한 이전 프레임은 버린다.
- 캡처 실패는 입력을 중지하고 프레임을 지운다. 후속 캡처 성공은 보기만 복원한다. 750ms 갱신 지연은 입력 엔진에서도 중지한다.
- 33ms 요청·16ms 포인터 표시 갱신은 설정값이며 실측 fps·지연이 아니다.
- App은 Core 계약을 호출하고 Win32 캡처·입력 구현은 Infrastructure에 둔다.
- 현재 상태는 구현 후보다. 겹침·재귀·혼합 DPI의 실사용 검증은 중단 상태다.
