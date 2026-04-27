using DibClient.Updater;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class ClientUpdaterExecutorTests
{
    [Fact]
    public void Execute_ShouldReplaceTargetFiles_AndPreserveUpdaterLog()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "dib-updater-executor-tests", Guid.NewGuid().ToString("N"));
        var targetDir = Path.Combine(testRoot, "target");
        var packageSourceDir = Path.Combine(testRoot, "package-source");
        var packagePath = Path.Combine(testRoot, "package.zip");
        var logPath = Path.Combine(testRoot, "updater.log");
        Directory.CreateDirectory(targetDir);
        Directory.CreateDirectory(packageSourceDir);
        File.WriteAllText(Path.Combine(targetDir, "old.txt"), "old");
        File.WriteAllText(Path.Combine(packageSourceDir, "new.txt"), "new");
        File.WriteAllText(Path.Combine(packageSourceDir, "digital-intelligence-bridge.exe"), "new exe");
        System.IO.Compression.ZipFile.CreateFromDirectory(packageSourceDir, packagePath);

        var executor = new UpgradeExecutor(new UpgradeExecutorOptions(RestartAfterUpgrade: false, LogPath: logPath));

        var result = executor.Execute(new UpgradeOptions(
            PackagePath: packagePath,
            TargetDirectory: targetDir,
            ExecutablePath: Path.Combine(targetDir, "digital-intelligence-bridge.exe"),
            ProcessId: null,
            Restart: false));

        Assert.Equal(0, result);
        Assert.False(File.Exists(Path.Combine(targetDir, "old.txt")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(targetDir, "new.txt")));
        Assert.Equal("new exe", File.ReadAllText(Path.Combine(targetDir, "digital-intelligence-bridge.exe")));
        Assert.True(File.Exists(logPath));
    }
}
