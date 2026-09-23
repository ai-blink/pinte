using System.IO;
using System.Text.Json;
using Magnifier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.App.Tests;

[TestClass]
public sealed class PointerTimingStoreTests
{
    [TestMethod]
    public void RoundTrip_PreservesRevisionAppliedPreviousAndSlots()
    {
        var original = PointerTimingProfile.Default with
        {
            Revision = 7,
            Applied = new(50, 20, 30, 0),
            Previous = PointerTimingSettings.Default,
            SlotA = new(40, 15, 25, 5),
            SlotB = new(0, 0, 0, 0)
        };
        var (loaded, error) = PointerTimingStore.ReadJson(JsonSerializer.Serialize(original));
        Assert.IsNull(error);
        Assert.AreEqual(original, loaded);
    }

    [DataTestMethod]
    [DataRow("{not json", "손상")]
    [DataRow("""{"SchemaVersion":2,"Revision":1}""", "버전")]
    [DataRow("""{"SchemaVersion":1,"Revision":1,"Applied":{"ArrivalMs":999,"MinimumHoldMs":0,"PostReleaseMs":0,"BetweenGesturesMs":0}}""", "범위")]
    [DataRow("""{"SchemaVersion":1,"Revision":-1}""", "범위")]
    public void InvalidProfile_FallsBackToBaselineAndReportsWhy(string json, string expected)
    {
        var (loaded, error) = PointerTimingStore.ReadJson(json);
        Assert.AreEqual(PointerTimingProfile.Default, loaded);
        StringAssert.Contains(error, expected);
    }

    [TestMethod]
    public void Save_WritesAtomicallyAndRefusesInvalidProfileWithoutTouchingFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "magnifier-timing-" + Guid.NewGuid().ToString("N"));
        var previous = PointerTimingStore.FilePath;
        PointerTimingStore.FilePath = Path.Combine(directory, "pointer-timing.json");
        try
        {
            var good = PointerTimingProfile.Default with { Revision = 1, Applied = new(20, 10, 15, 0) };
            Assert.IsTrue(PointerTimingStore.Save(good));
            Assert.IsFalse(PointerTimingStore.Save(good with { Applied = new(-1, 0, 0, 0) }));
            var (loaded, error) = PointerTimingStore.Load();
            Assert.IsNull(error);
            Assert.AreEqual(good, loaded);
            Assert.IsFalse(File.Exists(PointerTimingStore.FilePath + ".tmp"));
        }
        finally
        {
            PointerTimingStore.FilePath = previous;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
