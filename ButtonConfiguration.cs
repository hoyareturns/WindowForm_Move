namespace WindowForm_Move;

public sealed class ButtonPreference
{
    public bool Visible { get; set; } = true;
    public string DisplayName { get; set; } = string.Empty;
    public string Shortcut { get; set; } = string.Empty;
}

public sealed record ButtonDefinition(
    string Id,
    string Group,
    string DefaultName,
    bool Required = false);

public static class ButtonCatalog
{
    public static IReadOnlyList<ButtonDefinition> All { get; } =
    new ButtonDefinition[]
    {
        new("main.crosshair", "기본 툴바", "십자선 가이드"),
        new("main.annotation", "기본 툴바", "마킹 도구 창"),
        new("main.layout_toggle", "창 위치 세트", "창 위치 저장 세트 접기/펼치기"),
        new("main.layout_save", "창 위치 세트", "현재 창 위치 저장"),
        new("main.layout_load", "창 위치 세트", "창 위치 불러오기"),
        new("main.layout_delete", "창 위치 세트", "창 위치 삭제"),
        new("main.program_toggle", "프로그램 실행 세트", "프로그램 실행 세트 접기/펼치기"),
        new("main.program_save", "프로그램 실행 세트", "실행 항목 등록"),
        new("main.program_run", "프로그램 실행 세트", "선택 항목 실행"),
        new("main.program_edit", "프로그램 실행 세트", "실행 항목 편집"),
        new("main.program_delete", "프로그램 실행 세트", "실행 항목 삭제"),
        new("main.move_left", "창 이동", "왼쪽 모니터로 이동"),
        new("main.move_right", "창 이동", "오른쪽 모니터로 이동"),
        new("main.move_up_left", "창 이동", "왼쪽 위 모니터로 이동"),
        new("main.move_up_right", "창 이동", "오른쪽 위 모니터로 이동"),
        new("main.move_down", "창 이동", "아래 모니터로 이동"),
        new("main.half_left", "창 배치", "현재 모니터 왼쪽 절반"),
        new("main.half_right", "창 배치", "현재 모니터 오른쪽 절반"),
        new("main.half_top", "창 배치", "현재 모니터 위쪽 절반"),
        new("main.half_bottom", "창 배치", "현재 모니터 아래쪽 절반"),
        new("main.move_all", "기본 툴바", "모든 실행 창 함께 이동"),
        new("main.settings", "필수", "Smart_Window 통합 설정", true),
        new("main.app_exit", "필수", "Smart_Window 종료", true),
        new("main.minimize", "필수", "현재 창 최소화", true),
        new("main.maximize", "필수", "현재 창 최대화/복원", true),
        new("main.close", "필수", "현재 창 닫기", true),
        new("main.collapse", "필수", "Smart_Window 버튼 접기/펼치기", true),
        new("marker.dot", "마킹 도구", "번호 없는 원형 포인트"),
        new("marker.marker_color", "마킹 도구", "마크 색상"),
        new("marker.square", "마킹 도구", "채워진 정사각형"),
        new("marker.number", "마킹 도구", "번호 마크 찍기"),
        new("marker.text", "마킹 도구", "텍스트 추가 또는 수정"),
        new("marker.pen_color", "마킹 도구", "선/그리기 색상"),
        new("marker.arrow_note", "마킹 도구", "화살표와 메모"),
        new("marker.pencil", "마킹 도구", "연필로 자유선 그리기"),
        new("marker.double_arrow", "마킹 도구", "양방향 화살표"),
        new("marker.rectangle", "마킹 도구", "빈 사각형"),
        new("marker.ellipse", "마킹 도구", "빈 원"),
        new("marker.line", "마킹 도구", "자유 직선"),
        new("marker.horizontal", "마킹 도구", "수평선"),
        new("marker.vertical", "마킹 도구", "수직선"),
        new("marker.move", "마킹 편집", "기존 마킹 이동"),
        new("marker.undo", "마킹 편집", "마지막 작업 실행 취소"),
        new("marker.erase", "마킹 편집", "마킹 지우기"),
        new("marker.clear", "마킹 편집", "모든 마킹 지우기"),
        new("marker.capture", "마킹 편집", "마킹 영역 캡처"),
        new("marker.open_folder", "마킹 편집", "캡처 저장 폴더 열기"),
        new("marker.exit", "마킹 편집", "마킹 작업 종료")
    };

    public static ButtonDefinition Get(string id)
    {
        return All.First(definition => definition.Id == id);
    }
}

public sealed record ButtonSettingsValidationResult(
    bool Succeeded,
    string ErrorMessage,
    IReadOnlySet<string> ConflictingButtonIds);

public static class ButtonSettingsValidator
{
    public static ButtonSettingsValidationResult Validate(
        IDictionary<string, ButtonPreference> preferences)
    {
        var conflicts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var shortcuts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in ButtonCatalog.All)
        {
            if (!preferences.TryGetValue(definition.Id, out var preference))
            {
                preference = new ButtonPreference();
                preferences[definition.Id] = preference;
            }

            if (definition.Required)
            {
                preference.Visible = true;
            }

            if (!HotkeyParser.TryNormalize(preference.Shortcut, out var normalized, out var error))
            {
                conflicts.Add(definition.Id);
                return new ButtonSettingsValidationResult(false, error, conflicts);
            }

            preference.Shortcut = normalized;
            if (string.IsNullOrEmpty(normalized))
            {
                continue;
            }

            if (shortcuts.TryGetValue(normalized, out var existingId))
            {
                conflicts.Add(existingId);
                conflicts.Add(definition.Id);
            }
            else
            {
                shortcuts[normalized] = definition.Id;
            }
        }

        var message = conflicts.Count == 0
            ? string.Empty
            : "같은 단축키가 여러 버튼에 지정되어 있습니다.";
        return new ButtonSettingsValidationResult(conflicts.Count == 0, message, conflicts);
    }
}

public readonly record struct HotkeyGesture(Keys Key, KeyModifiers Modifiers)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }
        if (Modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }
        if (Modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }
        if (Modifiers.HasFlag(KeyModifiers.Windows))
        {
            parts.Add("Win");
        }
        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }
}

[Flags]
public enum KeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008
}

public static class HotkeyParser
{
    public static bool TryNormalize(string? value, out string normalized, out string error)
    {
        normalized = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!TryParse(value, out var gesture))
        {
            error = $"올바르지 않은 단축키입니다: {value}";
            return false;
        }

        normalized = gesture.ToString();
        return true;
    }

    public static bool TryParse(string? value, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var modifiers = KeyModifiers.None;
        Keys key = Keys.None;
        foreach (var part in parts)
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= KeyModifiers.Control;
                    continue;
                case "ALT":
                    modifiers |= KeyModifiers.Alt;
                    continue;
                case "SHIFT":
                    modifiers |= KeyModifiers.Shift;
                    continue;
                case "WIN":
                case "WINDOWS":
                    modifiers |= KeyModifiers.Windows;
                    continue;
            }

            if (key != Keys.None ||
                !Enum.TryParse(part, true, out key) ||
                key is Keys.None or Keys.ControlKey or Keys.Menu or Keys.ShiftKey or Keys.LWin or Keys.RWin)
            {
                return false;
            }
        }

        if (key == Keys.None || modifiers == KeyModifiers.None)
        {
            return false;
        }

        gesture = new HotkeyGesture(key, modifiers);
        return true;
    }
}

public static class HotkeyCapture
{
    public static bool TryFormat(Keys keyData, out string shortcut)
    {
        shortcut = string.Empty;
        var key = keyData & Keys.KeyCode;
        if (key is Keys.None or Keys.Enter or Keys.Escape or Keys.Tab or Keys.Delete or Keys.Back)
        {
            return false;
        }

        var modifiers = KeyModifiers.None;
        if (keyData.HasFlag(Keys.Control))
        {
            modifiers |= KeyModifiers.Control;
        }
        if (keyData.HasFlag(Keys.Alt))
        {
            modifiers |= KeyModifiers.Alt;
        }
        if (keyData.HasFlag(Keys.Shift))
        {
            modifiers |= KeyModifiers.Shift;
        }

        if (modifiers == KeyModifiers.None ||
            key is Keys.ControlKey or Keys.Menu or Keys.ShiftKey or Keys.LWin or Keys.RWin)
        {
            return false;
        }

        shortcut = new HotkeyGesture(key, modifiers).ToString();
        return true;
    }

    public static bool IsClearKey(Keys keyData)
    {
        var key = keyData & Keys.KeyCode;
        return key is Keys.Delete or Keys.Back;
    }
}
