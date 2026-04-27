using System;
using System.IO;
using Avalonia.Platform;

namespace DigitalIntelligenceBridge.Services;

public sealed class TrayIconAvailabilityService : ITrayIconAvailabilityService
{
    private readonly string _baseDirectory;
    private readonly string _assemblyName;

    public TrayIconAvailabilityService()
        : this(AppContext.BaseDirectory, typeof(TrayIconAvailabilityService).Assembly.GetName().Name ?? string.Empty)
    {
    }

    public TrayIconAvailabilityService(string baseDirectory, string assemblyName)
    {
        _baseDirectory = baseDirectory;
        _assemblyName = assemblyName;
    }

    public TrayIconAvailabilityResult CheckAvailability(string iconPath)
    {
        var fullPath = Path.Combine(_baseDirectory, iconPath);
        if (File.Exists(fullPath))
        {
            return new TrayIconAvailabilityResult(true, fullPath);
        }

        try
        {
            var uri = new Uri($"avares://{_assemblyName}/{iconPath}");
            using var _ = AssetLoader.Open(uri);
            return new TrayIconAvailabilityResult(true, $"{uri}（通过资源加载）");
        }
        catch (Exception ex)
        {
            return new TrayIconAvailabilityResult(false, $"{fullPath}（文件不存在，且资源加载失败: {ex.Message}）");
        }
    }
}
