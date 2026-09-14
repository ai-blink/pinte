using Microsoft.VisualStudio.TestTools.UnitTesting;
using Action = Magnifier.Infrastructure.HookLivenessMonitor.Action;

namespace Magnifier.Infrastructure.Tests;

[TestClass]
public sealed class HookLivenessMonitorTests
{
    [TestMethod]
    public void SilentHook_ProbesFirst_ThenReinstallsWhenProbeNeverEchoes()
    {
        var monitor = new HookLivenessMonitor(requiredStrikes: 3, probeGraceTicks: 2);
        Assert.AreEqual(Action.None, monitor.Sample(0, 10, 10, 0), "First sample only records a baseline.");
        Assert.AreEqual(Action.None, monitor.Sample(50, 11, 10, 0));
        Assert.AreEqual(Action.None, monitor.Sample(100, 12, 10, 0));
        // Third strike suspects the hook but must verify before reinstalling.
        Assert.AreEqual(Action.Probe, monitor.Sample(150, 13, 10, 0));
        // No echo arrives, so after the grace window it is confirmed dead.
        Assert.AreEqual(Action.None, monitor.Sample(200, 14, 10, 0));
        Assert.AreEqual(Action.Reinstall, monitor.Sample(250, 15, 10, 0));
    }

    [TestMethod]
    public void ProbeEcho_ClearsSuspicion_WithoutReinstall()
    {
        var monitor = new HookLivenessMonitor(requiredStrikes: 2, probeGraceTicks: 3);
        monitor.Sample(0, 0, 0, 0);
        monitor.Sample(50, 1, 0, 0);
        Assert.AreEqual(Action.Probe, monitor.Sample(100, 2, 0, 0));
        // A live hook echoes the tagged probe as activity.
        monitor.NoteHookActivity();
        Assert.AreEqual(Action.None, monitor.Sample(150, 3, 0, 0));
        Assert.AreEqual(0, monitor.Strikes);
        Assert.IsFalse(monitor.Verifying);
    }

    [TestMethod]
    public void HookActivity_ResetsStrikes()
    {
        var monitor = new HookLivenessMonitor(requiredStrikes: 3);
        monitor.Sample(0, 10, 10, 0);
        monitor.Sample(50, 11, 10, 0);
        monitor.Sample(100, 12, 10, 0);
        monitor.NoteHookActivity();
        Assert.AreEqual(Action.None, monitor.Sample(150, 13, 10, 0));
        Assert.AreEqual(0, monitor.Strikes);
    }

    [TestMethod]
    public void IdlePointer_NeverProbesOrReinstalls()
    {
        var monitor = new HookLivenessMonitor(requiredStrikes: 2);
        for (var i = 0; i < 10; i++) Assert.AreEqual(Action.None, monitor.Sample(i * 50, 10, 10, 0));
        Assert.AreEqual(0, monitor.Strikes);
    }

    [TestMethod]
    public void ButtonChangeWithoutHook_CountsAsStrike()
    {
        var monitor = new HookLivenessMonitor(requiredStrikes: 2, probeGraceTicks: 1);
        monitor.Sample(0, 10, 10, 0);
        Assert.AreEqual(Action.None, monitor.Sample(50, 10, 10, 1));
        Assert.AreEqual(Action.Probe, monitor.Sample(100, 10, 10, 0));
    }

    [TestMethod]
    public void Reinstall_IsRateLimitedByCooldown()
    {
        var monitor = new HookLivenessMonitor(requiredStrikes: 1, probeGraceTicks: 1, reinstallCooldownMilliseconds: 1000);
        monitor.Sample(0, 0, 0, 0);
        Assert.AreEqual(Action.Probe, monitor.Sample(50, 1, 0, 0));
        Assert.AreEqual(Action.Reinstall, monitor.Sample(100, 2, 0, 0));
        monitor.NoteReinstalled(100);
        Assert.AreEqual(Action.None, monitor.Sample(150, 3, 0, 0), "Within the cooldown, no new probe starts.");
        Assert.AreEqual(Action.Probe, monitor.Sample(1200, 4, 0, 0), "Cooldown elapsed and the hook is still silent.");
    }

    [TestMethod]
    public void OwnInjectedMovement_KeepsHookAlive()
    {
        // The relay's own SendInput moves also reach the hook. Activity is noted before tag filtering.
        var monitor = new HookLivenessMonitor(requiredStrikes: 2);
        monitor.Sample(0, 0, 0, 0);
        monitor.NoteHookActivity();
        Assert.AreEqual(Action.None, monitor.Sample(50, 5, 5, 0));
        monitor.NoteHookActivity();
        Assert.AreEqual(Action.None, monitor.Sample(100, 9, 9, 0));
        Assert.AreEqual(0, monitor.Strikes);
    }

    [TestMethod]
    public void InjectedCursorMovement_AloneNeverStorms_WhenHookIsAlive()
    {
        // Reproduces the assistive-input environment: the cursor moves every tick by injection,
        // and a live hook sees each move. The watchdog must stay quiet across many ticks.
        var monitor = new HookLivenessMonitor(requiredStrikes: 3, probeGraceTicks: 4);
        for (var i = 0; i < 200; i++)
        {
            monitor.NoteHookActivity(); // healthy hook observed the injected move
            Assert.AreEqual(Action.None, monitor.Sample(i * 50, i, i, 0));
        }
        Assert.AreEqual(0, monitor.Strikes);
    }
}
