using System.Runtime.InteropServices;

namespace WindowForm_Move;

public sealed class GlobalHotkeyManager : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 100;

    public GlobalHotkeyManager()
    {
        CreateHandle(new CreateParams());
    }

    public IReadOnlyList<string> Register(
        IEnumerable<(string Name, string Shortcut, Action Action)> registrations)
    {
        Clear();
        var failures = new List<string>();
        foreach (var registration in registrations)
        {
            if (!HotkeyParser.TryParse(registration.Shortcut, out var gesture))
            {
                continue;
            }

            var id = _nextId++;
            if (!RegisterHotKey(Handle, id, (uint)gesture.Modifiers | ModNoRepeat, (uint)gesture.Key))
            {
                failures.Add($"{registration.Name}: {registration.Shortcut}");
                continue;
            }
            _actions[id] = registration.Action;
        }
        return failures;
    }

    public void Dispose()
    {
        Clear();
        DestroyHandle();
        GC.SuppressFinalize(this);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmHotkey &&
            _actions.TryGetValue(message.WParam.ToInt32(), out var action))
        {
            action();
        }
        base.WndProc(ref message);
    }

    private void Clear()
    {
        foreach (var id in _actions.Keys)
        {
            UnregisterHotKey(Handle, id);
        }
        _actions.Clear();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
}
