namespace MarshallApp.Models;

public class LimitSettings
{
    public int MemoryLimitMb { get; set; } = 0;  
    public int CpuLimitPercent { get; set; } = 0;
    
    public LimitSettings(int cpuLimitPercent, int memoryLimitMb)
    {
        CpuLimitPercent = cpuLimitPercent;
        MemoryLimitMb = memoryLimitMb;
    }

}