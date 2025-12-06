using System.Runtime.InteropServices;
using MarshallApp.Models;
// ReSharper disable InconsistentNaming

namespace MarshallApp.Services;

public class JobManager(LimitSettings limitSettings) : IDisposable
{
    private IntPtr _jobHandle;

    #region CPU LIMIT

    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_CPU_RATE_CONTROL_INFORMATION
    {
        public uint ControlFlags;
        public uint CpuRate;
    }

    private const uint JOB_OBJECT_CPU_RATE_CONTROL_ENABLE = 0x1;
    private const uint JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP = 0x4;
    private const int JobObjectCpuRateControlInformation = 15;
    
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(
        IntPtr hJob,
        int JobObjectInfoClass,
        IntPtr lpJobObjectInfo,
        int cbJobObjectInfoLength);

    #endregion
    
    #region --- JOB OBJECT KILL TREE ---

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
#pragma warning disable SYSLIB1054
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);
#pragma warning restore SYSLIB1054

    [DllImport("kernel32.dll")]
#pragma warning disable SYSLIB1054
    // ReSharper disable once InconsistentNaming
    private static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass, ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION lpJobObjectInfo, int cbJobObjectInfoLength);
#pragma warning restore SYSLIB1054

    [DllImport("kernel32.dll", SetLastError = true)]
#pragma warning disable SYSLIB1054
    public static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
#pragma warning restore SYSLIB1054

    // ReSharper disable once ArrangeTypeMemberModifiers
    // ReSharper disable once InconsistentNaming
    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public int LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public int ActiveProcessLimit;
        public long Affinity;
        public int PriorityClass;
        public int SchedulingClass;
    }

    // ReSharper disable once ArrangeTypeMemberModifiers
    // ReSharper disable once InconsistentNaming
    [StructLayout(LayoutKind.Sequential)]
    struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    // ReSharper disable once ArrangeTypeMemberModifiers
    // ReSharper disable once InconsistentNaming
    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    // ReSharper disable once InconsistentNaming
    private const int JOB_OBJECT_EXTENDED_LIMIT_INFORMATION = 9;
    // ReSharper disable once InconsistentNaming
    private const int JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;
    
    [DllImport("kernel32.dll")]
#pragma warning disable SYSLIB1054
    private static extern bool CloseHandle(IntPtr hObject);
#pragma warning restore SYSLIB1054

    #endregion

    public IntPtr JobHandle => _jobHandle;

    public void CreateJobObject()
    {
        if (_jobHandle != IntPtr.Zero) return;
    
        _jobHandle = CreateJobObject(IntPtr.Zero, null);

        // --- MEMORY LIMIT ---
        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
        info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

        if (limitSettings.MemoryLimitMb > 0)
        {
            info.BasicLimitInformation.LimitFlags |= 0x100; // JOB_OBJECT_LIMIT_PROCESS_MEMORY
            info.ProcessMemoryLimit = (UIntPtr)(limitSettings.MemoryLimitMb * (int)1024UL * (int)1024UL);
        }

        SetInformationJobObject(
            _jobHandle,
            JOB_OBJECT_EXTENDED_LIMIT_INFORMATION,
            ref info,
            Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>()
        );

        // --- CPU LIMIT ---
        if (limitSettings.CpuLimitPercent <= 0 || limitSettings.CpuLimitPercent > 100) return;
        var cpuInfo = new JOBOBJECT_CPU_RATE_CONTROL_INFORMATION
        {
            ControlFlags = JOB_OBJECT_CPU_RATE_CONTROL_ENABLE | JOB_OBJECT_CPU_RATE_CONTROL_HARD_CAP,
            CpuRate = (uint)(limitSettings.CpuLimitPercent * 100) // 1% = 100 (Windows API)
        };

        var size = Marshal.SizeOf(cpuInfo);
        var ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(cpuInfo, ptr, false);

        SetInformationJobObject(
            _jobHandle,
            JobObjectCpuRateControlInformation,
            ptr,
            size
        );

        Marshal.FreeHGlobal(ptr);
    }

    public void Close()
    {
        if (_jobHandle == IntPtr.Zero) return;
        CloseHandle(_jobHandle);
        _jobHandle = IntPtr.Zero;
    }

    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }

    ~JobManager()
    {
        Close();
    }
}