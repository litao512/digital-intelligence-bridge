using DigitalIntelligenceBridge.Services;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class TrayIconAvailabilityServiceTests
{
    [Fact]
    public void CheckAvailability_ShouldReturnAvailable_WhenIconFileExistsUnderBaseDirectory()
    {
        var baseDirectory = CreateSandboxDirectory();
        var relativePath = Path.Combine("icons", $"{Guid.NewGuid():N}.ico");
        var fullPath = Path.Combine(baseDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "fake-icon");
        var service = new TrayIconAvailabilityService(baseDirectory, "DigitalIntelligenceBridge");

        try
        {
            var result = service.CheckAvailability(relativePath);

            Assert.True(result.IsAvailable);
            Assert.Equal(fullPath, result.Detail);
        }
        finally
        {
            Directory.Delete(baseDirectory, recursive: true);
        }
    }

    [Fact]
    public void CheckAvailability_ShouldReturnUnavailable_WhenFileAndAssetAreMissing()
    {
        var baseDirectory = CreateSandboxDirectory();
        var relativePath = Path.Combine("missing-icons", $"{Guid.NewGuid():N}.ico");
        var service = new TrayIconAvailabilityService(baseDirectory, "DigitalIntelligenceBridge");

        try
        {
            var result = service.CheckAvailability(relativePath);

            Assert.False(result.IsAvailable);
            Assert.StartsWith(Path.Combine(baseDirectory, relativePath), result.Detail);
            Assert.Contains("资源加载失败", result.Detail);
        }
        finally
        {
            Directory.Delete(baseDirectory, recursive: true);
        }
    }

    private static string CreateSandboxDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dib-tray-icon-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
