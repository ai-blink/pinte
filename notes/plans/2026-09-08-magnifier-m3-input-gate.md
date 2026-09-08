# M3 입력 gate·release 경로 계획

## 1. 목표

무엇을 끝내야 하는가:
- 미리보기의 짧은 드래그를 원본 화면 좌표로 환산할 수 있는 단일 입력 세션을 만들고, 실제 포인터 입력은 기본적으로 꺼 둔다.

완료 기준:
- 입력 gate가 꺼진 기본 상태에서는 미리보기 드래그가 `SendInput`을 호출하지 않는다.
- gate가 켜진 세션은 Down → Move → Up 순서를 한 번만 전달한다.
- 취소, 미리보기 창 닫기, WPF mouse capture 손실, gate 해제, 중간 오류는 항상 `LeftUp`을 시도하고 세션을 끝낸다.
- UIPI 등으로 입력을 보낼 수 없는 경우 성공처럼 표시하지 않고 원인을 표시한다.

## 2. 범위

이번에 할 것:
- Core에 기본 비활성 입력 gate와 단일 포인터 세션 계약을 추가하고 단위 테스트한다.
- Infrastructure에 가상 데스크톱 물리 픽셀을 `SendInput` 절대 좌표로 보내는 구현을 추가한다.
- App 미리보기 이미지에서 짧은 드래그를 받아 gate가 켜졌을 때만 세션에 전달한다.
- 입력 세션 상태도와 현재 개발 상태를 갱신한다.

이번에 하지 않을 것:
- 대상 창 자동 식별, 권한 상승, 입력 활성화의 실제 대상 앱 검증, 연속 입력, 자유 경로 녹화·재생, A→B 자동 획.

## 3. 접근

큰 단계:
1. Core 세션은 `MoveTo`·`LeftDown`·`LeftUp` 계약을 소비하고 release를 단일 finally 경로로 모은다.
2. Infrastructure만 `SendInput`과 가상 데스크톱 정규화를 소유한다. 실패 원인은 UIPI일 수 있음을 일반 메시지로 표시한다.
3. App은 checkbox 기본값을 꺼 두고, 미리보기 mouse capture 수명에 맞춰 세션 시작·이동·완료·취소를 호출한다.
4. Core 테스트, build/test, 기본 비활성 상태의 실제 창 확인을 수행한다.

중요한 판단 기준:
- 화면 좌표는 M2의 물리 픽셀 `ScreenPoint`를 그대로 사용한다.
- `SendInput`은 UIPI 때문에 거부될 수 있으며 원인을 정확히 판별할 수 없으므로 성공을 추정하지 않는다.
- 활성화된 실제 입력 검증은 전역 포인터를 조작하므로 사용자 승인 전에는 실행하지 않는다.

## 4. 검증

통과해야 할 확인:
- `dotnet test Magnifier.slnx --nologo`에서 기본 비활성, 정상 Down→Move→Up, 취소·gate 해제·오류 release를 검증한다.
- `dotnet build Magnifier.slnx --nologo`가 경고·오류 0으로 통과한다.
- 실제 창에서 입력 checkbox가 기본 해제이고, 해제 상태 미리보기 드래그가 입력을 보내지 않는 상태 문구를 보이는지 확인한다.

추가 승인 필요:
- checkbox를 켠 실제 대상 앱 드래그는 사용자 명시 승인 뒤에만 수행하며, 그 전 상태는 `NEEDS_USER_UI_CHECK`로 기록한다.

## 5. 남는 리스크

주의할 점:
- `SendInput`은 같은 또는 낮은 무결성 수준에만 전달되며 UIPI 차단 원인을 API 결과만으로 확정할 수 없다.

후속으로 넘길 것:
- M4에서 길게 누름 수치 제어와 명시적 A→B 단일 직선 획을 이 세션 위에 추가한다.

## 실행 규율

- Finish Line: 기본 비활성 input gate와 예외·취소에도 release를 시도하는 단일 드래그 세션을 코드·테스트·비활성 UI 확인으로 남긴다.
- Scope Limit: `src/Magnifier.Core`, `src/Magnifier.Infrastructure`, `src/Magnifier.App`, Core 테스트, 입력 세션 상태도와 M3 상태 문서만 변경한다.
- Review Budget: build·test·기본 비활성 UI 확인에서 발견된 M3 차단 문제만 최대 두 번 수정한다.
- Stop Rule: 수락 확인이 통과하면 실제 대상 앱 활성 입력, A→B 자동 획, 자유 경로와 대상 창 자동화는 시작하지 않는다.
- Completion Impact: 사용자가 기본적으로 안전한 상태에서 이후 정밀 드래그 기능을 활성화할 수 있는 복구 가능한 입력 기반이 생긴다.
