using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WindowForm_Move;

public sealed class GlobalMouseHook : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;

    private readonly HookProc _hookProc;
    private IntPtr _hook;

    public GlobalMouseHook()
    {
        _hookProc = HookCallback;
    }

    public Func<GlobalMouseEvent, bool>? Handler { get; set; }

    public void Start()
    {
        if (_hook != IntPtr.Zero)
        {
            return;
        }

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        var moduleHandle = module is null ? IntPtr.Zero : GetModuleHandle(module.ModuleName);
        _hook = SetWindowsHookEx(WH_MOUSE_LL, _hookProc, moduleHandle, 0);
    }

    public void Stop()
    {
        if (_hook == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    public void Dispose()
    {
        Stop();
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && Handler is not null)
        {
            var message = wParam.ToInt32();
            var type = message switch
            {
                WM_LBUTTONDOWN => GlobalMouseEventType.LeftDown,
                WM_LBUTTONUP => GlobalMouseEventType.LeftUp,
                WM_MOUSEMOVE => GlobalMouseEventType.Move,
                _ => GlobalMouseEventType.Other
            };

            if (type != GlobalMouseEventType.Other)
            {
                var data = Marshal.PtrToStructure<LowLevelMouseData>(lParam);
                if (Handler(new GlobalMouseEvent(type, new Point(data.Point.X, data.Point.Y))))
                {
                    return new IntPtr(1);
                }
            }
        }

        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LowLevelMouseData
    {
        public NativePoint Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, HookProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}

public readonly record struct GlobalMouseEvent(GlobalMouseEventType Type, Point Location);

public enum GlobalMouseEventType
{
    Other,
    Move,
    LeftDown,
    LeftUp
}
