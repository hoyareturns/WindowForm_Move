using System.Drawing.Imaging;

namespace WindowForm_Move;

public sealed class AnnotationManager : IDisposable
{
    private readonly AnnotationSettings _settings = AnnotationSettings.Load();
    private PresentationAnnotationForm? _session;

    public AnnotationManager()
    {
        _settings.NextMarkerNumber = 1;
    }

    public AnnotationTool ActiveTool { get; private set; }
    public Color MarkerColor => _settings.MarkerColor;
    public Color PenColor => _settings.PenColor;
    public int NextMarkerNumber => _settings.NextMarkerNumber;
    public bool ShowAnnotationSet => _settings.ShowAnnotationSet;
    public bool ShowLayoutSet => _settings.ShowLayoutSet;
    public bool ShowProgramSet => _settings.ShowProgramSet;
    public Color ToolbarColor => _settings.ToolbarColor;
    public bool MatchTargetWindowColor => _settings.MatchTargetWindowColor;
    public bool SharpIconRendering => _settings.SharpIconRendering;
    public event Action? ToolbarStateChanged;
    public event Action? PresentationStarted;
    public event Action? PresentationReady;
    public event Action? PresentationEnded;

    public void PositionPresentationNotice(Rectangle toolbarBounds)
    {
        _session?.PositionNotice(toolbarBounds);
    }

    public void AttachPresentationToolbar(Form toolbar)
    {
        _session?.AttachToolbar(toolbar);
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

    public void ChooseMarkerColor()
    {
        if (_session is not null && !_session.IsDisposed)
        {
            _session.ChooseMarkerColor();
            return;
        }

        using var dialog = new ColorDialog { Color = _settings.MarkerColor, FullOpen = true };
        var owner = Application.OpenForms.OfType<OverlayForm>().FirstOrDefault(form => form.Visible);
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
        var owner = Application.OpenForms.OfType<OverlayForm>().FirstOrDefault(form => form.Visible);
        if (dialog.ShowDialog(owner) == DialogResult.OK)
        {
            _settings.PenColorArgb = dialog.Color.ToArgb();
            _settings.Save();
            ToolbarStateChanged?.Invoke();
        }
    }

    public void UndoLast()
    {
        _session?.UndoLast();
    }

    public void ClearAll()
    {
        _session?.ClearAll();
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

        using var form = new AnnotationSettingsForm(_settings);
        if (form.ShowDialog() == DialogResult.OK)
        {
            form.ApplyTo(_settings);
            _settings.Save();
            ToolbarStateChanged?.Invoke();
        }
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
            var bounds = SystemInformation.VirtualScreen;
            frozenScreen = ScreenCapture.Capture(bounds);
            _session = new PresentationAnnotationForm(
                frozenScreen,
                bounds,
                _settings,
                CreateCapturePath);
            frozenScreen = null;
            _session.ToolChanged += tool =>
            {
                ActiveTool = tool;
                ToolbarStateChanged?.Invoke();
            };
            _session.SettingsChanged += () => ToolbarStateChanged?.Invoke();
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
                return;
            }

            using var result = frozen.Clone(selector.SelectedRegion, PixelFormat.Format32bppArgb);
            var outputPath = CreateCapturePath();
            result.Save(outputPath, ImageFormat.Png);
            var copied = TryCopyToClipboard(result);
            MessageBox.Show(
                copied
                    ? $"캡처를 저장하고 클립보드에 복사했습니다.\n\n{outputPath}"
                    : $"캡처를 저장했습니다.\n\n{outputPath}",
                "캡처 완료",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            ExplorerFolder.OpenIfNotOpen(Path.GetDirectoryName(outputPath)!);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "캡처 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            PresentationEnded?.Invoke();
        }
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
        var directory = string.IsNullOrWhiteSpace(_settings.CaptureDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            : Environment.ExpandEnvironmentVariables(_settings.CaptureDirectory.Trim());
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

    private static bool TryCopyToClipboard(Bitmap image)
    {
        try
        {
            using var clipboardImage = (Bitmap)image.Clone();
            Clipboard.SetImage(clipboardImage);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
