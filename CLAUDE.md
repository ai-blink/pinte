# Magnifier

마우스만 사용하는 사람이 Windows 화면을 확대해 정밀하게 클릭·드래그하고, 마우스만으로 원래 화면에 복귀하도록 돕는 범용 .NET 9 WPF 데스크톱 앱이다.

이 파일은 프로젝트의 단일 canonical instruction source다. 상세 현재 상태와 설계는 `rules/` 및 `doc/`를 따른다. 경쟁하는 authoritative `AGENTS.md`는 만들지 않는다.

## 절대 기준

- 모든 변경은 `doc/INTENT.md`의 사용자 문제와 수락 기준에 연결한다.
- 코드 한 파일은 500줄을 넘기기 전에 역할 기준으로 분리하고, 문서는 8KB를 넘기기 전에 세부 문서로 분리한다.
- UI는 Win32 입력 주입이나 화면 캡처를 직접 호출하지 않는다. 해당 동작은 별도 Infrastructure 경계로 둔다.
- 실제 포인터 입력은 기본적으로 꺼 둔다. 입력 세션은 취소·창 닫기·포인터 capture 손실에도 반드시 release로 끝난다.

## 폴더 맵

- `src/` — WPF 앱과 이후 Core·Infrastructure 프로젝트
- `doc/` — 제품 의도와 워크플로 템플릿
- `rules/` — 현재 개발 상태·결정·로드맵
- `docs/diagrams/` — 흐름·상태 다이어그램 인덱스
- `notes/` — 계획과 실행 근거

## 인덱스

- 제품 의도와 수락 기준: `doc/INTENT.md`
- 아키텍처와 파일 소유권: `rules/dev-arch.md`
- 재개 지점: `rules/dev-context.md`
- 현재 진행과 로드맵: `rules/dev-progress.md`, `rules/dev-roadmap.md`
- 결정: `rules/dev-decisions.md`
- 다이어그램: `docs/diagrams/README.md`

## 명령어

```powershell
dotnet build Magnifier.slnx --nologo
dotnet test Magnifier.slnx --nologo
```

## 경계

- 대상 앱별 브러시·레이어·색상 제어는 추가하지 않는다. 대신 범용 좌표·드래그 보조에 집중한다.
- 주력은 확대 화면의 실시간 직접 클릭·드래그와 마우스 전용 복귀다. A→B 단일 직선 획은 보조 기능이며 자유 경로 매크로 재생은 추가하지 않는다. Esc는 추가 취소 수단이다.
