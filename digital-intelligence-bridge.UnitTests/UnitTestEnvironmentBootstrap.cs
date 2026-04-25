using System;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace DigitalIntelligenceBridge.UnitTests;

internal static class UnitTestEnvironmentBootstrap
{
    private static readonly string SuiteConfigRoot =
        Path.Combine(Path.GetTempPath(), $"dib-test-config-suite-{Guid.NewGuid():N}");

    [ModuleInitializer]
    public static void Initialize()
    {
        Directory.CreateDirectory(SuiteConfigRoot);
        Environment.SetEnvironmentVariable("DIB_CONFIG_ROOT", SuiteConfigRoot);
        Environment.SetEnvironmentVariable("DIB_ALLOW_UNSAFE_CONFIG", "1");

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                if (Directory.Exists(SuiteConfigRoot))
                {
                    Directory.Delete(SuiteConfigRoot, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup failures in test process shutdown.
            }
        };
    }
}
