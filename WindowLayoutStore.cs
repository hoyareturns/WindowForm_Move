using System.Text.Json;

namespace WindowForm_Move;

public sealed record WindowLayoutEntry(
    string ProcessName,
    string Title,
    int X,
    int Y,
    int Width,
    int Height,
    bool Maximized,
    string ScreenDeviceName,
    int ScreenX,
    int ScreenY,
    int ScreenWidth,
    int ScreenHeight,
    string ExecutablePath);

public sealed record WindowLayoutProfile(string Name, List<WindowLayoutEntry> Windows);

public sealed class WindowLayoutStore
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private List<WindowLayoutProfile> _profiles;

    public WindowLayoutStore()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowForm_Move");
        _filePath = Path.Combine(folder, "window-layouts.json");
        _profiles = ReadProfiles();
    }

    public IReadOnlyList<string> GetNames()
    {
        return _profiles
            .Select(profile => profile.Name)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public bool Save(string name)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var windows = WindowController.CaptureWindowLayout().ToList();
        if (windows.Count == 0)
        {
            return false;
        }

        _profiles.RemoveAll(profile => string.Equals(profile.Name, name, StringComparison.CurrentCultureIgnoreCase));
        _profiles.Add(new WindowLayoutProfile(name, windows));
        WriteProfiles();
        return true;
    }

    public bool Load(string name, bool launchMissingPrograms)
    {
        var profile = Find(name);
        return profile is not null &&
               WindowController.RestoreWindowLayout(profile.Windows, launchMissingPrograms) > 0;
    }

    public bool Delete(string name)
    {
        var removed = _profiles.RemoveAll(profile =>
            string.Equals(profile.Name, name.Trim(), StringComparison.CurrentCultureIgnoreCase));
        if (removed == 0)
        {
            return false;
        }

        WriteProfiles();
        return true;
    }

    private WindowLayoutProfile? Find(string name)
    {
        return _profiles.FirstOrDefault(profile =>
            string.Equals(profile.Name, name.Trim(), StringComparison.CurrentCultureIgnoreCase));
    }

    private List<WindowLayoutProfile> ReadProfiles()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new List<WindowLayoutProfile>();
            }

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<WindowLayoutProfile>>(json, _jsonOptions)
                   ?? new List<WindowLayoutProfile>();
        }
        catch
        {
            return new List<WindowLayoutProfile>();
        }
    }

    private void WriteProfiles()
    {
        var folder = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(_profiles, _jsonOptions));
    }
}
