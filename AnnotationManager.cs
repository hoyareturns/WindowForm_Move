using System.Drawing.Imaging;

namespace WindowForm_Move;

public sealed class AnnotationManager : IDisposable
{
    public const string CustomTargetId = "custom-region";

    private readonly AnnotationSettings _settings = AnnotationSettings.Load();
    private PresentationAnnotationForm? _session;
    private string? _selectedTargetId;
    private Rectangle _customTargetBounds;
    private string? _operationStatus;

    public AnnotationManager()
    {
        _settings.NextMarkerNumber = 1;
        StartupManager.SetEnabled(_settings.AutoStartWithWindows);
    }

    public AnnotationTool ActiveTool { get; private set; }
    public bool IsPresentationActive => _session is not null && !_session.IsDisposed;
    public Color MarkerColor => _settings.MarkerColor;
    public Color PenColor => _settings.PenColor;
    public int NextMarkerNumber => _settings.NextMarkerNumber;
    public int MarkerSize => _settings.MarkerSize;
    public float PenWidth => _settings.PenWidth;
    public string MemoFontName => _settings.MemoFontName;
    public float MemoFontSize => _settings.MemoFontSize;
    public bool ShowAnnotationSet => _settings.ShowAnnotationSet;
    public bool ShowLayoutSet => _settings.ShowLayoutSet;
    public bool ShowProgramSet => _settings.ShowProgramSet;
    public bool ExpandAnnotationSetOnOpen => _settings.ExpandAnnotationSetOnOpen;
    public bool ExpandLayoutSetOnOpen => _settings.ExpandLayoutSetOnOpen;
    public bool ExpandProgramSetOnOpen => _settings.ExpandProgramSetOnOpen;
    public bool StartToolbarExpanded => _settings.StartToolbarExpanded;
    public Color ToolbarColor => _settings.ToolbarColor;
    public int ProgramComboWidth => _settings.ProgramComboWidth;
    public bool MatchTargetWindowColor => _settings.MatchTargetWindowColor;
    public bool SharpIconRendering => _settings.SharpIconRendering;
    public ButtonPreference GetButtonPreference(string id) => _settings.GetButtonPreference(id);
    public string GetButtonName(string id)
    {
        var preference = GetButtonPreference(id);
        return string.IsNullOrWhiteSpace(preference.DisplayName)
            ? ButtonCatalog.Get(id).DefaultName
            : preference.DisplayName.Trim();
    }
    public string GetButtonLabel(string id, string compactDefault)
    {
        var preference = GetButtonPreference(id);
        return string.IsNullOrWhiteSpace(preference.DisplayName)
            ? compactDefault
            : preference.DisplayName.Trim();
    }
    public event Action? ToolbarStateChanged;
    public event Action? ToolbarDefaultsChanged;
    public event Action? PresentationStarted;
    public event Action? PresentationReady;
    public event Action? PresentationEnded;
    public event Action? TargetSelectionStarted;
    public event Action? TargetSelectionEnded;

    public void PositionPresentationNotice(Rectangle toolbarBounds)
    {
        _session?.PositionNotice(toolbarBounds);
    }

    public void AttachPresentationToolbar(Form toolbar)
    {
        _session?.AttachToolbar(toolbar);
    }

    public IReadOnlyList<AnnotationTarget> GetAnnotationTargets()
    {
        var screens = Screen.AllScreens
            .OrderBy(GetMonitorNumber)
            .ToArray();
        var targets = screens
            .Select((screen, index) => new AnnotationTarget(
                screen.DeviceName,
                $"모니터 {GetMonitorNumber(screen, index + 1)} - {GetMonitorPositionName(screen)} ({screen.Bounds.Width} x {screen.Bounds.Height})",
                screen.Bounds))
            .ToList();
        var customName = _customTargetBounds.IsEmpty
            ? "직접 영역 선택..."
            : $"직접 영역 ({_customTargetBounds.Width} x {_customTargetBounds.Height})";
        targets.Add(new AnnotationTarget(CustomTargetId, customName, _customTargetBounds, true));
        return targets;
    }

    public string SelectedTargetId
    {
        get
        {
            EnsureSelectedTarget();
            return _selectedTargetId!;
        }
    }

    public string SelectedTargetDisplayName =>
        GetAnnotationTargets().FirstOrDefault(target => target.Id == SelectedTargetId)?.DisplayName
        ?? "대상 없음";

    public Rectangle SelectedTargetBounds => ResolveSelectedTargetBounds();
    public string? OperationStatus => _operationStatus;

    public void SuggestTarget(Screen screen)
    {
        if (IsPresentationActive || Screen.AllScreens.All(candidate => candidate.DeviceName != screen.DeviceName))
        {
            return;
        }

        _selectedTargetId = screen.DeviceName;
        ToolbarStateChanged?.Invoke();
    }

    public bool SelectTarget(string targetId)
    {
        if (IsPresentationActive)
        {
            return false;
        }

        var selected = false;
        if (targetId != CustomTargetId)
        {
            var screen = Screen.AllScreens.FirstOrDefault(candidate => candidate.DeviceName == targetId);
            if (screen is null)
            {
                return false;
            }

            _selectedTargetId = screen.DeviceName;
            selected = true;
        }
        else
        {
            selected = SelectCustomTarget();
        }

        if (!selected)
        {
            return false;
        }

        StartPresentation(AnnotationTool.None);
        return IsPresentationActive;
    }

    public void EndPresentation()
    {
        _session?.Close();
    }

    public void OpenCaptureFolder()
    {
        try
        {
            var directory = GetCaptureDirectory();
            Directory.CreateDirectory(directory);
            var opened = ExplorerFolder.OpenIfNotOpen(directory);
            SetOperationStatus(opened
                ? $"{DateTime.Now:HH:mm:ss} 캡처 폴더 열기"
                : $"{DateTime.Now:HH:mm:ss} 캡처 폴더를 열지 못했습니다.");
        }
        catch (Exception exception)
        {
            SetOperationStatus($"{DateTime.Now:HH:mm:ss} 폴더 오류: {exception.Message}");
        }
    }

    public void ToggleTool(AnnotationTool tool)
    {
        if (_session is null || _session.IsDisposed)
        {
            StartPresentation(tool);
            return;
        }

        _session.SetTool(ActiveTool == tool ? AnnotationTool.None : tool);
    }

    public void SetActiveTool(AnnotationTool tool)
    {
        if (tool == AnnotationTool.None)
        {
            _session?.Close();
        }
        else
        {
            ToggleTool(tool);
        }
    }

    public void SetNextMarkerNumber(int number)
    {
        var nextNumber = Math.Clamp(number, 1, 9999);
        if (_settings.NextMarkerNumber == nextNumber)
        {
            return;
        }

        _settings.NextMarkerNumber = nextNumber;
        _session?.SyncSettings();
        ToolbarStateChanged?.Invoke();
    }

    public void SetMarkerSize(int size)
    {
        _settings.MarkerSize = Math.Clamp(size, 4, 60);
        SaveDrawingSettings();
    }

    public void SetPenWidth(float width)
    {
        _settings.PenWidth = Math.Clamp(width, 1F, 20F);
        SaveDrawingSettings();
    }

    public void SetMemoFont(string fontName, float fontSize)
    {
        _settings.MemoFontName = string.IsNullOrWhiteSpace(fontName) ? "Segoe UI Semibold" : fontName;
        _settings.MemoFontSize = Math.Clamp(fontSize, 8F, 36F);
        SaveDrawingSettings();
    }

    public void ChooseMarkerColor()
    {
        if (_session is not null && !_session.IsDisposed)
        {
            _session.ChooseMarkerColor();
            return;
        }

        using var dialog = new ColorDialog { Color = _settings.MarkerColor, FullOpen = true };
        var owner = GetColorDialogOwner();
        if (dialog.ShowDialog(owner) == DialogResult.OK)
        {
            _settings.MarkerColorArgb = dialog.Color.ToArgb();
            _settings.Save();
            ToolbarStateChanged?.Invoke();
        }
    }

    public void ChoosePenColor()
    {
        if (_session is not null && !_session.IsDisposed)
        {
            _session.ChoosePenColor();
            return;
        }

        using var dialog = new ColorDialog { Color = _settings.PenColor, FullOpen = true };
        var owner = GetColorDialogOwner();
        if (dialog.ShowDialog(owner) == DialogResult.OK)
        {
            _settings.PenColorArgb = dialog.Color.ToArgb();
            _settings.Save();
            ToolbarStateChanged?.Invoke();
        }
    }

    private static IWin32Window? GetColorDialogOwner()
    {
        var owner = Application.OpenForms
            .OfType<AnnotationToolForm>()
            .FirstOrDefault(form => form.Visible && !form.IsDisposed)
            ?? Application.OpenForms
                .OfType<OverlayForm>()
                .FirstOrDefault(form => form.Visible && !form.IsDisposed)
            ?? Form.ActiveForm;

        owner?.BringToFront();
        return owner;
    }

    public void UndoLast()
    {
        _session?.UndoLast();
    }

    public void ClearAll()
    {
        _session?.ClearAll();
    }

    private void SaveDrawingSettings()
    {
        _settings.Save();
        _session?.SyncSettings();
        ToolbarStateChanged?.Invoke();
    }

    public void CaptureSelectedRegion()
    {
        if (_session is not null && !_session.IsDisposed)
        {
            _session.CaptureSelectedRegion();
            return;
        }

        CaptureFrozenScreen();
    }

    public void ShowSettings()
    {
        if (_session is not null && !_session.IsDisposed)
        {
            _session.ShowAnnotationSettings();
            return;
        }

        using var form = new AnnotationSettingsForm(_settings, ApplySettings);
        form.ShowDialog();
    }

    private bool ApplySettings()
    {
        if (!StartupManager.SetEnabled(_settings.AutoStartWithWindows))
        {
            MessageBox.Show(
                "Windows 자동 실행 설정을 변경하지 못했습니다.",
                "Smart_Window 설정",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        _settings.Save();
        _session?.SyncSettings();
        ToolbarDefaultsChanged?.Invoke();
        ToolbarStateChanged?.Invoke();
        return true;
    }

    public void Dispose()
    {
        if (_session is not null)
        {
            _session.FormClosed -= SessionClosed;
            _session.Dispose();
            _session = null;
        }
    }

    private void StartPresentation(AnnotationTool initialTool)
    {
        PresentationStarted?.Invoke();
        HideHostOverlays();
        Application.DoEvents();

        Bitmap? frozenScreen = null;
        try
        {
            var bounds = ResolveSelectedTargetBounds();
            frozenScreen = ScreenCapture.Capture(bounds);
            _session = new PresentationAnnotationForm(
                frozenScreen,
                bounds,
                _selectedTargetId == CustomTargetId,
                _settings,
                CreateCapturePath);
            frozenScreen = null;
            _session.ToolChanged += tool =>
            {
                ActiveTool = tool;
                ToolbarStateChanged?.Invoke();
            };
            _session.SettingsChanged += () =>
            {
                ToolbarDefaultsChanged?.Invoke();
                ToolbarStateChanged?.Invoke();
            };
            _session.StatusChanged += SetOperationStatus;
            _session.FormClosed += SessionClosed;
            ActiveTool = initialTool;
            _session.SetTool(initialTool);
            _session.Show();
            _session.Activate();
            PresentationReady?.Invoke();
            ToolbarStateChanged?.Invoke();
        }
        catch (Exception exception)
        {
            frozenScreen?.Dispose();
            _session?.Dispose();
            _session = null;
            ActiveTool = AnnotationTool.None;
            MessageBox.Show(exception.Message, "프레젠테이션 도구 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            PresentationEnded?.Invoke();
        }
    }

    private void CaptureFrozenScreen()
    {
        PresentationStarted?.Invoke();
        HideHostOverlays();
        Application.DoEvents();
        var bounds = SystemInformation.VirtualScreen;
        try
        {
            using var frozen = ScreenCapture.Capture(bounds);
            using var selectorImage = (Bitmap)frozen.Clone();
            using var selector = new CaptureSelectionForm(selectorImage, bounds);
            if (selector.ShowDialog() != DialogResult.OK)
            {
                SetOperationStatus($"{DateTime.Now:HH:mm:ss} 캡처 취소");
                return;
            }

            using var result = frozen.Clone(selector.SelectedRegion, PixelFormat.Format32bppArgb);
            var outputPath = CreateCapturePath();
            result.Save(outputPath, ImageFormat.Png);
            var copied = TryCopyToClipboard(result);
            SetOperationStatus(copied
                ? $"{DateTime.Now:HH:mm:ss} 저장·클립보드 복사 완료: {Path.GetFileName(outputPath)}"
                : $"{DateTime.Now:HH:mm:ss} 저장 완료 / 클립보드 복사 실패: {Path.GetFileName(outputPath)}");
        }
        catch (Exception exception)
        {
            SetOperationStatus($"{DateTime.Now:HH:mm:ss} 캡처 오류: {exception.Message}");
        }
        finally
        {
            PresentationEnded?.Invoke();
        }
    }

    private bool SelectCustomTarget()
    {
        TargetSelectionStarted?.Invoke();
        HideHostOverlays();
        Application.DoEvents();
        var virtualBounds = SystemInformation.VirtualScreen;
        try
        {
            using var frozen = ScreenCapture.Capture(virtualBounds);
            using var selectorImage = (Bitmap)frozen.Clone();
            using var selector = new CaptureSelectionForm(selectorImage, virtualBounds);
            if (selector.ShowDialog() != DialogResult.OK)
            {
                return false;
            }

            var selected = selector.SelectedScreenRegion;
            if (selected.Width < 5 || selected.Height < 5)
            {
                return false;
            }

            _customTargetBounds = selected;
            _selectedTargetId = CustomTargetId;
            ToolbarStateChanged?.Invoke();
            return true;
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "마킹 대상 선택 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        finally
        {
            TargetSelectionEnded?.Invoke();
        }
    }

    private Rectangle ResolveSelectedTargetBounds()
    {
        EnsureSelectedTarget();
        if (_selectedTargetId == CustomTargetId && !_customTargetBounds.IsEmpty)
        {
            return _customTargetBounds;
        }

        var screen = Screen.AllScreens.FirstOrDefault(candidate => candidate.DeviceName == _selectedTargetId)
                     ?? Screen.PrimaryScreen
                     ?? Screen.AllScreens.First();
        _selectedTargetId = screen.DeviceName;
        return screen.Bounds;
    }

    private void EnsureSelectedTarget()
    {
        if (_selectedTargetId == CustomTargetId && !_customTargetBounds.IsEmpty)
        {
            return;
        }

        if (_selectedTargetId is not null &&
            Screen.AllScreens.Any(screen => screen.DeviceName == _selectedTargetId))
        {
            return;
        }

        _selectedTargetId = (Screen.PrimaryScreen ?? Screen.AllScreens.First()).DeviceName;
    }

    private static int GetMonitorNumber(Screen screen)
    {
        return GetMonitorNumber(screen, int.MaxValue);
    }

    private static int GetMonitorNumber(Screen screen, int fallback)
    {
        var digits = new string(screen.DeviceName.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var number) ? number : fallback;
    }

    private static string GetMonitorPositionName(Screen screen)
    {
        var primary = Screen.PrimaryScreen ?? Screen.AllScreens.First();
        if (screen.DeviceName == primary.DeviceName)
        {
            return "주 모니터";
        }

        var primaryCenter = new Point(
            primary.Bounds.Left + primary.Bounds.Width / 2,
            primary.Bounds.Top + primary.Bounds.Height / 2);
        var center = new Point(
            screen.Bounds.Left + screen.Bounds.Width / 2,
            screen.Bounds.Top + screen.Bounds.Height / 2);
        var horizontal = center.X < primaryCenter.X ? "왼쪽" : center.X > primaryCenter.X ? "오른쪽" : string.Empty;
        var vertical = center.Y < primaryCenter.Y ? "위" : center.Y > primaryCenter.Y ? "아래" : string.Empty;
        return string.IsNullOrEmpty(horizontal + vertical) ? "보조 모니터" : horizontal + vertical;
    }

    private void SessionClosed(object? sender, FormClosedEventArgs e)
    {
        if (_session is not null)
        {
            _session.FormClosed -= SessionClosed;
            _session.Dispose();
            _session = null;
        }

        ActiveTool = AnnotationTool.None;
        ToolbarStateChanged?.Invoke();
        PresentationEnded?.Invoke();
    }

    private static void HideHostOverlays()
    {
        foreach (var overlay in Application.OpenForms.OfType<OverlayForm>())
        {
            overlay.Hide();
        }
    }

    private string CreateCapturePath()
    {
        var directory = GetCaptureDirectory();
        Directory.CreateDirectory(directory);

        var now = DateTime.Now;
        var pattern = string.IsNullOrWhiteSpace(_settings.CaptureFileNamePattern)
            ? "{date}_{time}"
            : _settings.CaptureFileNamePattern.Trim();
        var fileName = pattern
            .Replace("{datetime}", now.ToString("yyyyMMdd_HHmmss"), StringComparison.OrdinalIgnoreCase)
            .Replace("{date}", now.ToString("yyyyMMdd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{time}", now.ToString("HHmmss"), StringComparison.OrdinalIgnoreCase);

        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidCharacter, '_');
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = now.ToString("yyyyMMdd_HHmmss");
        }

        if (fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName[..^4];
        }

        var path = Path.Combine(directory, fileName + ".png");
        var suffix = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(directory, $"{fileName}_{suffix++}.png");
        }

        return path;
    }

    private string GetCaptureDirectory()
    {
        return string.IsNullOrWhiteSpace(_settings.CaptureDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            : Environment.ExpandEnvironmentVariables(_settings.CaptureDirectory.Trim());
    }

    private void SetOperationStatus(string status)
    {
        _operationStatus = status;
        ToolbarStateChanged?.Invoke();
    }

    private static bool TryCopyToClipboard(Bitmap image)
    {
        try
        {
            using var clipboardImage = (Bitmap)image.Clone();
            Clipboard.SetDataObject(clipboardImage, true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
