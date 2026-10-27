using System.Runtime.InteropServices;
using BioComp.Core.Abstractions;

namespace BioComp.Senses;

/// <summary>
/// Real machine body state via Win32 (no extra dependencies): battery through
/// GetSystemPowerStatus, memory through GlobalMemoryStatusEx, CPU load through
/// GetSystemTimes deltas. Poll at ~1 Hz.
/// </summary>
public sealed class WindowsInteroception : IInteroceptionSource
{
    private long _lastIdle, _lastKernel, _lastUser;
    private bool _hasLast;

    public InteroceptionSample Read()
    {
        GetSystemPowerStatus(out var power);
        GetSystemInfoMemory(out ulong availPhys, out ulong totalPhys);

        float cpu = 0f;
        if (GetSystemTimes(out var idle, out var kernel, out var user))
        {
            if (_hasLast)
            {
                long idleDelta = idle - _lastIdle;
                long totalDelta = (kernel - _lastKernel) + (user - _lastUser); // kernel includes idle
                cpu = totalDelta > 0 ? Math.Clamp(100f * (1f - (float)idleDelta / totalDelta), 0f, 100f) : 0f;
            }
            _lastIdle = idle; _lastKernel = kernel; _lastUser = user;
            _hasLast = true;
        }

        return new InteroceptionSample(
            BatteryPercent: power.BatteryLifePercent,
            OnAcPower: power.ACLineStatus == 1,
            BatteryFlag: power.BatteryFlag,
            CpuLoadPercent: cpu,
            MemoryAvailableGb: availPhys / (1024f * 1024f * 1024f));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte Reserved1;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS sps);

    private static void GetSystemInfoMemory(out ulong availPhys, out ulong totalPhys)
    {
        var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        GlobalMemoryStatusEx(ref ms);
        availPhys = ms.ullAvailPhys;
        totalPhys = ms.ullTotalPhys;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);
}
