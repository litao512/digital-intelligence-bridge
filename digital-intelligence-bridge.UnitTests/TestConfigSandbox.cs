using System;
using System.IO;

namespace DigitalIntelligenceBridge.UnitTests;

internal sealed class TestConfigSandbox : IDisposable
{
    private readonly string? _previousConfigRoot;
    private readonly string? _previousAllowUnsafeConfig;

    public TestConfigSandbox(string? rootDirectory = null)
    {
        RootDirectory = rootDirectory ?? Path.Combine(Path.GetTempPath(), $"dib-test-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(RootDirectory);

        _previousConfigRoot = Environment.GetEnvironmentVariable("DIB_CONFIG_ROOT");
        _previousAllowUnsafeConfig = Environment.GetEnvironmentVariable("DIB_ALLOW_UNSAFE_CONFIG");

        Environment.SetEnvironmentVariable("DIB_CONFIG_ROOT", RootDirectory);
        Environment.SetEnvironmentVariable("DIB_ALLOW_UNSAFE_CONFIG", "1");
    }

    public string RootDirectory { get; }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DIB_CONFIG_ROOT", _previousConfigRoot);
        Environment.SetEnvironmentVariable("DIB_ALLOW_UNSAFE_CONFIG", _previousAllowUnsafeConfig);

        if (Directory.Exists(RootDirectory))
        {
            Directory.Delete(RootDirectory, recursive: true);
        }
    }
}
