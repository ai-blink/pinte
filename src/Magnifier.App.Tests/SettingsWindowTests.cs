using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Magnifier.App.Tests;

[TestClass]
public sealed class SettingsWindowTests
{
    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task Constructor_SelectsInitialPageOnlyAfterControlsExist(bool compact) => StaTest.Run(() =>
    {
        var capture = new NoDesktopCapture();
        var preferences = MagnifierSettings.Default with
        {
            LensDisplayMode = compact ? LensDisplayMode.Compact : LensDisplayMode.Normal,
            ToolbarPlacement = ToolbarPlacement.Bottom,
            SourceIndicatorPreference = SourceIndicatorPreference.Always
        };
        var window = new SettingsWindow(preferences, capture, canResizeLens: true);
        try
        {
            Assert.IsTrue(Control<RadioButton>(window, "AppearancePageButton").IsChecked == true);
            Assert.AreEqual(Visibility.Visible, Control<StackPanel>(window, "AppearancePanel").Visibility);
            Assert.AreEqual(Visibility.Collapsed, Control<StackPanel>(window, "DefaultsPanel").Visibility);
            Assert.AreEqual(Visibility.Collapsed, Control<StackPanel>(window, "AboutPanel").Visibility);
            Assert.AreEqual((int)preferences.LensDisplayMode, Control<ComboBox>(window, "LensDisplayModeBox").SelectedIndex);
            Assert.AreEqual(2, Control<ComboBox>(window, "SourceIndicatorBox").SelectedIndex);
            Assert.IsTrue(Control<RadioButton>(window, "BottomToolbarButton").IsChecked == true);
            Assert.IsTrue(Control<Button>(window, "ResizeLensButton").IsEnabled);
            Assert.IsFalse(window.IsVisible);
            Assert.AreEqual(0, capture.ExclusionCount, "창 생성 테스트는 Show나 HWND 생성 없이 수행한다.");
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task Navigation_AfterConstruction_SwitchesAllPages() => StaTest.Run(() =>
    {
        var window = new SettingsWindow(MagnifierSettings.Default, new NoDesktopCapture());
        try
        {
            foreach (var (button, panel) in new[]
            {
                ("DefaultsPageButton", "DefaultsPanel"),
                ("AboutPageButton", "AboutPanel"),
                ("AppearancePageButton", "AppearancePanel")
            })
            {
                Control<RadioButton>(window, button).IsChecked = true;
                foreach (var page in new[] { "AppearancePanel", "DefaultsPanel", "AboutPanel" })
                    Assert.AreEqual(page == panel ? Visibility.Visible : Visibility.Collapsed,
                        Control<StackPanel>(window, page).Visibility);
            }
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task UserSettingChange_IsNotBlockedByInitializationGuard() => StaTest.Run(() =>
    {
        var window = new SettingsWindow(MagnifierSettings.Default, new NoDesktopCapture());
        try
        {
            var changes = new List<MagnifierSettings>();
            window.SettingsChanged += changes.Add;

            Control<ComboBox>(window, "LensDisplayModeBox").SelectedIndex = (int)LensDisplayMode.Compact;

            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(LensDisplayMode.Compact, changes[0].LensDisplayMode);
            Assert.AreEqual(SourceIndicatorPreference.Hidden, changes[0].SourceIndicatorPreference);
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    });

    [TestMethod]
    public Task AboutPage_ShowsTheReleaseInformationalVersion() => StaTest.Run(() =>
    {
        var window = new SettingsWindow(MagnifierSettings.Default, new NoDesktopCapture());
        try
        {
            Assert.AreEqual("버전 0.1.0-beta.1", Control<TextBlock>(window, "VersionText").Text);
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    });

    private static T Control<T>(SettingsWindow window, string name) where T : FrameworkElement =>
        (T)window.FindName(name);
}
