# M4 길게 누름 수치 제어·A→B 단일 직선 획 계획

## 1. 목표

무엇을 끝내야 하는가:
- 미리보기에서 A와 B 원본 좌표를 지정하고, 길게 누를 수 있는 큰 수치 버튼으로 카운트다운을 조절한 뒤 한 번의 직선 획을 준비한다.

완료 기준:
- `RepeatButton`의 길게 누름으로 카운트다운 초가 1~10초 범위에서 반복 조절된다.
- A/B 지정 모드의 이미지 클릭은 실제 입력 없이 두 물리 화면 좌표를 표시한다.
- 입력 gate가 꺼진 상태에서 실행을 누르면 실제 입력을 보내지 않고 원인을 표시한다.
- gate가 켜진 실행은 카운트다운 후 `Begin(A)`와 `Complete(B)`를 한 번만 호출하며, Esc·창 닫기·gate 해제는 카운트다운과 진행 중 세션을 취소한다.

## 2. 범위

이번에 할 것:
- `SelectionPreviewWindow`에 큰 A/B 지정·실행 버튼과 길게 누름 카운트다운 제어를 추가한다.
- 기존 `PointerInputSession`을 재사용해 A→B 획과 취소를 연결한다.
- M4 계획·상태를 갱신한다.

이번에 하지 않을 것:
- Core·Infrastructure 새 추상화, 다중 구간 획, 자유 경로 재생, 대상 창 자동 식별, 권한 상승, 실제 활성 입력의 대상 앱 검증.

## 3. 접근

큰 단계:
1. 미리보기 창이 A/B 좌표와 지정 모드, 1~10초 카운트다운을 소유한다.
2. 이미지 클릭은 지정 모드일 때만 A/B를 갱신하며, 일반 드래그와 분리한다.
3. 실행은 gate를 먼저 확인한 뒤 취소 가능한 카운트다운과 한 번의 `Begin(A)`→`Complete(B)`를 수행한다.
4. build/test와 gate 해제 UI 흐름을 확인한다.

중요한 판단 기준:
- 반복 수치 조절은 WPF `RepeatButton`만 사용한다.
- 실행 중 실제 입력은 기존 M3 session이 release를 보장한다.
- 활성 gate의 실제 획은 전역 포인터를 조작하므로 사용자 승인 전에는 실행하지 않는다.

## 4. 검증

통과해야 할 확인:
- `dotnet build Magnifier.slnx --nologo`
- `dotnet test Magnifier.slnx --nologo`
- 실제 창에서 A/B 지정, 길게 누름 카운트다운 조절, gate 해제 실행 차단과 Esc 카운트다운 취소를 확인한다.

추가 승인 필요:
- checkbox를 켠 실제 대상 앱 A→B 획은 사용자 명시 승인 뒤에만 수행하며, 그 전 상태는 `NEEDS_USER_UI_CHECK`다.

## 5. 실행 규율

- Finish Line: 큰 반복 수치 제어와 기본 비활성 A→B 획 준비·취소 흐름을 코드·검증으로 남긴다.
- Scope Limit: `src/Magnifier.App/SelectionPreviewWindow.*`와 M4 계획·상태 문서만 변경한다.
- Review Budget: build·test·gate 해제 UI 확인에서 발견된 M4 차단 문제만 최대 두 번 수정한다.
- Stop Rule: 수락 확인이 통과하면 실제 대상 앱 획 검증, 다중 구간·자유 경로와 대상 창 자동화는 시작하지 않는다.
- Completion Impact: 안구마우스 사용자가 실제 입력을 켜기 전에 정밀 직선 획의 좌표·대기 시간을 안전하게 준비할 수 있다.
