using S7Tool.Models;
using S7Tool.Services.Interfaces;
using System.Diagnostics;

namespace S7Tool.Services;

public class ProcessMonitorService : IProcessMonitorService
{
    private readonly Dictionary<int, TimeSpan> _cpuOld = new();
    private DateTime _last = DateTime.UtcNow;

    public List<ProcessInfo> GetProcesses()
    {
        var now = DateTime.UtcNow;
        double interval = Math.Max((now - _last).TotalMilliseconds, 1);
        _last = now;

        var result = new List<ProcessInfo>();

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                double mem = p.WorkingSet64 / 1024d / 1024d;

                double cpu = 0;

                try
                {
                    if (_cpuOld.TryGetValue(p.Id, out var old))
                    {
                        var newTime = p.TotalProcessorTime;
                        cpu = (newTime - old).TotalMilliseconds /
                              interval /
                              Environment.ProcessorCount * 100;
                    }

                    _cpuOld[p.Id] = p.TotalProcessorTime;
                }
                catch
                {
                    cpu = 0;
                }

                result.Add(new ProcessInfo
                {
                    Name = p.ProcessName,
                    Id = p.Id,
                    Memory = Math.Round(mem, 2),
                    Cpu = Math.Round(cpu, 2),
                    Disk = 0
                });
            }
            catch
            {
            }
        }

        return result;
    }

    public bool KillProcess(int pid)
    {
        try
        {
            Process.GetProcessById(pid).Kill();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
