using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WindowForm_Move;

public static class ExplorerFolder
{
    public static void OpenIfNotOpen(string directory)
    {
        var target = Normalize(directory);
        object? shell = null;
        object? windows = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is not null)
            {
                shell = Activator.CreateInstance(shellType);
                windows = shellType.InvokeMember("Windows", System.Reflection.BindingFlags.InvokeMethod, null, shell, null);
                if (windows is not null && ContainsFolder(windows, target))
                {
                    return;
                }
            }
        }
        catch
        {
            // Open Explorer below when shell window inspection is unavailable.
        }
        finally
        {
            ReleaseComObject(windows);
            ReleaseComObject(shell);
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"") { UseShellExecute = true });
        }
        catch
        {
            // The capture remains saved even if Explorer cannot be opened.
        }
    }

    private static bool ContainsFolder(object windows, string target)
    {
        var type = windows.GetType();
        var count = Convert.ToInt32(type.InvokeMember("Count", System.Reflection.BindingFlags.GetProperty, null, windows, null));
        for (var index = 0; index < count; index++)
        {
            object? window = null;
            try
            {
                window = type.InvokeMember("Item", System.Reflection.BindingFlags.InvokeMethod, null, windows, new object[] { index });
                var locationUrl = Convert.ToString(window?.GetType().InvokeMember(
                    "LocationURL",
                    System.Reflection.BindingFlags.GetProperty,
                    null,
                    window,
                    null));
                if (!string.IsNullOrWhiteSpace(locationUrl) && Uri.TryCreate(locationUrl, UriKind.Absolute, out var uri) && uri.IsFile &&
                    string.Equals(Normalize(uri.LocalPath), target, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                // Some shell windows do not expose a filesystem location.
            }
            finally
            {
                ReleaseComObject(window);
            }
        }

        return false;
    }

    private static string Normalize(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }
}
