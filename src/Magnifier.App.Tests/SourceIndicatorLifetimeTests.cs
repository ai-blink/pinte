using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.App.Tests;

[TestClass]
public sealed class SourceIndicatorLifetimeTests
{
    [TestMethod]
    public void Brief_IsVisibleUntilExactlyFiveSeconds()
    {
        var clock = new ManualClock();
        var lifetime = new SourceIndicatorLifetime(clock);
        lifetime.Start(SourceIndicatorPreference.Brief);

        Assert.IsTrue(lifetime.IsBrief);
        clock.Advance(TimeSpan.FromMilliseconds(4999));
        Assert.IsTrue(lifetime.IsVisible);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.IsFalse(lifetime.IsVisible);
    }

    [TestMethod]
    public void BriefRestart_OldDeadlineCannotHideNewIndication()
    {
        var clock = new ManualClock();
        var lifetime = new SourceIndicatorLifetime(clock);
        lifetime.Start(SourceIndicatorPreference.Brief);
        clock.Advance(TimeSpan.FromSeconds(4));
        lifetime.Start(SourceIndicatorPreference.Brief);

        clock.Advance(TimeSpan.FromSeconds(1)); // 첫 표시의 오래된 Tick 시점.
        Assert.IsTrue(lifetime.IsVisible);
        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.IsFalse(lifetime.IsVisible);
    }

    [TestMethod]
    public void CancelAndRestart_OnlyNewLifetimeRemains()
    {
        var clock = new ManualClock();
        var lifetime = new SourceIndicatorLifetime(clock);
        lifetime.Start(SourceIndicatorPreference.Brief);
        lifetime.Cancel();
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.IsFalse(lifetime.IsVisible);
        Assert.IsFalse(lifetime.IsBrief);

        lifetime.Start(SourceIndicatorPreference.Brief);
        Assert.IsTrue(lifetime.IsVisible);
    }

    [TestMethod]
    public void Always_HasNoOldBriefDeadline()
    {
        var clock = new ManualClock();
        var lifetime = new SourceIndicatorLifetime(clock);
        lifetime.Start(SourceIndicatorPreference.Brief);
        lifetime.Start(SourceIndicatorPreference.Always);
        clock.Advance(TimeSpan.FromDays(1));

        Assert.IsTrue(lifetime.IsVisible);
        Assert.IsFalse(lifetime.IsBrief);
    }

    [TestMethod]
    public void Hidden_CancelsPreviousIndication()
    {
        var lifetime = new SourceIndicatorLifetime(new ManualClock());
        Assert.IsFalse(lifetime.IsVisible);
        lifetime.Start(SourceIndicatorPreference.Always);
        lifetime.Start(SourceIndicatorPreference.Hidden);

        Assert.IsFalse(lifetime.IsVisible);
        Assert.IsFalse(lifetime.IsBrief);
    }
}
