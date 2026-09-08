# 아키텍처

## 현재 구조

- `src/Magnifier.App`: WPF 진입점·선택 오버레이·미리보기 창을 소유한다. 캡처 구현을 직접 호출하지 않고 Core의 `IScreenCapture` 계약을 소비한다.
- `src/Magnifier.Core`: 물리 화면 영역, BGRA32 프레임, 미리보기→원본 화면 좌표 환산 계약을 소유한다.
- `src/Magnifier.Infrastructure`: Windows GDI 화면 캡처를 구현한다. 대상 창 해석·입력 전달·release는 아직 소유하지 않는다.
- `src/Magnifier.Core.Tests`: Core 좌표 환산과 프레임 불변 조건을 단위 테스트한다.

## 의존성 규칙

- App은 Core 계약을 소비하며, composition root에서만 Infrastructure 구현을 만든다. Win32 P/Invoke를 직접 보유하지 않는다.
- Infrastructure는 Core를 참조할 수 있지만 App을 참조하지 않는다.
- 실제 입력은 runtime gate와 단일 release 경로 뒤에 둔다.

## 검증 전략

- Core: 좌표 변환, 범위, 길게 누름 반복, A/B 세션 종료 단위 테스트.
- Infrastructure: 화면 캡처 실패·대상 창 해석·입력 해제 테스트와 진단 모드.
- App: WPF 명령·캡처 상태 표기 테스트와 사용자의 실제 대상 앱 확인.

## 다이어그램

`docs/diagrams/capture-flow.md`가 M2의 선택·캡처·미리보기 경계를 설명한다. 입력 세션 상태 전이가 구현되면 `docs/diagrams/README.md`에 Mermaid 상태도를 추가한다.
