using System.Windows.Controls;
using Magnifier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.App.Tests;

[TestClass]
public sealed class CaptureRecoveryTests
{
    [TestMethod]
    public Task CaptureFailure_PausesRelayAndKeepsInputRequestForAutomaticRetry() => StaTest.Run(async () =>
    {
        using var host = new HiddenWindows();
        var source = new ScreenRegion(20, 30, 80, 60);
        await host.Lens.SetSourceAsync(source, 0);
        host.Relay.Calls.Clear();

        await host.Lens.ShowCaptureFailureAsync(source, "일시적인 캡처 오류");

        Assert.AreEqual(1, host.Relay.Calls.Count(call => call == "pause"));
        Assert.IsFalse(host.Relay.Calls.Contains("stop"));
        Assert.AreEqual(0, host.Relay.StopCount);
        StringAssert.Contains(((TextBlock)host.Lens.FindName("CaptureStatusText")).Text, "자동 재시도");
        Assert.IsFalse(PrivateAccess.Get<bool>(host.Lens, "_captureStopPending"));
    });

    [TestMethod]
    public Task PanMode_UpdatesExpandedAndCompactToggleLabels() => StaTest.Run(async () =>
    {
        using var host = new HiddenWindows();
        await host.Lens.SetSourceAsync(new ScreenRegion(20, 30, 80, 60), 0);

        await PrivateAccess.CallAsync(host.Lens, "SetPanModeAsync", true);

        Assert.AreEqual("손 도구 켜짐, 누르면 끔", System.Windows.Automation.AutomationProperties.GetName((Button)host.Lens.FindName("PanModeButton")));
        Assert.AreEqual("✋✓", ((Button)host.Lens.FindName("CompactPanModeButton")).Content);

        await host.Lens.EndPanModeAsync();

        Assert.AreEqual("손 도구 꺼짐, 누르면 켬", System.Windows.Automation.AutomationProperties.GetName((Button)host.Lens.FindName("PanModeButton")));
        Assert.AreEqual("✋○", ((Button)host.Lens.FindName("CompactPanModeButton")).Content);
    });
}
