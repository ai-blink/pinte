# Pinte

<p align="center">
  <img src="src/Magnifier.App/Assets/Pinte.png" width="96" alt="Pinte 标志">
</p>

<p align="center">面向鼠标操作的 Windows 精确点击与拖动放大工具</p>

<p align="center">
  <a href="README.md">English</a> ·
  <a href="README.ko.md">한국어</a> ·
  <a href="README.zh-CN.md">简体中文</a> ·
  <a href="README.ja.md">日本語</a>
</p>

> **正式版 — v0.1.0。** 此版本适合日常实际使用，但仍需持续验证与各类 Windows 应用的兼容性。

Pinte 帮助主要使用鼠标的用户放大 Windows 桌面的指定区域，通过独立的放大镜窗口进行精确点击或拖动，并且无需键盘即可返回原始屏幕。

## 功能

1. 在原始屏幕上选择一个矩形源区域。
2. 在独立的实时放大镜中打开该区域。
3. 像平常一样在放大镜中点击和拖动。
4. 使用可见的放大镜控件返回原始屏幕。

源区域边框和放大镜窗口可以分别移动和调整大小。支持普通/紧凑布局、顶部/底部工具栏、缩放、手形工具平移、重新编辑源区域以及记住布局偏好。

## 下载与运行

1. 从 [v0.1.0 发布页](https://github.com/ai-blink/pinte/releases/tag/v0.1.0)下载 `Pinte-v0.1.0-win-x64.zip`。
2. 将 ZIP 解压到可写入的文件夹。
3. 运行 `Magnifier.App.exe`。
4. 选择 **Screen area**，调整源区域边框，然后打开放大镜。

发布包适用于 64 位 Windows 10 或 Windows 11，且为自包含版本，不需要另行安装 .NET 运行时。

## 安全与兼容性

- Pinte 会有意将自己的辅助窗口排除在屏幕捕获之外，以防止“镜中镜”式的无限反馈循环。因此，一些屏幕录制或屏幕共享工具可能不会显示 Pinte 放大镜。
- 编辑源区域、调整放大镜大小、捕获失败、返回或关闭之前，输入转发都会暂停。短暂的捕获失败会自动重试；只有收到新的画面后才会自动恢复操作。如果手动返回或关闭，则不会恢复。
- 安全桌面、受保护内容、以管理员权限运行的应用、远程会话及各应用的输入策略都可能阻止捕获或输入转发。请先在目标应用中验证，再用于重要操作。
- Pinte 是本地实时放大工具，不会上传或保存屏幕捕获内容。

## 从源代码构建

要求：Windows 和 .NET 9 SDK。

```powershell
dotnet build Magnifier.slnx --nologo
dotnet test Magnifier.slnx --nologo
```

有关多显示器、DPI、紧凑放大镜、捕获恢复和拖动的手动验证，请参阅[验证指南](doc/manual-validation.md)和[紧凑放大镜场景](doc/compact-lens-validation.md)。

## 当前状态

`v0.1.0` 是 Pinte 的首个正式版。核心点击与拖动流程已有自动回归测试，但真实目标应用中的行为仍需在您使用的 Windows 环境中持续验证。可复现的问题请提交至 [GitHub Issues](https://github.com/ai-blink/pinte/issues)。

## 许可证

Pinte 采用 [MIT License](LICENSE) 发布。
