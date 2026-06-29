using WindowForm_Move;

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

Assert(
    HotkeyParser.TryNormalize("control + shift + f9", out var normalized, out _) &&
    normalized == "Ctrl+Shift+F9",
    "단축키 정규화 실패");

var preferences = ButtonCatalog.All.ToDictionary(
    definition => definition.Id,
    _ => new ButtonPreference());
preferences["main.move_left"].Shortcut = "Ctrl+Alt+L";
preferences["main.move_right"].Shortcut = "control + alt + l";
preferences["main.close"].Visible = false;

var validation = ButtonSettingsValidator.Validate(preferences);
Assert(!validation.Succeeded, "중복 단축키를 허용했습니다.");
Assert(validation.ConflictingButtonIds.Contains("main.move_left"), "첫 번째 충돌 버튼이 누락되었습니다.");
Assert(validation.ConflictingButtonIds.Contains("main.move_right"), "두 번째 충돌 버튼이 누락되었습니다.");
Assert(preferences["main.close"].Visible, "필수 버튼 숨김이 복원되지 않았습니다.");

preferences["main.move_right"].Shortcut = "Ctrl+Alt+R";
validation = ButtonSettingsValidator.Validate(preferences);
Assert(validation.Succeeded, validation.ErrorMessage);
Assert(preferences["main.move_left"].Shortcut == "Ctrl+Alt+L", "정규화된 단축키가 저장되지 않았습니다.");

static IEnumerable<Control> Descendants(Control parent)
{
    foreach (Control child in parent.Controls)
    {
        yield return child;
        foreach (var descendant in Descendants(child))
        {
            yield return descendant;
        }
    }
}

static int AbsoluteRight(Control control)
{
    var right = control.Right;
    for (var parent = control.Parent; parent is not null; parent = parent.Parent)
    {
        right += parent.Left;
    }
    return right;
}

static int AbsoluteLeft(Control control)
{
    var left = control.Left;
    for (var parent = control.Parent; parent is not null; parent = parent.Parent)
    {
        left += parent.Left;
    }
    return left;
}

static int AbsoluteTop(Control control)
{
    var top = control.Top;
    for (var parent = control.Parent; parent is not null; parent = parent.Parent)
    {
        top += parent.Top;
    }
    return top;
}

using var settingsForm = new AnnotationSettingsForm(new AnnotationSettings());
settingsForm.CreateControl();
var descendants = Descendants(settingsForm).ToArray();
var grid = descendants.OfType<DataGridView>().Single();
Assert(grid.Rows.Count == ButtonCatalog.All.Count, "설정 화면에 일부 버튼이 누락되었습니다.");
Assert(grid.Columns["DisplayName"].HeaderText == "표시 이름", "표시 이름 컬럼명이 적용되지 않았습니다.");

var commandButtons = descendants
    .OfType<Button>()
    .Where(button => button.Text is "확인" or "취소" or "적용")
    .Select(button => button.Text)
    .ToHashSet();
Assert(commandButtons.SetEquals(new[] { "확인", "취소", "적용" }), "하단 명령 버튼 구성이 올바르지 않습니다.");

var rootLayout = settingsForm.Controls.OfType<TableLayoutPanel>().Single();
Assert(rootLayout.RowCount == 2, "설정창 루트 레이아웃은 탭과 하단 버튼의 2행이어야 합니다.");

var applyCount = 0;
using var applyForm = new AnnotationSettingsForm(new AnnotationSettings(), () =>
{
    applyCount++;
    return true;
});
applyForm.CreateControl();
applyForm.Show();
Application.DoEvents();
var applyButton = Descendants(applyForm).OfType<Button>().Single(button => button.Text == "적용");
applyButton.PerformClick();
Assert(applyCount == 1, "적용 버튼이 설정 반영 콜백을 호출하지 않았습니다.");
Assert(!applyForm.IsDisposed && applyForm.DialogResult == DialogResult.None, "적용 버튼이 설정창을 닫았습니다.");
applyForm.Hide();

Assert(
    HotkeyCapture.TryFormat(Keys.Control | Keys.Alt | Keys.L, out var captured) &&
    captured == "Ctrl+Alt+L",
    "실제 키 조합을 단축키 문자열로 변환하지 못했습니다.");
Assert(!HotkeyCapture.TryFormat(Keys.Enter, out _), "Enter 키를 단축키로 기록했습니다.");
Assert(
    HotkeyCapture.IsClearKey(Keys.Delete) && HotkeyCapture.IsClearKey(Keys.Back),
    "단축키 삭제 키를 인식하지 못했습니다.");

using var programEditForm = new ProgramLaunchEditForm(new ProgramLaunchEntry(
    "E3D_폴더",
    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
    string.Empty,
    string.Empty,
    "Ctrl+Alt+E"));
programEditForm.CreateControl();
var programEditTextBoxes = Descendants(programEditForm).OfType<TextBox>().ToArray();
Assert(
    programEditTextBoxes.Any(input => input.Text == "Ctrl+Alt+E"),
    "프로그램 실행 항목 편집창에 단축키 입력칸이 없습니다.");
Assert(
    programEditForm.CreateEntry().Shortcut == "Ctrl+Alt+E",
    "프로그램 실행 항목 단축키가 저장 모델에 반영되지 않았습니다.");

var startupSettings = new AnnotationSettings
{
    ExpandAnnotationSetOnOpen = true,
    ExpandLayoutSetOnOpen = true,
    ExpandProgramSetOnOpen = true,
    ProgramComboWidth = 180
};
Assert(!startupSettings.StartToolbarExpanded, "전체 툴바 시작 상태가 세트별 펼침 상태와 분리되지 않았습니다.");
Assert(startupSettings.ProgramComboWidth == 180, "프로그램 실행 목록 폭 설정이 저장되지 않았습니다.");

using var annotationManager = new AnnotationManager();
using var annotationTool = new AnnotationToolForm(annotationManager, _ => { }, () => { });
annotationTool.CreateControl();
var annotationControls = Descendants(annotationTool).ToArray();
var sectionNames = annotationControls
    .OfType<GroupBox>()
    .Select(group => group.Text)
    .ToHashSet();
Assert(
    sectionNames.SetEquals(new[] { "대상 / 상태", "마크 / 번호", "그리기 도구", "편집 / 캡처" }),
    "마킹 도구의 네 영역 배치가 적용되지 않았습니다.");
Assert(
    annotationControls.OfType<Button>().Any(button => button.Text == "텍스트"),
    "마킹 도구 버튼에 표시 이름이 나타나지 않습니다.");
Assert(
    annotationControls.OfType<Button>().Any(button => button.Text == "원형 마킹"),
    "원형 마킹 버튼명이 적용되지 않았습니다.");
Assert(
    annotationControls
        .OfType<Button>()
        .Where(button => button.BackColor.ToArgb() == annotationManager.MarkerColor.ToArgb())
        .All(button => string.IsNullOrWhiteSpace(button.Text)),
    "마킹 색상 버튼 내부에 중복 텍스트가 표시됩니다.");
Assert(
    annotationControls.OfType<Label>().Any(label =>
        label.Name == "StatusLabel" &&
        label.BorderStyle == BorderStyle.None),
    "마킹 상태창 테두리가 제거되지 않았습니다.");
Assert(
    annotationControls.OfType<Button>().Any(button =>
        button.Name == "CollapseButton" &&
        button.Text == "접기"),
    "마킹 도구 축소 버튼이 없습니다.");
var collapseButton = annotationControls.OfType<Button>().Single(button => button.Name == "CollapseButton");
var targetInput = annotationControls
    .OfType<ComboBox>()
    .Single(combo => combo.Items.Cast<object>().Any(item => item is AnnotationTarget));
var statusLabel = annotationControls.OfType<Label>().Single(label => label.Name == "StatusLabel");
Assert(
    collapseButton.Parent is Panel,
    "마킹 도구 축소 버튼이 대상/상태 영역 안에 배치되지 않았습니다.");
Assert(
    collapseButton.Height == targetInput.Height &&
    statusLabel.Height == targetInput.Height,
    $"상태창과 접기 버튼 높이가 모니터 선택창과 다릅니다. target={targetInput.Height}, status={statusLabel.Height}, collapse={collapseButton.Height}");
Assert(
    collapseButton.FlatStyle == FlatStyle.Flat &&
    collapseButton.FlatAppearance.BorderSize == 1,
    "접기 버튼 테두리가 일반 아이콘 버튼처럼 얇지 않습니다.");
Assert(
    collapseButton.Font.Size == statusLabel.Font.Size &&
    collapseButton.Font.Style == statusLabel.Font.Style,
    "접기/펼치기 글꼴이 상태 문구와 같은 크기/굵기가 아닙니다.");
var regularButtonWidth = annotationControls
    .OfType<Button>()
    .First(button => button.Text == "텍스트")
    .Width;
Assert(
    annotationControls
        .Where(control =>
            control is NumericUpDown ||
            control is Button button && string.IsNullOrWhiteSpace(button.Text) ||
            control is ComboBox combo && combo.Items.Count > 10)
        .Where(control => control != targetInput)
        .All(control => control.Width <= regularButtonWidth),
    "설정 입력 컨트롤 폭이 일반 도구 버튼보다 넓습니다.");
Assert(
    annotationControls.OfType<Button>().Single(button => button.Text == "마킹 종료").Width <= regularButtonWidth,
    "마킹 종료 버튼 폭이 일반 도구 버튼보다 넓습니다.");
var markerSizeInput = annotationControls
    .OfType<NumericUpDown>()
    .First(input => input.Maximum == 60);
var rightGuide = AbsoluteRight(markerSizeInput);
Assert(
    AbsoluteRight(annotationControls.OfType<Button>().Single(button => button.Text == "마킹 종료")) == rightGuide,
    "마킹 종료 버튼 오른쪽 끝이 설정 컨트롤 기준선과 맞지 않습니다.");
Assert(
    AbsoluteRight(collapseButton) == rightGuide,
    $"대상/상태 영역의 오른쪽 끝이 설정 컨트롤 기준선과 맞지 않습니다. guide={rightGuide}, collapse={AbsoluteRight(collapseButton)}");
Assert(
    AbsoluteRight(targetInput) < AbsoluteLeft(statusLabel) &&
    AbsoluteRight(statusLabel) < AbsoluteLeft(collapseButton),
    "대상/상태 영역의 컨트롤 배치 순서가 올바르지 않습니다.");
Assert(
    annotationTool.ClientSize.Width - rightGuide <= AbsoluteLeft(targetInput) + 12,
    "마킹 도구 오른쪽 여백이 왼쪽 여백보다 과하게 큽니다.");
var moveButton = annotationControls.OfType<Button>().Single(button => button.Text == "선택/이동");
var exitButton = annotationControls.OfType<Button>().Single(button => button.Text == "마킹 종료");
Assert(
    AbsoluteTop(moveButton) == AbsoluteTop(exitButton),
    $"편집/캡처 일반 버튼과 마킹 종료 버튼 시작 높이가 다릅니다. move={AbsoluteTop(moveButton)}, exit={AbsoluteTop(exitButton)}");
Assert(
    moveButton.Parent!.ClientSize.Height - (moveButton.Top + moveButton.Height + moveButton.Margin.Bottom) >= 0,
    "편집/캡처 일반 버튼 아래쪽 외곽선이 잘릴 수 있습니다.");

if (args.Contains("--preview", StringComparer.OrdinalIgnoreCase))
{
    static string SavePreview(Form form, string fileName)
    {
        form.Show();
        Application.DoEvents();
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        var path = Path.Combine(Path.GetTempPath(), fileName);
        bitmap.Save(path);
        form.Hide();
        return path;
    }

    using var settingsPreview = new AnnotationSettingsForm(new AnnotationSettings());
    using var annotationPreviewManager = new AnnotationManager();
    using var annotationPreview = new AnnotationToolForm(annotationPreviewManager, _ => { }, () => { });
    Console.WriteLine(SavePreview(settingsPreview, "smart-window-settings-preview.png"));
    Console.WriteLine(SavePreview(annotationPreview, "smart-window-annotation-preview.png"));
}

Console.WriteLine("Smart_Window settings tests passed.");
