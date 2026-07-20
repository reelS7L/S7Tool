using S7Tool.Models;

namespace S7Tool.Services.Interfaces;

public interface IProcessMonitorService
{
    List<ProcessInfo> GetProcesses();
    bool KillProcess(int pid);
}
