using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.App.Tests;

[TestClass]
public sealed class SettingsTests
{
    private const string LegacyJson = """
        {"ToolbarPlacement":1,"Theme":2,"RememberLayout":false,
         "DefaultSourceWidth":1280,"DefaultSourceHeight":720,"DefaultZoom":3.5}
        """;

    [TestMethod]
    public void LegacyJson_PreservesExistingValuesAndDefaultsNewPreferences()
    {
        var settings = MagnifierSettingsStore.ReadJson(LegacyJson);

        Assert.AreEqual(SourceIndicatorPreference.Hidden, settings.SourceIndicatorPreference);
        Assert.AreEqual(LensDisplayMode.Normal, settings.LensDisplayMode);
        AssertLegacyValues(settings);
    }

    [DataTestMethod]
    [DataRow(999, 1, SourceIndicatorPreference.Hidden, LensDisplayMode.Compact)]
    [DataRow(2, -1, SourceIndicatorPreference.Always, LensDisplayMode.Normal)]
    [DataRow(-1, 999, SourceIndicatorPreference.Hidden, LensDisplayMode.Normal)]
    public void UnknownNewEnums_NormalizeOnlyTheirOwnFields(int indicator, int display,
        SourceIndicatorPreference expectedIndicator, LensDisplayMode expectedDisplay)
    {
        var json = LegacyJson.TrimEnd().TrimEnd('}') +
            $",\"SourceIndicatorPreference\":{indicator},\"LensDisplayMode\":{display}}}";

        var settings = MagnifierSettingsStore.ReadJson(json);

        Assert.AreEqual(expectedIndicator, settings.SourceIndicatorPreference);
        Assert.AreEqual(expectedDisplay, settings.LensDisplayMode);
        AssertLegacyValues(settings);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void DisplayPreferences_RoundTripIndependentlyOfRememberLayout(int indicator)
    {
        var original = new MagnifierSettings
        {
            RememberLayout = false,
            ToolbarPlacement = ToolbarPlacement.Bottom,
            SourceIndicatorPreference = (SourceIndicatorPreference)indicator,
            LensDisplayMode = LensDisplayMode.Compact
        };

        var loaded = MagnifierSettingsStore.ReadJson(System.Text.Json.JsonSerializer.Serialize(original));

        Assert.AreEqual(original, loaded);
    }

    [DataTestMethod]
    [DataRow("null")]
    [DataRow("{破損")]
    [DataRow("{\"ToolbarPlacement\":99,\"DefaultZoom\":3}")]
    [DataRow("{\"Theme\":99,\"DefaultZoom\":3}")]
    [DataRow("{\"DefaultSourceWidth\":0,\"DefaultZoom\":3}")]
    public void CorruptOrInvalidExistingSettings_KeepDefaultFallback(string json)
        => Assert.AreEqual(MagnifierSettings.Default, MagnifierSettingsStore.ReadJson(json));

    private static void AssertLegacyValues(MagnifierSettings settings)
    {
        Assert.IsTrue(settings.IsValid());
        Assert.AreEqual(ToolbarPlacement.Bottom, settings.ToolbarPlacement);
        Assert.AreEqual(ThemePreference.Dark, settings.Theme);
        Assert.IsFalse(settings.RememberLayout);
        Assert.AreEqual(1280, settings.DefaultSourceWidth);
        Assert.AreEqual(720, settings.DefaultSourceHeight);
        Assert.AreEqual(3.5, settings.DefaultZoom);
    }
}
