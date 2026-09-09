using System.IO;
using System.Text.Json;
using Magnifier.Core;

namespace Magnifier.App;

internal sealed record LensLayout(ScreenRegion Source, ScreenRegion Lens, double Zoom);

internal static class LensLayoutStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Magnifier", "layout.json");

    public static LensLayout? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var layout = JsonSerializer.Deserialize<LensLayout>(File.ReadAllText(FilePath));
            return layout is { Source.Width: > 0, Source.Height: > 0, Lens.Width: > 0, Lens.Height: > 0 }
                && double.IsFinite(layout.Zoom) && layout.Zoom is >= 0.25 and <= 8 ? layout : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        { return null; }
    }

    public static bool Save(LensLayout layout)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(layout));
            File.Move(FilePath + ".tmp", FilePath, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}
