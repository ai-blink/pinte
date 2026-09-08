# 아키텍처

## 현재 구조

- `src/Magnifier.App`: WPF 진입점과 창 구성. 현재는 템플릿 스캐폴드다.
- 이후 `Magnifier.Core`: 선택 영역·좌표 변환·A/B·입력 세션 상태 계약을 소유한다.
- 이후 `Magnifier.Infrastructure`: 화면 캡처, 대상 창 해석, 입력 전달과 release를 소유한다.

## 의존성 규칙

- App은 Core 계약만 소비하며 Win32 P/Invoke를 직접 보유하지 않는다.
- Infrastructure는 Core를 참조할 수 있지만 App을 참조하지 않는다.
- 실제 입력은 runtime gate와 단일 release 경로 뒤에 둔다.

## 검증 전략

- Core: 좌표 변환, 범위, 길게 누름 반복, A/B 세션 종료 단위 테스트.
- Infrastructure: 대상 창 해석·입력 해제 테스트와 진단 모드.
- App: WPF 명령·상태 표기 테스트와 사용자의 실제 대상 앱 확인.

## 다이어그램

현재는 구현 전이므로 다이어그램이 없다. 구현 시 입력 세션 상태 전이가 추가되면 `docs/diagrams/README.md`에 Mermaid 상태도를 등록한다.
