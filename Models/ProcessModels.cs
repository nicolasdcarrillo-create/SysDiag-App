using System.ComponentModel;

namespace SysDiag.Models;

public class ProcessRow
{
    [DisplayName("Proceso")] public string Proceso { get; set; } = "";
    [DisplayName("PID")] public int Pid { get; set; }
    [DisplayName("CPU %")] public double CpuPct { get; set; }
    [DisplayName("RAM (MB)")] public double RamMb { get; set; }
}
