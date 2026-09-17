using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace Magnifier.App;

public enum ToolbarPlacement
{
    Top,
    Bottom
}

public enum ThemePreference
{
    System,
    Light,
    Dark
}

public enum SourceIndicatorPreference { Hidden = 0, Brief = 1, Always = 2 }
public enum LensDisplayMode { Normal = 0, Compact = 1 }

public sealed record MagnifierSettings
{
    public ToolbarPlacement ToolbarPlacement { get; init; } = ToolbarPlacement.Top;
    public ThemePreference Theme { get; init; } = ThemePreference.System;
    public SourceIndicatorPreference SourceIndicatorPreference { get; init; } = SourceIndicatorPreference.Hidden;
    public bool HideAppWindowsFromScreenCapture { get; init; }
    public LensDisplayMode LensDisplayMode { get; init; } = LensDisplayMode.Normal;
    public bool RememberLayout { get; init; } = true;
    public int DefaultSourceWidth { get; init; } = 960;
    public int DefaultSourceHeight { get; init; } = 540;
    public double DefaultZoom { get; init; } = 2;

    public static MagnifierSettings Default { get; } = new();

    public bool IsValid() => Enum.IsDefined(typeof(ToolbarPlacement), ToolbarPlacement)
        && Enum.IsDefined(typeof(ThemePreference), Theme)
        && Enum.IsDefined(SourceIndicatorPreference)
        && Enum.IsDefined(LensDisplayMode)
        && DefaultSourceWidth is >= 80 and <= 7680
        && DefaultSourceHeight is >= 60 and <= 4320
        && double.IsFinite(DefaultZoom) && DefaultZoom is >= 0.25 and <= 8;
}

internal static class MagnifierSettingsStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Magnifier", "settings.json");

    public static MagnifierSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return MagnifierSettings.Default;
            return ReadJson(File.ReadAllText(FilePath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return MagnifierSettings.Default;
        }
    }

    internal static MagnifierSettings ReadJson(string json)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<MagnifierSettings>(json);
            if (settings is null) return MagnifierSettings.Default;
            // 새 enum만 필드별로 복구해 기존 배치·테마·배율 선택을 보존한다.
            settings = settings with
            {
                SourceIndicatorPreference = Enum.IsDefined(settings.SourceIndicatorPreference)
                    ? settings.SourceIndicatorPreference : SourceIndicatorPreference.Hidden,
                LensDisplayMode = Enum.IsDefined(settings.LensDisplayMode)
                    ? settings.LensDisplayMode : LensDisplayMode.Normal
            };
            return settings.IsValid() ? settings : MagnifierSettings.Default;
        }
        catch (JsonException) { return MagnifierSettings.Default; }
    }

    public static bool Save(MagnifierSettings settings)
    {
        if (!settings.IsValid()) return false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings));
            File.Move(temporary, FilePath, true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

internal static class MagnifierTheme
{
    public static void Apply(ThemePreference preference)
    {
        // System follows WPF high-contrast visibility; a normal system palette stays light.
        var dark = preference == ThemePreference.Dark ||
            (preference == ThemePreference.System && SystemParameters.HighContrast);
        Application.Current.Resources["AppSurfaceBrush"] = CreateBrush(dark ? "#14171D" : "#F5F6F8");
        Application.Current.Resources["AppPanelBrush"] = CreateBrush(dark ? "#1D222A" : "#FFFFFF");
        Application.Current.Resources["AppToolbarBrush"] = CreateBrush(dark ? "#1D222A" : "#FFFFFF");
        Application.Current.Resources["AppCanvasBrush"] = CreateBrush(dark ? "#171D26" : "#F7F9FD");
        Application.Current.Resources["AppBorderBrush"] = CreateBrush(dark ? "#343C47" : "#DFE3E9");
        Application.Current.Resources["AppTextBrush"] = CreateBrush(dark ? "#F1F3F6" : "#17232F");
        Application.Current.Resources["AppMutedTextBrush"] = CreateBrush(dark ? "#ACB5C2" : "#65717D");
        Application.Current.Resources["AppHoverBrush"] = CreateBrush(dark ? "#282E38" : "#EDF0F4");
        Application.Current.Resources["AppPressedBrush"] = CreateBrush(dark ? "#343C47" : "#DFE5ED");
        Application.Current.Resources["AppAccentBrush"] = CreateBrush(dark ? "#A6C5FF" : "#286BE8");
        Application.Current.Resources["AppAccentHoverBrush"] = CreateBrush(dark ? "#BED4FF" : "#1F5FCE");
        Application.Current.Resources["AppAccentSoftBrush"] = CreateBrush(dark ? "#364662" : "#EAF1FF");
    }

    private static SolidColorBrush CreateBrush(string value)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)!);
        brush.Freeze();
        return brush;
    }
}
