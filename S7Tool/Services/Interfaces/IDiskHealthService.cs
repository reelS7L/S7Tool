using S7Tool.DiskEngine.Models;
using S7Tool.Models;

namespace S7Tool.Services.Interfaces;

public record SmartHistoryPoint(DateTime TimestampUtc, int DiskNumber, double? TemperatureCelsius, string HealthTier);

public record AlertThresholds(double MaxTemperatureCelsius, double MinWearPercentRemaining);

public interface IDiskHealthService
{
    Task<List<DiskHealthRow>> GetAllAsync();

    Task<bool> StartSelfTestAsync(int diskNumber, bool extended);

    Task AppendHistoryAsync(IEnumerable<DiskSmartInfo> snapshot);
    Task<List<SmartHistoryPoint>> GetHistoryAsync(int diskNumber, int maxPoints = 200);

    AlertThresholds GetAlertThresholds();
    void SetAlertThresholds(AlertThresholds thresholds);

    Task ExportAsync(IEnumerable<DiskSmartInfo> disks, string format, string destinationPath);
}
