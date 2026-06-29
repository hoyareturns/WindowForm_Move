using System.Diagnostics;
using System.Text.Json;

namespace WindowForm_Move;

public sealed record ProgramLaunchEntry(
    string Name,
    string FilePath,
    string WorkingDirectory,
    string Arguments,
    string LauncherPath = "",
    string Shortcut = "");

public sealed class ProgramLaunchStore
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private List<ProgramLaunchEntry> _entries;

    public ProgramLaunchStore()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowForm_Move");
        _filePath = Path.Combine(folder, "program-launchers.json");
        _entries = ReadEntries();
    }

    public IReadOnlyList<string> GetNames()
    {
        return _entries
            .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(GetDisplayName)
            .ToList();
    }

    public IReadOnlyList<(string Name, string Shortcut, Action Action)> GetHotkeyRegistrations()
    {
        return _entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Shortcut))
            .Select(entry => (
                entry.Name,
                entry.Shortcut,
                (Action)(() => Load(entry.Name, out _))))
            .ToList();
    }

    public string? Save(string preferredName, IWin32Window? owner)
    {
        var choice = MessageBox.Show(
            owner,
            "등록할 항목을 선택해 주세요.\n\n예: 파일 또는 프로그램\n아니요: 폴더\n취소: 등록 취소",
            "실행 항목 등록",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);
        if (choice == DialogResult.Cancel)
        {
            return null;
        }

        string selectedPath;
        if (choice == DialogResult.Yes)
        {
            using var fileDialog = new OpenFileDialog
            {
                Title = "실행할 프로그램 또는 파일 선택",
                Filter = "모든 파일|*.*",
                CheckFileExists = true,
                Multiselect = false,
                RestoreDirectory = true
            };
            if (fileDialog.ShowDialog(owner) != DialogResult.OK)
            {
                return null;
            }

            selectedPath = fileDialog.FileName;
        }
        else
        {
            using var folderDialog = new FolderBrowserDialog { Description = "등록할 폴더 선택" };
            if (folderDialog.ShowDialog(owner) != DialogResult.OK)
            {
                return null;
            }

            selectedPath = folderDialog.SelectedPath;
        }

        var name = string.IsNullOrWhiteSpace(preferredName)
            ? GetDefaultName(selectedPath)
            : ExtractEntryName(preferredName);
        name = MakeUniqueName(name, selectedPath);
        var entry = new ProgramLaunchEntry(
            name,
            selectedPath,
            Directory.Exists(selectedPath) ? selectedPath : Path.GetDirectoryName(selectedPath) ?? string.Empty,
            string.Empty,
            string.Empty);
        using (var form = new ProgramLaunchEditForm(entry))
        {
            if (form.ShowDialog(owner) != DialogResult.OK)
            {
                return null;
            }

            entry = form.CreateEntry();
        }
        if (!HotkeyParser.TryNormalize(entry.Shortcut, out var normalizedShortcut, out var shortcutError))
        {
            MessageBox.Show(owner, shortcutError, "단축키 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        entry = entry with { Shortcut = normalizedShortcut };
        if (_entries.Any(item => string.Equals(item.Name, entry.Name, StringComparison.CurrentCultureIgnoreCase)))
        {
            MessageBox.Show(owner, "같은 표시 이름이 이미 등록되어 있습니다.", "이름 중복", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        if (!ValidateShortcut(entry, owner))
        {
            return null;
        }

        _entries.RemoveAll(item => string.Equals(item.Name, name, StringComparison.CurrentCultureIgnoreCase));
        _entries.Add(entry);
        WriteEntries();
        return GetDisplayName(entry);
    }

    public bool Load(string name, out string? error)
    {
        error = null;
        var entry = Find(name);
        if (entry is null)
        {
            error = "실행할 항목을 선택해 주세요.";
            return false;
        }

        var isFolder = Directory.Exists(entry.FilePath);
        if (!File.Exists(entry.FilePath) && !isFolder)
        {
            error = $"등록된 파일을 찾을 수 없습니다.\n\n{entry.FilePath}";
            return false;
        }

        try
        {
            var launcherPath = entry.LauncherPath?.Trim().Trim('"') ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(launcherPath) && !File.Exists(launcherPath))
            {
                error = $"지정된 실행 도구를 찾을 수 없습니다.\n\n{launcherPath}";
                return false;
            }

            var fileName = isFolder || string.IsNullOrWhiteSpace(launcherPath)
                ? entry.FilePath
                : launcherPath;
            var arguments = entry.Arguments ?? string.Empty;
            if (!isFolder && !string.IsNullOrWhiteSpace(launcherPath))
            {
                var quotedTarget = $"\"{entry.FilePath}\"";
                arguments = arguments.Contains("{file}", StringComparison.OrdinalIgnoreCase)
                    ? arguments.Replace("{file}", quotedTarget, StringComparison.OrdinalIgnoreCase)
                    : string.Join(" ", new[] { arguments.Trim(), quotedTarget }.Where(value => value.Length > 0));
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = isFolder ? string.Empty : arguments,
                WorkingDirectory = Directory.Exists(entry.WorkingDirectory)
                    ? entry.WorkingDirectory
                    : Path.GetDirectoryName(entry.FilePath) ?? string.Empty,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public string? Edit(string name, IWin32Window? owner)
    {
        var index = _entries.FindIndex(entry =>
            string.Equals(entry.Name, ExtractEntryName(name), StringComparison.CurrentCultureIgnoreCase));
        if (index < 0)
        {
            return null;
        }

        using var form = new ProgramLaunchEditForm(_entries[index]);
        if (form.ShowDialog(owner) != DialogResult.OK)
        {
            return _entries[index].Name;
        }

        var edited = form.CreateEntry();
        if (!HotkeyParser.TryNormalize(edited.Shortcut, out var normalizedShortcut, out var shortcutError))
        {
            MessageBox.Show(owner, shortcutError, "단축키 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }
        edited = edited with { Shortcut = normalizedShortcut };
        if (form.SaveAsCopy)
        {
            edited = edited with { Name = MakeUniqueCopyName(edited.Name) };
            if (!ValidateShortcut(edited, owner))
            {
                return null;
            }
            _entries.Add(edited);
            WriteEntries();
            return GetDisplayName(edited);
        }

        if (_entries.Where((_, entryIndex) => entryIndex != index).Any(entry =>
                string.Equals(entry.Name, edited.Name, StringComparison.CurrentCultureIgnoreCase)))
        {
            MessageBox.Show(owner, "같은 표시 이름이 이미 등록되어 있습니다.", "이름 중복", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        if (!ValidateShortcut(edited, owner, index))
        {
            return null;
        }

        _entries[index] = edited;
        WriteEntries();
        return GetDisplayName(edited);
    }

    public bool Delete(string name)
    {
        var removed = _entries.RemoveAll(entry =>
            string.Equals(entry.Name, ExtractEntryName(name), StringComparison.CurrentCultureIgnoreCase));
        if (removed == 0)
        {
            return false;
        }

        WriteEntries();
        return true;
    }

    private ProgramLaunchEntry? Find(string name)
    {
        return _entries.FirstOrDefault(entry =>
            string.Equals(entry.Name, ExtractEntryName(name), StringComparison.CurrentCultureIgnoreCase));
    }

    private bool ValidateShortcut(ProgramLaunchEntry entry, IWin32Window? owner, int editingIndex = -1)
    {
        if (string.IsNullOrWhiteSpace(entry.Shortcut))
        {
            return true;
        }

        var duplicate = _entries.Where((_, index) => index != editingIndex)
            .FirstOrDefault(item => string.Equals(item.Shortcut, entry.Shortcut, StringComparison.OrdinalIgnoreCase));
        if (duplicate is null)
        {
            return true;
        }

        MessageBox.Show(
            owner,
            $"같은 단축키가 이미 등록되어 있습니다.\n\n{duplicate.Name}: {duplicate.Shortcut}",
            "단축키 중복",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        return false;
    }

    private static string GetDisplayName(ProgramLaunchEntry entry)
    {
        return string.IsNullOrWhiteSpace(entry.Shortcut)
            ? entry.Name
            : $"{entry.Name} : {entry.Shortcut}";
    }

    private static string ExtractEntryName(string value)
    {
        var text = value.Trim();
        var separatorIndex = text.LastIndexOf(" : ", StringComparison.Ordinal);
        return separatorIndex > 0 ? text[..separatorIndex].Trim() : text;
    }

    private string MakeUniqueName(string requestedName, string filePath)
    {
        var sameName = Find(requestedName);
        if (sameName is null || string.Equals(sameName.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
        {
            return requestedName;
        }

        var number = 2;
        while (Find($"{requestedName} {number}") is not null)
        {
            number++;
        }

        return $"{requestedName} {number}";
    }

    private static string GetDefaultName(string path)
    {
        if (Directory.Exists(path))
        {
            return new DirectoryInfo(path).Name;
        }

        return Path.GetFileNameWithoutExtension(path);
    }

    private string MakeUniqueCopyName(string requestedName)
    {
        var baseName = string.IsNullOrWhiteSpace(requestedName) ? "실행 항목 복사본" : requestedName.Trim();
        if (Find(baseName) is null)
        {
            return baseName;
        }

        var number = 2;
        while (Find($"{baseName} {number}") is not null)
        {
            number++;
        }

        return $"{baseName} {number}";
    }

    private List<ProgramLaunchEntry> ReadEntries()
    {
        try
        {
            return File.Exists(_filePath)
                ? JsonSerializer.Deserialize<List<ProgramLaunchEntry>>(File.ReadAllText(_filePath), _jsonOptions)
                  ?? new List<ProgramLaunchEntry>()
                : new List<ProgramLaunchEntry>();
        }
        catch
        {
            return new List<ProgramLaunchEntry>();
        }
    }

    private void WriteEntries()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(_entries, _jsonOptions));
    }
}
