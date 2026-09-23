using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Magnifier.Infrastructure.WindowsLivePointerRelay;

namespace Magnifier.Infrastructure.Tests;

[TestClass]
public sealed class MouseInputRoutingTests
{
    [DataTestMethod]
    [DataRow(1u, 0)]
    [DataRow(3u, 0)]
    [DataRow(1u, 0x50524F42)]
    [DataRow(3u, 0x50524F42)]
    public void AssistiveInput_UpdatesHeldButtonsAndKeepsAbsoluteMovement(uint flags, int tag)
    {
        var buttons = 0;
        Assert.AreEqual(MouseInputOrigin.Assistive, ObserveMouseButtons(0x201, flags, tag, 0, ref buttons));
        Assert.AreEqual(1, buttons, "외부 주입 Down을 보존해야 합니다.");
        Assert.AreEqual(MouseInputOrigin.Assistive, ObserveMouseButtons(0x200, flags, tag, 0, ref buttons));
        Assert.AreEqual(1, buttons);
        Assert.AreEqual(MouseInputOrigin.Assistive, ObserveMouseButtons(0x202, flags, tag, 0, ref buttons));
        Assert.AreEqual(0, buttons, "외부 주입 Up을 보존해야 재개할 수 있습니다.");
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(31)]
    public void OwnMoveDownUp_DoNotChangeObservedButtons(int initialButtons)
    {
        var buttons = initialButtons;
        foreach (var kind in new uint[] { 0x200, 0x201, 0x202 })
        {
            Assert.AreEqual(MouseInputOrigin.RelayOutput,
                ObserveMouseButtons(kind, 1, WindowsPointerInput.InjectionTag, 0, ref buttons));
            Assert.AreEqual(initialButtons, buttons);
        }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void InterleavedSameMessage_OnlyOwnTagIsExcluded(bool ownFirst)
    {
        // No cursor/time heuristic participates: identical coordinates and event timing
        // cannot make an assistive input become relay output, in either ordering.
        var tags = ownFirst
            ? new nint[] { WindowsPointerInput.InjectionTag, 0 }
            : new nint[] { 0, WindowsPointerInput.InjectionTag };
        var buttons = 0;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var observed = new List<uint>();
            foreach (var kind in new uint[] { 0x201, 0x200, 0x202 })
            {
                foreach (var tag in tags)
                {
                    var previous = buttons;
                    var origin = ObserveMouseButtons(kind, 1, tag, 0, ref buttons);
                    if (origin == MouseInputOrigin.RelayOutput)
                    {
                        Assert.AreEqual(previous, buttons);
                        continue;
                    }
                    Assert.AreEqual(MouseInputOrigin.Assistive, origin);
                    Assert.IsFalse(IsDuplicateLeftButtonDown(kind, previous));
                    observed.Add(kind);
                }
                Assert.AreEqual(kind == 0x202 ? 0 : 1, buttons);
            }
            CollectionAssert.AreEqual(new uint[] { 0x201, 0x200, 0x202 }, observed);
        }
    }

    [TestMethod]
    public void PhysicalButtons_KeepIndependentButtonBits()
    {
        var buttons = 0;
        Assert.AreEqual(MouseInputOrigin.Physical, ObserveMouseButtons(0x201, 0, 0, 0, ref buttons));
        ObserveMouseButtons(0x204, 0, 0, 0, ref buttons);
        ObserveMouseButtons(0x207, 0, 0, 0, ref buttons);
        ObserveMouseButtons(0x20B, 0, 0, 1u << 16, ref buttons);
        ObserveMouseButtons(0x20B, 0, 0, 2u << 16, ref buttons);
        Assert.AreEqual(31, buttons);
        ObserveMouseButtons(0x202, 0, 0, 0, ref buttons);
        Assert.AreEqual(30, buttons);
        ObserveMouseButtons(0x205, 0, 0, 0, ref buttons);
        ObserveMouseButtons(0x208, 0, 0, 0, ref buttons);
        ObserveMouseButtons(0x20C, 0, 0, 1u << 16, ref buttons);
        ObserveMouseButtons(0x20C, 0, 0, 2u << 16, ref buttons);
        Assert.AreEqual(0, buttons);
    }
}
