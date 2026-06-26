using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WindowForm_Move;

public enum MoveDirection
{
    Left,
    Right,
    UpLeft,
    UpRight,
    Up,
    Down
}

public enum WindowAction
{
    Minimize,
    Maximize,
    Restore
}

public enum WindowHalf
{
    Left,
    Right,
    Top,
    Bottom
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
    private const int SW_SHOWMAXIMIZED = 3;
    private const int WM_SYSCOMMAND = 0x0112;
    private const int SC_CLOSE = 0xF060;
    private const uint OBJID_NATIVEOM = 0xFFFFFFF0;
    private const int GW_HWNDPREV = 3;
    private const int GW_OWNER = 4;
    private const int GWL_EXSTYLE = -20;
    private const int DWMWA_CLOAKED = 14;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const long WS_EX_TOOLWINDOW = 0x00000080L;
    private const long WS_EX_APPWINDOW = 0x00040000L;

    private static readonly IntPtr ShellWindow = GetShellWindow();
    private static readonly int CurrentProcessId = Environment.ProcessId;

    public static IntPtr GetForegroundWindow() => NativeGetForegroundWindow();

    public static void ActivateWindow(IntPtr handle)
    {
        if (handle != IntPtr.Zero)
        {
            SetForegroundWindow(handle);
        }
    }

    public static IReadOnlyList<WindowLayoutEntry> CaptureWindowLayout()
    {
        var result = new List<WindowLayoutEntry>();
        foreach (var window in GetMovableWindows())
        {
            if (!TryGetWindowRectangle(window.Handle, out var bounds))
            {
                continue;
            }

            var screen = Screen.FromHandle(window.Handle);
            if (IsZoomed(window.Handle))
            {
                bounds = screen.WorkingArea;
            }

            result.Add(new WindowLayoutEntry(
                window.ProcessName,
                window.Title,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                IsZoomed(window.Handle),
                screen.DeviceName,
                screen.WorkingArea.X,
                screen.WorkingArea.Y,
                screen.WorkingArea.Width,
                screen.WorkingArea.Height,
                GetProcessPath(window.Handle)));
        }

        return result;
    }

    public static int RestoreWindowLayout(IReadOnlyList<WindowLayoutEntry> entries)
    {
        var available = GetLayoutRestoreWindows().ToList();
        var usedHandles = new HashSet<IntPtr>();
        var missing = new List<WindowLayoutEntry>();
        return RestoreAvailableEntries(entries, available, usedHandles, missing);
    }

    private static int RestoreAvailableEntries(
        IEnumerable<WindowLayoutEntry> entries,
        List<WindowInfo> available,
        HashSet<IntPtr> usedHandles,
        List<WindowLayoutEntry> missing)
    {
        var restored = 0;

        foreach (var entry in entries)
        {
            var match = available.FirstOrDefault(window =>
                string.Equals(window.ProcessName, entry.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(window.Title, entry.Title, StringComparison.CurrentCultureIgnoreCase));

            if (match is null)
            {
                var sameProcess = available
                    .Where(window => string.Equals(
                        window.ProcessName,
                        entry.ProcessName,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (sameProcess.Count == 1)
                {
                    match = sameProcess[0];
                }
            }

            if (match is null)
            {
                missing.Add(entry);
                continue;
            }

            available.Remove(match);
            usedHandles.Add(match.Handle);
            var savedBounds = new Rectangle(entry.X, entry.Y, entry.Width, entry.Height);
            var savedArea = new Rectangle(entry.ScreenX, entry.ScreenY, entry.ScreenWidth, entry.ScreenHeight);
            var targetScreen = Screen.AllScreens.FirstOrDefault(screen =>
                                   string.Equals(screen.DeviceName, entry.ScreenDeviceName, StringComparison.OrdinalIgnoreCase))
                               ?? Screen.FromRectangle(savedBounds);
            var targetBounds = entry.Maximized
                ? targetScreen.WorkingArea
                : MapRectToScreen(savedBounds, savedArea, targetScreen.WorkingArea);

            var wasMinimized = IsIconic(match.Handle);
            ShowWindow(match.Handle, SW_RESTORE);
            if (wasMinimized)
            {
                Thread.Sleep(40);
            }
            MoveWindow(
                match.Handle,
                targetBounds.Left,
                targetBounds.Top,
                targetBounds.Width,
                targetBounds.Height,
                true);

            if (entry.Maximized)
            {
                ShowWindow(match.Handle, SW_MAXIMIZE);
            }

            restored++;
        }

        return restored;
    }

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

    public static IReadOnlyDictionary<IntPtr, int> GetWindowZOrder()
    {
        var result = new Dictionary<IntPtr, int>();
        var order = 0;
        EnumWindows((handle, _) =>
        {
            result[handle] = order++;
            return true;
        }, IntPtr.Zero);
        return result;
    }

    public static bool PlaceWindowDirectlyAbove(
        IntPtr windowHandle,
        IntPtr targetHandle,
        Rectangle bounds)
    {
        if (windowHandle == IntPtr.Zero || targetHandle == IntPtr.Zero)
        {
            return false;
        }

        var windowAboveTarget = GetWindow(targetHandle, GW_HWNDPREV);
        if (windowAboveTarget == windowHandle)
        {
            return true;
        }

        return SetWindowPos(
            windowHandle,
            windowAboveTarget,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    public static bool TryGetOverlayRegion(IntPtr handle, out Rectangle region)
    {
        region = Rectangle.Empty;
        if (!IsOverlayLayout(handle) || !TryGetWindowRectangle(handle, out var windowRect))
        {
            return false;
        }

        var workingArea = Screen.FromHandle(handle).WorkingArea;
        region = Rectangle.Intersect(windowRect, workingArea);
        return region.Width > 0 && region.Height > 0;
    }

    private static IReadOnlyList<WindowInfo> GetLayoutRestoreWindows()
    {
        var windows = new List<WindowInfo>();
        EnumWindows((handle, _) =>
        {
            if (handle == IntPtr.Zero ||
                handle == ShellWindow ||
                !IsWindowVisible(handle) ||
                IsWindowCloaked(handle) ||
                BelongsToCurrentProcess(handle) ||
                !IsAppWindowCandidate(handle) ||
                IsExcludedWindowClass(handle))
            {
                return true;
            }

            var title = GetWindowTitle(handle);
            if (string.IsNullOrWhiteSpace(title))
            {
                return true;
            }

            if (!IsIconic(handle) && !IsMovableWindow(handle))
            {
                return true;
            }

            windows.Add(new WindowInfo(handle, title, GetProcessName(handle)));
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    public static bool IsMovableWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero || handle == ShellWindow || !IsWindowVisible(handle) || IsWindowCloaked(handle))
        {
            return false;
        }

        if (BelongsToCurrentProcess(handle))
        {
            return false;
        }

        if (!IsAppWindowCandidate(handle))
        {
            return false;
        }

        if (IsExcludedWindowClass(handle))
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

        if (!IsRectangleVisibleOnAnyScreen(rect))
        {
            return false;
        }

        return true;
    }

    private static bool IsAppWindowCandidate(IntPtr handle)
    {
        var exStyle = GetWindowLongPtr(handle, GWL_EXSTYLE).ToInt64();
        if ((exStyle & WS_EX_TOOLWINDOW) != 0)
        {
            return false;
        }

        var owner = GetWindow(handle, GW_OWNER);
        if (owner != IntPtr.Zero && (exStyle & WS_EX_APPWINDOW) == 0)
        {
            return false;
        }

        return true;
    }

    public static bool IsRectangleVisibleOnAnyScreen(Rectangle rect)
    {
        return Screen.AllScreens.Any(screen => Rectangle.Intersect(screen.Bounds, rect).Width > 0 &&
                                               Rectangle.Intersect(screen.Bounds, rect).Height > 0);
    }

    private static bool IsWindowCloaked(IntPtr handle)
    {
        try
        {
            var result = DwmGetWindowAttribute(handle, DWMWA_CLOAKED, out var cloaked, sizeof(int));
            return result == 0 && cloaked != 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsExcludedWindowClass(IntPtr handle)
    {
        var className = GetWindowClassName(handle);
        return className is "Progman" or "WorkerW" or "Shell_TrayWnd" or "NotifyIconOverflowWindow" or "DV2ControlHost";
    }

    public static bool BelongsToCurrentProcess(IntPtr handle)
    {
        _ = GetWindowThreadProcessId(handle, out var processId);
        return processId == CurrentProcessId;
    }

    public static bool IsMinimized(IntPtr handle) => IsIconic(handle);

    public static bool IsMaximized(IntPtr handle)
    {
        var placement = new WindowPlacement
        {
            Length = Marshal.SizeOf<WindowPlacement>()
        };

        if (!IsZoomed(handle) || !GetWindowPlacement(handle, ref placement) || placement.ShowCommand != SW_SHOWMAXIMIZED)
        {
            return false;
        }

        if (!TryGetWindowRectangle(handle, out var windowRect))
        {
            return false;
        }

        var workingArea = Screen.FromHandle(handle).WorkingArea;
        const int frameTolerance = 12;
        return windowRect.Left <= workingArea.Left + frameTolerance &&
               windowRect.Top <= workingArea.Top + frameTolerance &&
               windowRect.Right >= workingArea.Right - frameTolerance &&
               windowRect.Bottom >= workingArea.Bottom - frameTolerance;
    }

    public static bool IsOverlayLayout(IntPtr handle)
    {
        return IsMaximized(handle);
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
                IsIconic(handle) ||
                IsWindowCloaked(handle) ||
                !IsAppWindowCandidate(handle) ||
                IsExcludedWindowClass(handle))
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

    public static bool SnapWindowToHalf(IntPtr handle, WindowHalf half)
    {
        if (!IsMovableWindow(handle))
        {
            return false;
        }

        if (IsZoomed(handle) || IsIconic(handle))
        {
            ShowWindow(handle, SW_RESTORE);
        }

        var target = GetHalfRectangle(Screen.FromHandle(handle).WorkingArea, half);
        return MoveWindow(handle, target.Left, target.Top, target.Width, target.Height, true);
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
        if (string.Equals(GetProcessName(handle), "EXCEL", StringComparison.OrdinalIgnoreCase))
        {
            _ = TryCloseExcelWindow(handle);
            return;
        }

        PostMessage(handle, WM_SYSCOMMAND, new IntPtr(SC_CLOSE), IntPtr.Zero);
    }

    private static bool TryCloseExcelWindow(IntPtr handle)
    {
        object? excelWindow = null;
        try
        {
            var dispatchId = new Guid("00020400-0000-0000-C000-000000000046");
            var documentHandle = FindDescendantWindowByClass(handle, "EXCEL7");
            var nativeHandle = documentHandle == IntPtr.Zero ? handle : documentHandle;
            if (AccessibleObjectFromWindow(nativeHandle, OBJID_NATIVEOM, ref dispatchId, out excelWindow) != 0 ||
                excelWindow is null)
            {
                return false;
            }

            ((dynamic)excelWindow).Close();
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (excelWindow is not null && Marshal.IsComObject(excelWindow))
            {
                Marshal.FinalReleaseComObject(excelWindow);
            }
        }
    }

    private static IntPtr FindDescendantWindowByClass(IntPtr parent, string className)
    {
        var result = IntPtr.Zero;
        EnumChildWindows(parent, (handle, _) =>
        {
            if (string.Equals(GetWindowClassName(handle), className, StringComparison.OrdinalIgnoreCase))
            {
                result = handle;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return result;
    }

    public static void ToggleMaximizeWindow(IntPtr handle)
    {
        ShowWindow(handle, IsZoomed(handle) ? SW_RESTORE : SW_MAXIMIZE);
    }

    private static Screen? FindTargetScreen(Screen fromScreen, MoveDirection direction)
    {
        if (direction is MoveDirection.UpLeft or MoveDirection.UpRight)
        {
            return FindTopCornerScreen(direction);
        }

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

    private static Screen? FindTopCornerScreen(MoveDirection direction)
    {
        var screens = Screen.AllScreens;
        if (screens.Length == 0)
        {
            return null;
        }

        var minimumTop = screens.Min(screen => screen.Bounds.Top);
        var rowTolerance = Math.Max(1, screens.Min(screen => screen.Bounds.Height) / 2);
        var topRow = screens
            .Where(screen => screen.Bounds.Top <= minimumTop + rowTolerance)
            .ToList();

        if (topRow.Count == 0)
        {
            topRow = screens.ToList();
        }

        return direction == MoveDirection.UpLeft
            ? topRow.OrderBy(screen => CenterOf(screen.Bounds).X).First()
            : topRow.OrderByDescending(screen => CenterOf(screen.Bounds).X).First();
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

    private static Rectangle GetHalfRectangle(Rectangle workingArea, WindowHalf half)
    {
        var leftWidth = workingArea.Width / 2;
        var topHeight = workingArea.Height / 2;
        return half switch
        {
            WindowHalf.Left => new Rectangle(workingArea.Left, workingArea.Top, leftWidth, workingArea.Height),
            WindowHalf.Right => new Rectangle(
                workingArea.Left + leftWidth,
                workingArea.Top,
                workingArea.Width - leftWidth,
                workingArea.Height),
            WindowHalf.Top => new Rectangle(workingArea.Left, workingArea.Top, workingArea.Width, topHeight),
            WindowHalf.Bottom => new Rectangle(
                workingArea.Left,
                workingArea.Top + topHeight,
                workingArea.Width,
                workingArea.Height - topHeight),
            _ => workingArea
        };
    }

    private static bool RectanglesApproximatelyEqual(Rectangle actual, Rectangle expected)
    {
        const int tolerance = 12;
        return Math.Abs(actual.Left - expected.Left) <= tolerance &&
               Math.Abs(actual.Top - expected.Top) <= tolerance &&
               Math.Abs(actual.Right - expected.Right) <= tolerance &&
               Math.Abs(actual.Bottom - expected.Bottom) <= tolerance;
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
            MoveDirection.UpLeft => rect with { X = rect.X - step, Y = rect.Y - step },
            MoveDirection.UpRight => rect with { X = rect.X + step, Y = rect.Y - step },
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

    private static string GetProcessPath(IntPtr handle)
    {
        try
        {
            _ = GetWindowThreadProcessId(handle, out var processId);
            using var process = Process.GetProcessById((int)processId);
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetWindowClassName(IntPtr handle)
    {
        var buffer = new char[256];
        var copied = GetClassName(handle, buffer, buffer.Length);
        return copied <= 0 ? string.Empty : new string(buffer, 0, copied);
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

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPlacement
    {
        public int Length;
        public int Flags;
        public int ShowCommand;
        public Point MinimumPosition;
        public Point MaximumPosition;
        public Rectangle NormalPosition;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc enumProc, IntPtr lParam);

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
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WindowPlacement lpwndpl);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("oleacc.dll")]
    private static extern int AccessibleObjectFromWindow(
        IntPtr hwnd,
        uint objectId,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out object? accessibleObject);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, char[] lpClassName, int nMaxCount);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : new IntPtr(GetWindowLong32(hWnd, nIndex));
    }
}
