// SPDX-License-Identifier: EUPL-1.2
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OmniEurope.Documents.StressTests;

/// <summary>
/// The processor time spent by the calling thread, so that a time limit measures the work of the code under test and
/// not how many other tests share the machine. Windows and Linux read the thread's own clock; any other system falls
/// back to the elapsed time.
/// </summary>
internal static class ThreadCpuTime
{
    private const int ClockThreadCpuTimeId = 3;

    /// <summary>Runs <paramref name="work"/> on the calling thread and returns its result with the time it took.</summary>
    public static (T Result, TimeSpan Time) Measure<T>(Func<T> work)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            var watch = Stopwatch.StartNew();
            var value = work();
            return (value, watch.Elapsed);
        }

        var start = Now();
        var result = work();
        return (result, Now() - start);
    }

    private static TimeSpan Now()
    {
        if (OperatingSystem.IsWindows())
        {
            if (!GetThreadTimes(GetCurrentThread(), out _, out _, out var kernel, out var user))
            {
                throw new InvalidOperationException($"GetThreadTimes failed ({Marshal.GetLastPInvokeError()}).");
            }

            return TimeSpan.FromTicks(kernel + user);
        }

        if (clock_gettime(ClockThreadCpuTimeId, out var time) != 0)
        {
            throw new InvalidOperationException($"clock_gettime failed ({Marshal.GetLastPInvokeError()}).");
        }

        return TimeSpan.FromTicks((time.Seconds * TimeSpan.TicksPerSecond) + (time.Nanoseconds / 100));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Timespec
    {
        public long Seconds;
        public long Nanoseconds;
    }

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetThreadTimes(IntPtr thread, out long creation, out long exit, out long kernel, out long user);

    [DllImport("libc", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int clock_gettime(int clock, out Timespec time);
}
