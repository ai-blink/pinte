# Magnifier 레포·웹 1차 탐색

날짜: 2026-09-10 · 목적: 기능·UI/UX 개편 초안 근거. 실행 호환성 보고가 아니다.

## 요구와 자료의 구분

직접 요청은 탐색 후 계획 수정과 브레인스토밍이다. 확대를 통한 정밀 조작·마우스만으로 복귀가 핵심이며 후속 답변은 드래그 가능·정밀 맞춤 어려움이다. 배치 목업 작성도 요청했다.

첨부 이미지는 Mixamo 마커 배치 사례다. 핸드오프 안의 구현·실행·커밋 지시문은 과거 맥락으로 읽었으며 이번 실행 명령으로 취급하지 않았다.

## 로컬 근거

| 파일·프로젝트 | 관찰 / 반영 |
|---|---|
| Magnifier 의도·M5·최근 5개 커밋 | 최근 207037b까지 A/B 전용; 계획의 중심을 직접 조작과 복귀로 교체 |
| src/Magnifier.App/SelectionPreviewWindow.xaml.cs:158 | 일반 이미지 클릭은 안내만 출력; 확대 표시를 조작 완료로 보지 않음 |
| src/Magnifier.App/SelectionOverlayWindow.xaml.cs:34 및 XAML | 드래그 선택·Esc 취소; 마우스 진입·취소 보강 필요 |
| src/Magnifier.App/MainWindow.xaml.cs:37 | 100ms 타이머; 실측 반응성은 별도 |
| Core/PointerInputSession.cs, Infrastructure/WindowsPointerInput.cs | release·전역 좌표 전달 자산 존재; UI와 실제 포인터 충돌 해결 증거 없음 |
| C:/ai/projects/blender-addon/INTENT.md·CLAUDE.md | 휠·가운데 버튼·키 연타를 화면 버튼으로 대체, 정밀 마커 작업; 앱 내부 제어는 애드온 역할 |
| C:/ai/projects/blink-tools/INTENT.md | 도구 다운로드와 질문·답변, 일반 사용자도 대상; 독립 도구다운 사용법 |
| C:/ai/projects/new-alt/doc/INTENT.md·next/README.md | overlay-first·모드 구분·release·API 수락과 대상 반응 구분 |
| new-alt next/src/AltController.Next.Infrastructure/Mouse/RemotePointerOutputAdapter.cs | 좌표·hold/release·커서 복원 코드; target resolver 의존은 범용 좌표 조작에 그대로 이식하지 않음 |

추가 [MagnifierApp 비교](2026-09-10-magnifierapp-reference.md)는 별도 기록했다.

ik의 Codex 메모리 경로는 없어 C:/Users/user/.claude/memory/ik-context.md를 읽기 참고했다. 2026-08-24 배경보다 이번 답변을 우선한다. 개인 프로필 원문·연락처·건강 정보는 문서·모델 보조 입력에 복제하지 않았다.

## 공개 GitHub 전수 목록

연결 계정 ai-blink. [공개 API](https://api.github.com/users/ai-blink/repos?type=owner&per_page=100&page=1)의 소유 공개 저장소 **8개/8개**를 목록·설명·README 수준으로 확인했다. 프로필 public_repos=8과 일치하며 추가 페이지는 없다. 전체 코드 감사라는 뜻은 아니다. 시사점은 README에서 도출한 해석이다.

| 저장소 | 확인한 기능 / 시사점 |
|---|---|
| [alt-tab](https://github.com/ai-blink/alt-tab) | 창 미리보기·제목, UI 배율·9방향 위치 제어 → 작은 모서리에 의존하지 않는 배치 |
| [copy-manager](https://github.com/ai-blink/copy-manager) | 탐색 중 유지되는 창, 배율·내부 스크롤 → 작업 도중 사라지지 않는 제어 |
| [GazeScroll](https://github.com/ai-blink/GazeScroll) | 머무르기·클릭 스크롤, 포커스 비탈취 → 휠 대체 UX 참고; 앱별 분기·관리자 실행은 복제하지 않음 |
| [memojang](https://github.com/ai-blink/memojang) | 익숙한 편집기, 캡처 제외 결과 표시 → 표준 UI·실제 상태 구분 |
| [Mini-Capture](https://github.com/ai-blink/Mini-Capture) | 상시 진입 버튼·영역 선택·뷰어 배율 → 마우스 진입 참고 |
| [polyexec](https://github.com/ai-blink/polyexec) | 격리 코드 실행·웹 지식 도구 → 조작 UX와 직접 연관 낮음 |
| [scrcpy-gui](https://github.com/ai-blink/scrcpy-gui) | 명령줄 옵션 GUI·프리셋; 일부 capture 탈출은 키 사용 → 키보드 탈출 의존 별도 점검 |
| [win-resize](https://github.com/ai-blink/win-resize) | 위치·크기 프로필, 명시적 종료 → 배치 복원 참고; 커서 제한은 기본 채택 안 함 |

blender-addon·blink-tools·new-alt·magnifier는 이 공개 목록에 없어 로컬에서 별도로 읽었다. 공개 상태의 사유는 추정하지 않는다.

## 웹 근거

| 출처 | 사실 / 설계 영향 |
|---|---|
| [Windows Magnifier](https://support.microsoft.com/en-au/windows/use-magnifier-to-make-things-on-the-screen-easier-to-see-414948ba-8b1c-d3bd-8615-0e5e32204198) | 전체 화면·렌즈·도킹과 추적 설정이 있음. 본 사용자의 복귀 어려움은 별도 UX 문제 |
| [OptiKey mouse](https://github.com/Optikey/Optikey/wiki/Simulate-a-mouse) | 대략 위치→확대→정밀 선택, 확대 일회/지속·hold/release·두 지점 drag. 단계적 정밀 선택 참고이며 연속 직접 드래그 검증 근거는 아님 |
| [MagSetInputTransform](https://learn.microsoft.com/en-us/windows/win32/api/magnification/nf-magnification-magsetinputtransform) | UIAccess 필수; Windows 10 1703부터 mouse에도 필요. touch 전용으로 제외하거나 mouse 자동 전달을 가정하지 않음 |
| [API Overview](https://learn.microsoft.com/en-us/windows/win32/winauto/magapi/magapi-intro) | 창형/전체형 구분, 전체형 창 필터 미지원·WOW64 미지원. 복귀띠·제외 창·실행 아키텍처 검증 필요 |
| [UIAccess](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-securityoverview) | 서명·신뢰 설치 경로·manifest 조건. UIAccess만으로 모든 권한 경계를 넘지 않음 |
| [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput) | 스트림 삽입 수 반환, UIPI 적용. 반환값·LastError만으로 UIPI 원인 식별 불가. 실제 반응 별도 확인 |
| [MSLLHOOKSTRUCT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-msllhookstruct) | injected flag는 모든 프로세스의 합성 이벤트 표시. 자기 출력과 타 보조입력 구분을 검증해야 함 |

## 결론·한계

1차 탐색 당시 추천은 고정 작업창+개요였으나, 이후 사용자가 실제 원본 테두리 창+독립 이동 확대 렌즈 창을 확인해 현재 계획은 두 창 구조로 바꿨다. 엔진은 실제 장치·겹침·드래그 복귀·배포 조건 검증 전이다.

research-dispatch로 출처를 수집했고 ollama-router의 로컬 러너·설치 태그 glm-5.2:cloud로 익명 요구의 UX 누락만 보조 검토했다. 드래그 중 툴바 접근 충돌 지적을 반영했다. 드래그 불가능 전제의 hold/release 필수 제안은 채택하지 않았다. 모델 출력은 출처·실행 증거가 아니다.
