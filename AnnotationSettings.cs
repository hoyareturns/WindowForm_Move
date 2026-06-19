using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindowForm_Move;

public sealed class AnnotationSettings
{
    public int MarkerColorArgb { get; set; } = Color.FromArgb(0, 120, 215).ToArgb();
    public int MarkerSize { get; set; } = 30;
    public int NextMarkerNumber { get; set; } = 1;
    public int PenColorArgb { get; set; } = Color.FromArgb(0, 170, 230).ToArgb();
    public float PenWidth { get; set; } = 3F;

    [JsonIgnore]
    public Color MarkerColor => Color.FromArgb(MarkerColorArgb);

    [JsonIgnore]
    public Color PenColor => Color.FromArgb(PenColorArgb);

    [JsonIgnore]
    public Color ArrowColor => PenColor;

    [JsonIgnore]
    public float ArrowWidth => PenWidth;

    public static AnnotationSettings Load()
    {
        try
        {
            var path = GetSettingsPath();
            return File.Exists(path)
                ? JsonSerializer.Deserialize<AnnotationSettings>(File.ReadAllText(path)) ?? new AnnotationSettings()
                : new AnnotationSettings();
        }
        catch
        {
            return new AnnotationSettings();
        }
    }

    public void Save()
    {
        try
        {
            var path = GetSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Drawing remains usable with the current in-memory settings.
        }
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowForm_Move",
            "annotation-settings.json");
    }
}
