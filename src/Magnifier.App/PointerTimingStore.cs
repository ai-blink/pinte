using System.IO;
using System.Text.Json;
using Magnifier.Core;

namespace Magnifier.App;

// Developer trial profile for relay output timing. Kept apart from settings.json so a
// bad trial never damages the user's layout/theme settings.
internal sealed record PointerTimingProfile
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public long Revision { get; init; }
    public PointerTimingSettings Applied { get; init; } = PointerTimingSettings.Default;
    public PointerTimingSettings? Previous { get; init; }
    public PointerTimingSettings SlotA { get; init; } = PointerTimingSettings.Default;
    public PointerTimingSettings SlotB { get; init; } = PointerTimingSettings.Presets[1].Value;

    public static PointerTimingProfile Default { get; } = new();

    public bool IsValid() => SchemaVersion == CurrentSchemaVersion && Revision >= 0
        && Applied?.IsValid() == true && Previous?.IsValid() != false
        && SlotA?.IsValid() == true && SlotB?.IsValid() == true;
}

internal static class PointerTimingStore
{
    internal static string FilePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Magnifier", "pointer-timing.json");

    // Error is null when the profile was read or absent; otherwise the baseline is used
    // and the damaged file stays untouched until the user explicitly applies a value.
    public static (PointerTimingProfile Profile, string? Error) Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return (PointerTimingProfile.Default, null);
            return ReadJson(File.ReadAllText(FilePath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (PointerTimingProfile.Default, $"타이밍 프로필 읽기 실패 · 기준값 사용: {exception.Message}");
        }
    }

    internal static (PointerTimingProfile Profile, string? Error) ReadJson(string json)
    {
        try
        {
            var profile = JsonSerializer.Deserialize<PointerTimingProfile>(json);
            if (profile is null) return (PointerTimingProfile.Default, "타이밍 프로필이 비어 있음 · 기준값 사용");
            if (profile.SchemaVersion != PointerTimingProfile.CurrentSchemaVersion)
                return (PointerTimingProfile.Default, $"지원하지 않는 타이밍 프로필 버전 {profile.SchemaVersion} · 기준값 사용");
            return profile.IsValid() ? (profile, null)
                : (PointerTimingProfile.Default, "타이밍 프로필 값이 허용 범위를 벗어남 · 기준값 사용");
        }
        catch (JsonException)
        {
            return (PointerTimingProfile.Default, "타이밍 프로필 손상 · 기준값 사용");
        }
    }

    public static bool Save(PointerTimingProfile profile)
    {
        if (!profile.IsValid()) return false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(profile));
            File.Move(temporary, FilePath, true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
