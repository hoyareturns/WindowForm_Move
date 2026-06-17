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

    public static bool IsCoveredByWindowInFront(IntPtr targetHandle, Rectangle overlayRect)
    {
        var covered = false;
        EnumWindows((handle, _) =>
        {
            if (handle == targetHandle)
            {
                return false;
            }

            if (handle == IntPtr.Zero ||
                handle == ShellWindow ||
                BelongsToCurrentProcess(handle) ||
                !IsWindowVisible(handle) ||
                IsIconic(handle))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(GetWindowTitle(handle)))
            {
                return true;
            }

            if (!TryGetWindowRectangle(handle, out var rect) || rect.Width <= 0 || rect.Height <= 0)
            {
                return true;
            }

            if (rect.IntersectsWith(overlayRect))
            {
                covered = true;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return covered;
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
        var fromBounds = fromScreen.Bounds;
        var candidate = Screen.AllScreens
            .Where(screen => screen.DeviceName != fromScreen.DeviceName)
            .Select(screen => new
            {
                Screen = screen,
                Score = GetDirectionalScore(fromBounds, screen.Bounds, direction)
            })
            .Where(candidate => candidate.Score is not null)
            .OrderBy(candidate => candidate.Score!.Value.PrimaryDistance)
            .ThenBy(candidate => candidate.Score!.Value.PerpendicularDistance)
            .ThenBy(candidate => candidate.Score!.Value.CenterDistance)
            .FirstOrDefault();

        return candidate?.Screen;
    }

    private static DirectionalScore? GetDirectionalScore(Rectangle from, Rectangle to, MoveDirection direction)
    {
        var fromCenter = CenterOf(from);
        var toCenter = CenterOf(to);

        return direction switch
        {
            MoveDirection.Left when toCenter.X < fromCenter.X => new DirectionalScore(
                Math.Max(0, from.Left - to.Right),
                GetIntervalDistance(from.Top, from.Bottom, to.Top, to.Bottom),
                DistanceSquared(fromCenter, toCenter)),
            MoveDirection.Right when toCenter.X > fromCenter.X => new DirectionalScore(
                Math.Max(0, to.Left - from.Right),
                GetIntervalDistance(from.Top, from.Bottom, to.Top, to.Bottom),
                DistanceSquared(fromCenter, toCenter)),
            MoveDirection.Up when toCenter.Y < fromCenter.Y => new DirectionalScore(
                Math.Max(0, from.Top - to.Bottom),
                GetIntervalDistance(from.Left, from.Right, to.Left, to.Right),
                DistanceSquared(fromCenter, toCenter)),
            MoveDirection.Down when toCenter.Y > fromCenter.Y => new DirectionalScore(
                Math.Max(0, to.Top - from.Bottom),
                GetIntervalDistance(from.Left, from.Right, to.Left, to.Right),
                DistanceSquared(fromCenter, toCenter)),
            _ => null
        };
    }

    private static int GetIntervalDistance(int firstStart, int firstEnd, int secondStart, int secondEnd)
    {
        if (firstEnd < secondStart)
        {
            return secondStart - firstEnd;
        }

        if (secondEnd < firstStart)
        {
            return firstStart - secondEnd;
        }

        return 0;
    }

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

    private readonly record struct DirectionalScore(int PrimaryDistance, int PerpendicularDistance, long CenterDistance);

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

}
