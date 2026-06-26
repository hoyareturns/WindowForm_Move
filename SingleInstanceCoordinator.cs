using System.Diagnostics;

namespace WindowForm_Move;

internal static class SingleInstanceCoordinator
{
    private const string StartupMutexName = @"Local\Smart_Window_Start_Gate";
    private const int StartupMutexTimeoutMilliseconds = 5000;
    private const int ProcessExitTimeoutMilliseconds = 3000;

    public static void StopExistingInstances()
    {
        using var startupMutex = new Mutex(false, StartupMutexName);
        var lockTaken = false;

        try
        {
            try
            {
                lockTaken = startupMutex.WaitOne(StartupMutexTimeoutMilliseconds);
            }
            catch (AbandonedMutexException)
            {
                lockTaken = true;
            }

            StopOtherProcesses();
        }
        finally
        {
            if (lockTaken)
            {
                startupMutex.ReleaseMutex();
            }
        }
    }

    private static void StopOtherProcesses()
    {
        using var current = Process.GetCurrentProcess();
        var processName = current.ProcessName;

        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                if (process.Id == current.Id)
                {
                    continue;
                }

                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(ProcessExitTimeoutMilliseconds);
                }
                catch (InvalidOperationException)
                {
                    // The previous instance already exited.
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Continue starting when an inaccessible stale process cannot be stopped.
                }
            }
        }
    }
}
