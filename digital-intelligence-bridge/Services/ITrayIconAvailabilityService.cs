namespace DigitalIntelligenceBridge.Services;

public interface ITrayIconAvailabilityService
{
    TrayIconAvailabilityResult CheckAvailability(string iconPath);
}

public sealed record TrayIconAvailabilityResult(bool IsAvailable, string Detail);
