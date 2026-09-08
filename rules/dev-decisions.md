# 개발 결정

## Vision

- D-001: 이 도구는 범용 Windows 화면 오버레이다. 특정 페인팅 앱의 내부 API에 결합하지 않는다.

## Layer boundaries

- D-002: WPF UI와 Windows 입력·캡처 코드는 분리한다.
- D-007: M2의 선택 영역과 캡처 프레임은 물리 화면 픽셀을 기준으로 한다. App은 WPF `PointToScreen` 결과를 Core `ScreenRegion`으로 바꾸고, Infrastructure만 GDI 화면 복사를 수행한다.
- D-009: M5 미리보기·선택 오버레이는 항상 위로 고정하지 않는다. 미리보기는 최초에 원본 영역 밖에 배치하며, 원본과 겹치면 자기 캡처 프레임으로 교체하지 않고 마지막 정상 프레임을 유지한다.

## Executor policy

- D-003: 실제 입력은 기본 비활성이며 Down·Move·Up은 단일 세션과 release 경로로 처리한다.
- D-008: M3의 `SendInput` P/Invoke는 Infrastructure만 소유한다. App gate는 기본 해제이고, Core 세션은 취소·capture 손실·gate 해제·오류에서 `LeftUp`을 시도한다. UIPI 거부 원인은 API 결과만으로 확정하지 않는다.

## Security and safety

- D-004: 권한이 다른 대상 앱에는 입력을 보내지 않고 원인을 표시한다.

## Workflow

- D-005: 구현 전 사용자 UX 설계 승인과 빌드 계획 검증을 요구한다.

## Infrastructure

- D-006: `C:\ai\projects\new-alt`의 원격 포인터·프레임 캡처 구조는 참고하되 코드를 그대로 복사하지 않는다.
