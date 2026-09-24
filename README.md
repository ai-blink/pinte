# Pinte

<p align="center">
  <img src="src/Magnifier.App/Assets/Pinte.png" width="96" alt="Pinte logo">
</p>

<p align="center">A mouse-first Windows magnifier for precise clicks and drags.</p>

<p align="center">
  <a href="README.md">English</a> ·
  <a href="README.ko.md">한국어</a> ·
  <a href="README.zh-CN.md">简体中文</a> ·
  <a href="README.ja.md">日本語</a>
</p>

> **Official release — v0.2.0.** Pinte is available for hands-on use, while compatibility with every Windows application still needs to be verified.

Pinte helps people who work with a mouse to enlarge part of the Windows desktop, make a precise click or drag through an independent lens window, and return to the original screen without relying on a keyboard.

## What it does

1. Choose a rectangular source area on the original screen.
2. Open it in a separate, live magnified lens.
3. Click and drag inside the lens as you normally would.
4. Return to the original screen with the visible lens control.

The source frame and the lens can be positioned and resized separately. The lens supports normal and compact layouts, top or bottom toolbar placement, zoom controls, pan/hand tool, source-area editing, and remembered layout preferences.

## Download and run

1. Download `Pinte-v0.2.0-win-x64.zip` from the [v0.2.0 release](https://github.com/ai-blink/pinte/releases/tag/v0.2.0).
2. Extract the ZIP to a folder you can write to.
3. Run `Magnifier.App.exe`.
4. Select **Screen area**, adjust the source frame, then open the magnified lens.

The release archive is self-contained for 64-bit Windows 10 or Windows 11; it does not require a separately installed .NET runtime.

## Safety and compatibility

- Screen-capture hiding is off by default and can be changed in Settings. Turn it on to exclude Pinte helper windows and prevent the repeating “mirror inside a mirror” feedback loop in screen-recording or sharing tools.
- Input relay is paused before source editing, lens resizing, a capture failure, returning, or closing. A temporary capture failure is retried automatically; relay resumes only after a fresh frame arrives, unless you explicitly return or close the lens.
- Secure desktop surfaces, protected content, elevated applications, remote sessions, and individual application input policies can prevent capture or input relay. Test your intended target before depending on Pinte for an important operation.
- Pinte is a local live magnifier. It does not upload or save screen captures.

## Build from source

Requirements: Windows and the .NET 9 SDK.

```powershell
dotnet build Magnifier.slnx --nologo
dotnet test Magnifier.slnx --nologo
```

For manual checks—including multi-monitor, DPI, compact lens, capture recovery, and drag behavior—see [the validation guide](doc/manual-validation.md) and [compact-lens scenarios](doc/compact-lens-validation.md).

## Current status

`v0.2.0` adds advanced input timing settings, ordered click delivery for targets that miss instant clicks, a toolbar resize toggle for the lens, and a toolbar that stays on one line at narrow widths. The core click-and-drag flow has automated regression coverage, but live target-application behavior still needs validation across the Windows environments you use. Please report reproducible issues through [GitHub Issues](https://github.com/ai-blink/pinte/issues).

## License

Pinte is released under the [MIT License](LICENSE).
