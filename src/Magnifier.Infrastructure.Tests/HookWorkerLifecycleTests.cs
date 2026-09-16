using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.Infrastructure.Tests;

[TestClass]
public sealed class HookWorkerLifecycleTests
{
    [TestMethod]
    public void Replacement_IsSingleFlight_AndAdvancesWorkerGeneration()
    {
        var lifecycle = new HookWorkerLifecycle();

        Assert.AreEqual(1, lifecycle.Generation);
        Assert.IsTrue(lifecycle.TryBeginReplacement());
        Assert.IsFalse(lifecycle.TryBeginReplacement(), "A silent hook may only schedule one successor thread.");
        Assert.IsTrue(lifecycle.ReplacementPending);

        Assert.AreEqual(2, lifecycle.CompleteReplacement());
        Assert.IsFalse(lifecycle.ReplacementPending);
    }

    [TestMethod]
    public void Replacement_CannotCompleteWithoutARequest()
    {
        var lifecycle = new HookWorkerLifecycle();

        Assert.ThrowsException<InvalidOperationException>(() => lifecycle.CompleteReplacement());
    }
}
