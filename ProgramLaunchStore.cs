using System.Diagnostics;
using System.Text.Json;

namespace WindowForm_Move;

public sealed record ProgramLaunchEntry(
    string Name,
    string FilePath,
    string WorkingDirectory,
    string Arguments,
    string LauncherPath = "");

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
            .Select(entry => entry.Name)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
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
            : preferredName.Trim();
        name = MakeUniqueName(name, selectedPath);
        var entry = new ProgramLaunchEntry(
            name,
            selectedPath,
            Directory.Exists(selectedPath) ? selectedPath : Path.GetDirectoryName(selectedPath) ?? string.Empty,
            string.Empty,
            string.Empty);
        _entries.RemoveAll(item => string.Equals(item.Name, name, StringComparison.CurrentCultureIgnoreCase));
        _entries.Add(entry);
        WriteEntries();
        return name;
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
            string.Equals(entry.Name, name.Trim(), StringComparison.CurrentCultureIgnoreCase));
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
        if (form.SaveAsCopy)
        {
            edited = edited with { Name = MakeUniqueCopyName(edited.Name) };
            _entries.Add(edited);
            WriteEntries();
            return edited.Name;
        }

        if (_entries.Where((_, entryIndex) => entryIndex != index).Any(entry =>
                string.Equals(entry.Name, edited.Name, StringComparison.CurrentCultureIgnoreCase)))
        {
            MessageBox.Show(owner, "같은 표시 이름이 이미 등록되어 있습니다.", "이름 중복", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        _entries[index] = edited;
        WriteEntries();
        return edited.Name;
    }

    public bool Delete(string name)
    {
        var removed = _entries.RemoveAll(entry =>
            string.Equals(entry.Name, name.Trim(), StringComparison.CurrentCultureIgnoreCase));
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
            string.Equals(entry.Name, name.Trim(), StringComparison.CurrentCultureIgnoreCase));
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
