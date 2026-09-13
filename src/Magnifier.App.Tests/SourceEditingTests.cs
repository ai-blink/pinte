using System.Windows.Threading;
using Magnifier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.App.Tests;

[TestClass]
public sealed class SourceEditingTests
{
    [TestMethod]
    public Task ModalResume_WhileSourceEditing_KeepsExternalSuspension() => StaTest.Run(async () =>
    {
        using var host = new HiddenWindows();
        PrivateAccess.Set(host.Main, "_editingSource", true);
        await PrivateAccess.CallAsync(host.Main, "BeginModalAsync", "중첩 모달");
        Assert.IsTrue(host.Lens.IsInteractionLocked);
        await PrivateAccess.CallAsync(host.Main, "EndModalAsync", 0, "모달 종료");
        Assert.IsFalse(host.Lens.IsInteractionLocked);

        // 숨은 렌즈의 IsVisible guard 아래인 실제 재개 메서드를 별도로 확인한다.
        await PrivateAccess.CallAsync(host.Main, "ResumeInputAfterModalAsync", "영역 편집 보류 유지");

        Assert.IsTrue(PrivateAccess.Get<bool>(host.Main, "_editingSource"));
        CollectionAssert.AreEqual(new[] { true }, host.Relay.Suspensions);
        Assert.AreEqual(0, host.Relay.StartCount);
        AssertNoDesktopCalls(host);
    });

    [TestMethod]
    public Task CompletingEdit_DuringModal_DoesNotConfirmOrReleaseSuspension() => StaTest.Run(async () =>
    {
        using var host = new HiddenWindows();
        PrivateAccess.Set(host.Main, "_editingSource", true);
        PrivateAccess.Set(host.Main, "_modalOpen", true);

        await PrivateAccess.CallAsync(host.Main, "CompleteSourceEditingAsync");

        Assert.IsTrue(PrivateAccess.Get<bool>(host.Main, "_editingSource"));
        Assert.AreEqual(0, host.Relay.Calls.Count);
        Assert.IsNull(host.Lens.CurrentRegion);
        AssertNoDesktopCalls(host);
    });

    [TestMethod]
    public Task CompletingEdit_ConfirmsSourceButDoesNotCreateInputRequest() => StaTest.Run(async () =>
    {
        using var host = new HiddenWindows();
        var source = new ScreenRegion(-600, 200, 320, 180);
        host.Editor.SetRegion(source);
        PrivateAccess.Set(host.Main, "_editingSource", true);
        await host.Lens.SetInputSuspendedAsync(true, "편집 시작");

        await PrivateAccess.CallAsync(host.Main, "CompleteSourceEditingAsync");

        Assert.AreEqual(source, host.Lens.CurrentRegion);
        Assert.IsFalse(PrivateAccess.Get<bool>(host.Main, "_editingSource"));
        CollectionAssert.AreEqual(new[] { true, false }, host.Relay.Suspensions);
        Assert.AreEqual(0, host.Relay.StartCount);
        Assert.AreEqual(0, host.Relay.FrameCount, "숨은 창에서 새 프레임을 확인한 것으로 보고하지 않는다.");
        AssertNoDesktopCalls(host);
    });

    [TestMethod]
    public Task StaleEditCompletion_AfterSessionChange_CannotResume() => StaTest.Run(async () =>
    {
        using var host = new HiddenWindows();
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.Relay.PauseCallback = () => waiting.Task;
        PrivateAccess.Set(host.Main, "_editingSource", true);
        var completion = PrivateAccess.CallAsync(host.Main, "CompleteSourceEditingAsync");
        Assert.IsFalse(completion.IsCompleted);

        PrivateAccess.Set(host.Main, "_sessionVersion", 1);
        waiting.SetResult();
        await completion;

        Assert.AreEqual(0, host.Relay.Suspensions.Count);
        Assert.AreEqual(0, host.Relay.StartCount);
        Assert.IsFalse(PrivateAccess.Get<DispatcherTimer>(host.Main, "_timer").IsEnabled);
        AssertNoDesktopCalls(host);
    });

    [TestMethod]
    public Task EditCompletionReleaseFailure_StopsAndDoesNotRequestResume() => StaTest.Run(async () =>
    {
        using var host = new HiddenWindows();
        await host.Lens.SetInputSuspendedAsync(true, "영역 편집 시작");
        host.Relay.PauseCallback = () => Task.FromException(new InvalidOperationException("해제 실패"));
        PrivateAccess.Set(host.Main, "_editingSource", true);

        await PrivateAccess.CallAsync(host.Main, "CompleteSourceEditingAsync");

        Assert.AreEqual(1, host.Relay.StopCount);
        Assert.AreEqual(0, host.Relay.StartCount);
        Assert.IsTrue(host.Relay.Suspensions[^1], "Stop 성공 뒤에도 외부 편집 보류는 남는다.");
        Assert.AreEqual(0, host.Relay.FrameCount);
        Assert.IsFalse(PrivateAccess.Get<bool>(host.Main, "_editingSource"));
        AssertNoDesktopCalls(host);
    });

    [TestMethod]
    public Task ModalResumeFailure_StopsEvenWhenReleaseRetryAlsoFails() => StaTest.Run(async () =>
    {
        using var host = new HiddenWindows();
        await host.Lens.SetInputSuspendedAsync(true, "모달 시작");
        host.Relay.SuspensionFailure = new InvalidOperationException("보류 해제 실패");
        host.Relay.StopFailure = new InvalidOperationException("Up 재시도 실패");

        await PrivateAccess.CallAsync(host.Main, "ResumeInputAfterModalAsync", "모달 종료");

        Assert.AreEqual(1, host.Relay.StopCount);
        Assert.AreEqual(0, host.Relay.StartCount);
        Assert.AreEqual(0, host.Relay.FrameCount);
        AssertNoDesktopCalls(host);
    });

    private static void AssertNoDesktopCalls(HiddenWindows host)
    {
        Assert.AreEqual((nint)0, host.Lens.WindowHandle);
        Assert.AreEqual((nint)0, host.Editor.WindowHandle);
        Assert.AreEqual(0, host.Capture.CaptureCount);
        Assert.AreEqual(0, host.Capture.ExclusionCount);
        Assert.AreEqual(0, host.Windows.NativeOperationCount);
        Assert.AreEqual(0, host.Pointer.CallCount);
    }
}
