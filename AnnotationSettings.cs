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
    public string MemoFontName { get; set; } = "Segoe UI Semibold";
    public float MemoFontSize { get; set; } = 10F;
    public string CaptureDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    public string CaptureFileNamePattern { get; set; } = "{date}_{time}";
    public bool ShowAnnotationSet { get; set; } = true;
    public bool ShowLayoutSet { get; set; } = true;
    public bool ShowProgramSet { get; set; } = true;
    public bool ExpandAnnotationSetOnOpen { get; set; }
    public bool ExpandLayoutSetOnOpen { get; set; }
    public bool ExpandProgramSetOnOpen { get; set; }
    public bool StartToolbarExpanded { get; set; }
    public int ToolbarColorArgb { get; set; } = Color.FromArgb(45, 45, 45).ToArgb();
    public bool MatchTargetWindowColor { get; set; }
    public bool SharpIconRendering { get; set; }
    public bool AutoStartWithWindows { get; set; }
    public int ProgramComboWidth { get; set; } = 110;
    public Dictionary<string, ButtonPreference> ButtonPreferences { get; set; } = new();

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
            settings.ProgramComboWidth = Math.Clamp(settings.ProgramComboWidth, 70, 320);
            settings.EnsureButtonPreferences();
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
            EnsureButtonPreferences();
            var path = GetSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Drawing remains usable with the current in-memory settings.
        }
    }

    public ButtonPreference GetButtonPreference(string id)
    {
        EnsureButtonPreferences();
        return ButtonPreferences[id];
    }

    public void EnsureButtonPreferences()
    {
        ButtonPreferences ??= new Dictionary<string, ButtonPreference>();
        foreach (var definition in ButtonCatalog.All)
        {
            if (!ButtonPreferences.TryGetValue(definition.Id, out var preference) || preference is null)
            {
                preference = new ButtonPreference();
                ButtonPreferences[definition.Id] = preference;
            }

            if (definition.Required)
            {
                preference.Visible = true;
            }

            preference.DisplayName ??= string.Empty;
            preference.Shortcut ??= string.Empty;
        }
        ProgramComboWidth = Math.Clamp(ProgramComboWidth, 70, 320);
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowForm_Move",
            "annotation-settings.json");
    }
}
