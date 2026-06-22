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
    public string CaptureDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    public string CaptureFileNamePattern { get; set; } = "{date}_{time}";
    public bool ShowAnnotationSet { get; set; } = true;
    public bool ShowLayoutSet { get; set; } = true;
    public bool ShowProgramSet { get; set; } = true;
    public int ToolbarColorArgb { get; set; } = Color.FromArgb(45, 45, 45).ToArgb();
    public bool MatchTargetWindowColor { get; set; }
    public bool SharpIconRendering { get; set; }

    [JsonIgnore]
    public Color MarkerColor => Color.FromArgb(MarkerColorArgb);

    [JsonIgnore]
    public Color PenColor => Color.FromArgb(PenColorArgb);

    [JsonIgnore]
    public Color ArrowColor => PenColor;

    [JsonIgnore]
    public Color ToolbarColor => Color.FromArgb(ToolbarColorArgb);

    [JsonIgnore]
    public float ArrowWidth => PenWidth;

    public static AnnotationSettings Load()
    {
        try
        {
            var path = GetSettingsPath();
            var settings = File.Exists(path)
                ? JsonSerializer.Deserialize<AnnotationSettings>(File.ReadAllText(path)) ?? new AnnotationSettings()
                : new AnnotationSettings();
            settings.CaptureDirectory = string.IsNullOrWhiteSpace(settings.CaptureDirectory)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
                : settings.CaptureDirectory;
            settings.CaptureFileNamePattern = string.IsNullOrWhiteSpace(settings.CaptureFileNamePattern)
                ? "{date}_{time}"
                : settings.CaptureFileNamePattern;
            return settings;
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
