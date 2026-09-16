using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.Infrastructure.Tests;

[TestClass]
public sealed class HookRecoveryLimiterTests
{
    [TestMethod]
    public void ConsecutiveRecoveries_StopAfterTheConfiguredBudget()
    {
        var limiter = new HookRecoveryLimiter(maximumAttempts: 3, quietPeriodMilliseconds: 1_000);

        Assert.IsTrue(limiter.TryAcquire(0));
        Assert.IsTrue(limiter.TryAcquire(100));
        Assert.IsTrue(limiter.TryAcquire(200));
        Assert.IsFalse(limiter.TryAcquire(300));
        Assert.AreEqual(3, limiter.Attempts);
    }

    [TestMethod]
    public void QuietInterval_ResetsTheAutomaticRecoveryBudget()
    {
        var limiter = new HookRecoveryLimiter(maximumAttempts: 2, quietPeriodMilliseconds: 1_000);

        Assert.IsTrue(limiter.TryAcquire(0));
        Assert.IsTrue(limiter.TryAcquire(100));
        Assert.IsFalse(limiter.TryAcquire(200));

        Assert.IsTrue(limiter.TryAcquire(1_200));
        Assert.AreEqual(1, limiter.Attempts);
    }

    [TestMethod]
    public void ExplicitNewSession_ResetsTheAutomaticRecoveryBudget()
    {
        var limiter = new HookRecoveryLimiter(maximumAttempts: 1, quietPeriodMilliseconds: 1_000);

        Assert.IsTrue(limiter.TryAcquire(0));
        Assert.IsFalse(limiter.TryAcquire(100));

        limiter.Reset();

        Assert.IsTrue(limiter.TryAcquire(200));
    }
}
