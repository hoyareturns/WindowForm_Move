using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WindowForm_Move;

public enum MoveDirection
{
    Left,
    Right,
    Up,
    Down
}

public enum WindowAction
{
    Minimize,
    Maximize,
    Restore
}

public sealed record WindowInfo(IntPtr Handle, string Title, string ProcessName)
{
    public string DisplayName => string.IsNullOrWhiteSpace(ProcessName)
        ? Title
        : $"{ProcessName} - {Title}";
}

public static class WindowController
{
    private const int SW_RESTORE = 9;
    private const int SW_MINIMIZE = 6;
    private const int SW_MAXIMIZE = 3;
    private const int WM_CLOSE = 0x0010;
    private const int GWL_HWNDPARENT = -8;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private static readonly IntPtr ShellWindow = GetShellWindow();
    private static readonly int CurrentProcessId = Environment.ProcessId;

    public static IntPtr GetForegroundWindow() => NativeGetForegroundWindow();

    public static IReadOnlyList<WindowInfo> GetMovableWindows()
    {
        var windows = new List<WindowInfo>();
        EnumWindows((handle, _) =>
        {
            if (!IsMovableWindow(handle))
            {
                return true;
            }

            var title = GetWindowTitle(handle);
            var processName = GetProcessName(handle);
            windows.Add(new WindowInfo(handle, title, processName));
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    public static bool IsMovableWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero || handle == ShellWindow || !IsWindowVisible(handle))
        {
            return false;
        }

        if (BelongsToCurrentProcess(handle))
        {
            return false;
        }

        var title = GetWindowTitle(handle);
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        if (!TryGetWindowRectangle(handle, out var rect) || rect.Width <= 0 || rect.Height <= 0)
        {
            return false;
        }

        return true;
    }

    public static bool BelongsToCurrentProcess(IntPtr handle)
    {
        _ = GetWindowThreadProcessId(handle, out var processId);
        return processId == CurrentProcessId;
    }

    public static bool IsMinimized(IntPtr handle) => IsIconic(handle);

    public static void SetOwnerWindow(IntPtr overlayHandle, IntPtr ownerHandle)
    {
        if (overlayHandle == IntPtr.Zero || ownerHandle == IntPtr.Zero)
        {
            return;
        }

        SetWindowLongPtr(overlayHandle, GWL_HWNDPARENT, ownerHandle);
    }

    public static void SetOverlayBounds(IntPtr overlayHandle, int x, int y, int width, int height, bool isVisible)
    {
        var flags = SWP_NOZORDER | SWP_NOACTIVATE;
        if (!isVisible)
        {
            flags |= SWP_SHOWWINDOW;
        }

        SetWindowPos(overlayHandle, IntPtr.Zero, x, y, width, height, flags);
    }

    public static bool TryGetWindowRectangle(IntPtr handle, out Rectangle rectangle)
    {
        if (!GetWindowRect(handle, out var rect))
        {
            rectangle = Rectangle.Empty;
            return false;
        }

        rectangle = rect.ToRectangle();
        return true;
    }

    public static bool MoveWindowToDirection(IntPtr handle, MoveDirection direction)
    {
        if (!TryGetWindowRectangle(handle, out var originalRect))
        {
            return false;
        }

        var fromScreen = Screen.FromHandle(handle);
        var targetScreen = FindTargetScreen(fromScreen, direction);
        var wasMaximized = IsZoomed(handle);
        var wasMinimized = IsIconic(handle);

        if (wasMaximized || wasMinimized)
        {
            ShowWindow(handle, SW_RESTORE);
        }

        if (!TryGetWindowRectangle(handle, out var rect))
        {
            rect = originalRect;
        }

        var next = targetScreen is null
            ? NudgeWithinVirtualScreen(rect, direction)
            : MapRectToScreen(rect, fromScreen.WorkingArea, targetScreen.WorkingArea);

        MoveWindow(handle, next.Left, next.Top, next.Width, next.Height, true);

        if (wasMaximized)
        {
            ShowWindow(handle, SW_MAXIMIZE);
        }
        else if (wasMinimized)
        {
            ShowWindow(handle, SW_MINIMIZE);
        }

        return true;
    }

    public static void ApplyAction(IntPtr handle, WindowAction action)
    {
        var command = action switch
        {
            WindowAction.Minimize => SW_MINIMIZE,
            WindowAction.Maximize => SW_MAXIMIZE,
            WindowAction.Restore => SW_RESTORE,
            _ => SW_RESTORE
        };

        ShowWindow(handle, command);
    }

    public static void CloseWindow(IntPtr handle)
    {
        PostMessage(handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    private static Screen? FindTargetScreen(Screen fromScreen, MoveDirection direction)
    {
        var fromCenter = CenterOf(fromScreen.Bounds);
        var candidates = Screen.AllScreens
            .Where(screen => screen.DeviceName != fromScreen.DeviceName)
            .Select(screen => new { Screen = screen, Center = CenterOf(screen.Bounds) })
            .Where(candidate => IsInDirection(fromCenter, candidate.Center, direction))
            .OrderBy(candidate => DistanceSquared(fromCenter, candidate.Center))
            .ToList();

        return candidates.FirstOrDefault()?.Screen;
    }

    private static bool IsInDirection(Point from, Point to, MoveDirection direction) => direction switch
    {
        MoveDirection.Left => to.X < from.X,
        MoveDirection.Right => to.X > from.X,
        MoveDirection.Up => to.Y < from.Y,
        MoveDirection.Down => to.Y > from.Y,
        _ => false
    };

    private static Rectangle MapRectToScreen(Rectangle rect, Rectangle fromArea, Rectangle toArea)
    {
        var width = Math.Min(rect.Width, toArea.Width);
        var height = Math.Min(rect.Height, toArea.Height);
        var relativeX = fromArea.Width == 0 ? 0 : (double)(rect.Left - fromArea.Left) / fromArea.Width;
        var relativeY = fromArea.Height == 0 ? 0 : (double)(rect.Top - fromArea.Top) / fromArea.Height;
        var left = toArea.Left + (int)Math.Round(relativeX * toArea.Width);
        var top = toArea.Top + (int)Math.Round(relativeY * toArea.Height);

        left = Math.Clamp(left, toArea.Left, toArea.Right - width);
        top = Math.Clamp(top, toArea.Top, toArea.Bottom - height);

        return new Rectangle(left, top, width, height);
    }

    private static Rectangle NudgeWithinVirtualScreen(Rectangle rect, MoveDirection direction)
    {
        const int step = 80;
        var virtualBounds = SystemInformation.VirtualScreen;
        var next = direction switch
        {
            MoveDirection.Left => rect with { X = rect.X - step },
            MoveDirection.Right => rect with { X = rect.X + step },
            MoveDirection.Up => rect with { Y = rect.Y - step },
            MoveDirection.Down => rect with { Y = rect.Y + step },
            _ => rect
        };

        next.X = Math.Clamp(next.X, virtualBounds.Left, virtualBounds.Right - next.Width);
        next.Y = Math.Clamp(next.Y, virtualBounds.Top, virtualBounds.Bottom - next.Height);
        return next;
    }

    private static Point CenterOf(Rectangle rect)
    {
        return new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
    }

    private static long DistanceSquared(Point a, Point b)
    {
        var x = a.X - b.X;
        var y = a.Y - b.Y;
        return (long)x * x + (long)y * y;
    }

    private static string GetWindowTitle(IntPtr handle)
    {
        var length = GetWindowTextLength(handle);
        if (length <= 0)
        {
            return string.Empty;
        }

        var buffer = new char[length + 1];
        var copied = GetWindowText(handle, buffer, buffer.Length);
        return copied <= 0 ? string.Empty : new string(buffer, 0, copied);
    }

    private static string GetProcessName(IntPtr handle)
    {
        try
        {
            _ = GetWindowThreadProcessId(handle, out var processId);
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRect
    {
        private readonly int _left;
        private readonly int _top;
        private readonly int _right;
        private readonly int _bottom;

        public Rectangle ToRectangle()
        {
            return Rectangle.FromLTRB(_left, _top, _right, _bottom);
        }
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, char[] lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
    private static extern IntPtr NativeGetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }
}
