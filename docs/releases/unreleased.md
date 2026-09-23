# Pinte — Unreleased (after v0.1.1)

**Draft patch notes · not yet tagged · last updated 2026-09-24**

Changes on `main` since [v0.1.1](https://github.com/ai-blink/pinte/releases/tag/v0.1.1).
The version number and release date are decided at release time. English is
canonical; a Korean summary follows.

## Highlights

- **Advanced input timing settings.** Settings → Advanced → *Open input timing*
  opens a small always-on-top panel that tunes four relay waits without a
  rebuild: arrival before press (0–300 ms), minimum hold (0–200 ms), dwell
  after release (0–300 ms) and gap between gestures (0–300 ms). It offers
  Apply, Previous, Default, presets (Default 100/35/60/0, Fast 50/20/30/0,
  Low latency 5/1/5/0, Instant 0/0/0/0) and A/B slots. Values change only
  after the current click or drag finishes, are saved to
  `%LOCALAPPDATA%\Magnifier\pointer-timing.json` and are re-applied on start.
  Buttons move in 1 ms steps below 10 ms and 5 ms steps above.
- **Ordered click delivery for targets that miss instant clicks.** Each lens
  press is now relayed as one ordered gesture: move the cursor to the source
  point, wait, press, hold, release, dwell, then return. Queued clicks keep
  their order, drag movement is replayed in order, and zero or expired waits
  advance immediately instead of waiting for the next 50 ms tick. Pinte's own
  injected input is told apart from other accessibility tools by its input
  tag. Default waits: 100/35/60/0 ms.
- **Hidden-instance recovery.** Launching Pinte again while it is running
  brings the existing entry window back through the release-first return
  path, and the collapsed `⌕` lens icon stays above other windows.

## Verification

- `dotnet build Magnifier.slnx --nologo`: 0 warnings, 0 errors.
- Automated tests: Core 65, Infrastructure 81, App 61 passed.
- One user confirmed click, double-click and drag delivery with the
  Low latency preset (5/1/5/0 ms) in one game on one PC.

## Known limitations

- The best timing depends on the machine and the target application. The
  default stays conservative at 100/35/60/0 ms; if clicks are missed, increase
  the arrival wait first.
- Other games and applications, mixed-DPI and multi-monitor setups are not yet
  validated.
- Not code-signed. Secure desktops, elevated target applications, remote
  sessions and target-specific input policies can still block capture or input.

---

## 한국어 요약

v0.1.1 이후 `main`에 반영된 변경입니다. 버전 번호와 날짜는 릴리즈할 때 정합니다.

### 주요 변경

- **입력 타이밍 고급 설정**: 설정 → ⚙ 고급 → 「입력 타이밍 조절 열기」로 항상 위에
  뜨는 조절 창을 엽니다. 도착 후 누르기 대기(0~300ms)·최소 누름 유지(0~200ms)·
  해제 후 체류(0~300ms)·제스처 간격(0~300ms)을 재빌드 없이 바꿉니다. 적용·직전값·
  기본값, 프리셋(기본 100/35/60/0 · 빠름 50/20/30/0 · 저지연 5/1/5/0 · 즉시 0/0/0/0),
  A/B 비교를 제공합니다. 진행 중인 클릭·드래그가 끝난 뒤 다음 누름부터 바뀌고,
  값은 `%LOCALAPPDATA%\Magnifier\pointer-timing.json`에 저장되어 다음 실행에도
  적용됩니다. −/+ 버튼은 10ms 미만에서 1ms, 그 이상에서 5ms씩 움직입니다.
- **순서가 있는 클릭 전달**: 즉시 클릭을 놓치는 대상 앱을 위해, 렌즈 누름을 한 묶음
  동작(원본 위치로 이동 → 대기 → 누름 → 유지 → 해제 → 체류 → 복귀)으로 전달합니다.
  여러 클릭은 순서를 지키고 드래그 이동도 순서대로 재생하며, 0ms·이미 지난 대기는
  다음 50ms 주기를 기다리지 않고 바로 진행합니다. Pinte 자신이 보낸 입력은 입력 태그로
  다른 보조 도구 입력과 구분합니다. 기본 대기는 100/35/60/0ms입니다.
- **숨은 인스턴스 복구**: 실행 중에 다시 실행하면 기존 진입 창을 release 우선 경로로
  되살리고, 접힌 렌즈 `⌕` 아이콘은 다른 창 위에 유지됩니다.

### 검증

- 표준 build 경고 0·오류 0, 자동 테스트 Core 65·Infrastructure 81·App 61 통과.
- 한 사용자가 한 PC·한 게임에서 「저지연」(5/1/5/0ms)으로 클릭·더블클릭·드래그 전달을
  확인했습니다.

### 알려진 제한

- 알맞은 타이밍은 PC 사양과 대상 앱마다 다릅니다. 기본값은 보수적인 100/35/60/0ms를
  유지하며, 클릭이 누락되면 도착 대기부터 늘리세요.
- 다른 게임·앱, 혼합 DPI·다중 모니터 환경은 아직 검증하지 않았습니다.
- 코드 서명되지 않았습니다. 보안 화면, 관리자 권한 대상 앱, 원격 세션, 앱별 입력
  정책은 여전히 캡처나 입력을 막을 수 있습니다.
