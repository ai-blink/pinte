# Pinte

<p align="center">
  <img src="src/Magnifier.App/Assets/Pinte.png" width="96" alt="Pinte 로고">
</p>

<p align="center">마우스로 정밀하게 클릭·드래그하기 위한 Windows 확대 도구</p>

<p align="center">
  <a href="README.md">English</a> ·
  <a href="README.ko.md">한국어</a> ·
  <a href="README.zh-CN.md">简体中文</a> ·
  <a href="README.ja.md">日本語</a>
</p>

> **정식 릴리즈 — v0.1.0.** 실제 사용을 위해 배포하는 버전이며, 모든 Windows 앱과의 호환성은 계속 확인이 필요합니다.

Pinte는 마우스를 주로 사용하는 사람이 Windows 화면의 필요한 부분을 확대하고, 별도 렌즈 창에서 정밀하게 클릭하거나 드래그한 다음, 키보드 없이 원래 화면으로 돌아갈 수 있도록 돕습니다.

## 할 수 있는 일

1. 원래 화면에서 사각형 원본 영역을 지정합니다.
2. 별도의 실시간 확대 렌즈로 엽니다.
3. 렌즈 안에서 평소처럼 클릭하고 드래그합니다.
4. 눈에 보이는 렌즈 제어로 원래 화면으로 복귀합니다.

원본 테두리와 렌즈는 각각 이동·크기 조절할 수 있습니다. 일반/컴팩트 레이아웃, 위/아래 도구막대, 배율, 손 도구를 이용한 이동, 원본 영역 편집, 배치 기억을 지원합니다.

## 내려받기와 실행

1. [v0.1.0 릴리즈](https://github.com/ai-blink/pinte/releases/tag/v0.1.0)에서 `Pinte-v0.1.0-win-x64.zip`을 내려받습니다.
2. 쓰기 가능한 폴더에 ZIP을 풉니다.
3. `Magnifier.App.exe`를 실행합니다.
4. **화면 영역 지정**을 누르고 원본 테두리를 맞춘 뒤 확대 렌즈를 엽니다.

릴리즈 ZIP은 64비트 Windows 10 또는 Windows 11용 자체 포함 배포본입니다. 별도 .NET 런타임 설치가 필요하지 않습니다.

## 안전과 호환성

- Pinte는 자기 창을 화면 캡처에서 의도적으로 제외합니다. 렌즈가 끝없이 겹쳐 보이는 피드백 루프를 막기 위해서입니다. 그래서 일부 화면 녹화·공유 도구에서는 Pinte 렌즈가 보이지 않을 수 있습니다.
- 원본 편집, 렌즈 크기 조절, 캡처 실패, 복귀, 닫기 전에 입력 전달을 멈춥니다. 일시적인 캡처 실패는 자동 재시도하며, 새 프레임이 들어온 뒤에만 조작을 자동 재개합니다. 사용자가 복귀하거나 닫으면 재개하지 않습니다.
- 보안 데스크톱, 보호 콘텐츠, 관리자 권한 앱, 원격 세션, 앱별 입력 정책은 캡처 또는 입력 전달을 막을 수 있습니다. 중요한 작업에 쓰기 전 대상 앱에서 확인하세요.
- Pinte는 로컬 실시간 확대 도구입니다. 화면을 업로드하거나 저장하지 않습니다.

## 소스에서 빌드

필수 조건: Windows, .NET 9 SDK.

```powershell
dotnet build Magnifier.slnx --nologo
dotnet test Magnifier.slnx --nologo
```

멀티 모니터, DPI, 컴팩트 렌즈, 캡처 복구, 드래그 등의 수동 검증은 [검증 안내](doc/manual-validation.md)와 [컴팩트 렌즈 시나리오](doc/compact-lens-validation.md)를 확인하세요.

## 현재 상태

`v0.1.0`은 Pinte의 첫 정식 릴리즈입니다. 핵심 클릭·드래그 흐름에는 자동 회귀 테스트가 있지만, 실제 대상 앱 동작은 사용하는 Windows 환경에서 계속 확인이 필요합니다. 재현 가능한 문제는 [GitHub Issues](https://github.com/ai-blink/pinte/issues)에 알려 주세요.

## 라이선스

Pinte는 [MIT License](LICENSE)로 배포합니다.
