using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.Infrastructure.Tests;

[TestClass]
public sealed class KeyboardEscapeHookTests
{
    [TestMethod]
    public void PhysicalEscapeDown_IsAcceptedAsCancel()
    {
        Assert.IsTrue(WindowsLivePointerRelay.IsPhysicalEscapeKeyDown(0x0100, 0x1B, 0));
        Assert.IsTrue(WindowsLivePointerRelay.IsPhysicalEscapeKeyDown(0x0104, 0x1B, 0));
    }

    [TestMethod]
    public void InjectedEscapeDown_IsIgnored()
    {
        Assert.IsFalse(WindowsLivePointerRelay.IsPhysicalEscapeKeyDown(0x0100, 0x1B, 0x10));
    }

    [TestMethod]
    public void NonEscapeAndKeyUp_AreIgnored()
    {
        Assert.IsFalse(WindowsLivePointerRelay.IsPhysicalEscapeKeyDown(0x0100, 0x41, 0));
        Assert.IsFalse(WindowsLivePointerRelay.IsPhysicalEscapeKeyDown(0x0101, 0x1B, 0));
    }
}
