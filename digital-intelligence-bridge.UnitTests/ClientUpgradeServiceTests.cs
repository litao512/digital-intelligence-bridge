using DigitalIntelligenceBridge.Services;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class ClientUpgradeServiceTests
{
    [Fact]
    public void AppContainerRegistrations_ShouldRegisterClientUpgradeService_ForSettingsViewAutowire()
    {
        var appPath = FindRepositoryFile("digital-intelligence-bridge", "App.axaml.cs");
        var source = File.ReadAllText(appPath);

        Assert.Contains(
            "containerRegistry.RegisterSingleton<IClientUpgradeService, ClientUpgradeService>()",
            source);
    }

    [Fact]
    public void BuildStartInfo_ShouldUseTemporaryUpdaterCopy_AndPassUpgradeArguments()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "dib-client-upgrade-tests", Guid.NewGuid().ToString("N"));
        var appDir = Path.Combine(testRoot, "app");
        var cacheDir = Path.Combine(testRoot, "cache");
        Directory.CreateDirectory(appDir);
        Directory.CreateDirectory(cacheDir);
        var appExePath = Path.Combine(appDir, "digital-intelligence-bridge.exe");
        var updaterExePath = Path.Combine(appDir, "DibClient.Updater.exe");
        var packagePath = Path.Combine(cacheDir, "dib-win-x64-portable-1.0.4.zip");
        File.WriteAllText(appExePath, "app");
        File.WriteAllText(updaterExePath, "updater");
        File.WriteAllText(Path.Combine(appDir, "DibClient.Updater.dll"), "updater dll");
        File.WriteAllText(packagePath, "zip");

        var launcher = new RecordingClientUpgradeProcessLauncher();
        var appExit = new RecordingClientUpgradeApplicationExit();
        var service = new ClientUpgradeService(
            launcher,
            appExit,
            () => 1234,
            () => appExePath,
            () => appDir,
            () => Path.Combine(testRoot, "updater-runs"));

        var result = service.StartUpgrade(packagePath);

        Assert.True(result.IsSuccess);
        Assert.True(appExit.WasCalled);
        Assert.NotNull(launcher.LastRequest);
        Assert.StartsWith(Path.Combine(testRoot, "updater-runs"), launcher.LastRequest!.WorkingDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--package", launcher.LastRequest.Arguments);
        Assert.Contains(packagePath, launcher.LastRequest.Arguments);
        Assert.Contains("--target-dir", launcher.LastRequest.Arguments);
        Assert.Contains(appDir, launcher.LastRequest.Arguments);
        Assert.Contains("--exe", launcher.LastRequest.Arguments);
        Assert.Contains(appExePath, launcher.LastRequest.Arguments);
        Assert.Contains("--process-id", launcher.LastRequest.Arguments);
        Assert.Contains("1234", launcher.LastRequest.Arguments);
        Assert.NotEqual(updaterExePath, launcher.LastRequest.FileName);
        Assert.True(File.Exists(launcher.LastRequest.FileName));
        Assert.True(File.Exists(Path.Combine(launcher.LastRequest.WorkingDirectory, "DibClient.Updater.dll")));
    }

    [Fact]
    public void BuildStartInfo_ShouldEscapeTargetDirectory_WhenBaseDirectoryEndsWithSeparator()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "dib-client-upgrade-tests", Guid.NewGuid().ToString("N"));
        var appDir = Path.Combine(testRoot, "app");
        var cacheDir = Path.Combine(testRoot, "cache");
        Directory.CreateDirectory(appDir);
        Directory.CreateDirectory(cacheDir);
        var appExePath = Path.Combine(appDir, "digital-intelligence-bridge.exe");
        var updaterExePath = Path.Combine(appDir, "DibClient.Updater.exe");
        var packagePath = Path.Combine(cacheDir, "dib-win-x64-portable-1.0.8.zip");
        File.WriteAllText(appExePath, "app");
        File.WriteAllText(updaterExePath, "updater");
        File.WriteAllText(packagePath, "zip");

        var launcher = new RecordingClientUpgradeProcessLauncher();
        var service = new ClientUpgradeService(
            launcher,
            new RecordingClientUpgradeApplicationExit(),
            () => 1234,
            () => appExePath,
            () => appDir + Path.DirectorySeparatorChar,
            () => Path.Combine(testRoot, "updater-runs"));

        var result = service.StartUpgrade(packagePath);

        Assert.True(result.IsSuccess);
        Assert.NotNull(launcher.LastRequest);
        Assert.DoesNotContain($"{appDir}{Path.DirectorySeparatorChar}\" --exe", launcher.LastRequest!.Arguments);
    }

    private static string FindRepositoryFile(params string[] relativePathParts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(relativePathParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException($"未找到仓库文件：{Path.Combine(relativePathParts)}");
    }

    private sealed class RecordingClientUpgradeProcessLauncher : IClientUpgradeProcessLauncher
    {
        public ClientUpgradeProcessRequest? LastRequest { get; private set; }

        public void Launch(ClientUpgradeProcessRequest request)
        {
            LastRequest = request;
        }
    }

    private sealed class RecordingClientUpgradeApplicationExit : IClientUpgradeApplicationExit
    {
        public bool WasCalled { get; private set; }

        public void ExitApplication()
        {
            WasCalled = true;
        }
    }
}
