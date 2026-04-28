using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace DigitalIntelligenceBridge.Services;

public interface IClientUpgradeService
{
    ClientUpgradeStartResult StartUpgrade(string packagePath);
}

public sealed record ClientUpgradeStartResult(bool IsSuccess, string Summary, string Detail);

public sealed record ClientUpgradeProcessRequest(string FileName, string Arguments, string WorkingDirectory);

public interface IClientUpgradeProcessLauncher
{
    void Launch(ClientUpgradeProcessRequest request);
}

public interface IClientUpgradeApplicationExit
{
    void ExitApplication();
}

public sealed class ClientUpgradeService : IClientUpgradeService
{
    private const string UpdaterFileName = "DibClient.Updater.exe";
    private readonly IClientUpgradeProcessLauncher _launcher;
    private readonly IClientUpgradeApplicationExit _applicationExit;
    private readonly Func<int> _currentProcessIdProvider;
    private readonly Func<string?> _currentExecutablePathProvider;
    private readonly Func<string> _baseDirectoryProvider;
    private readonly Func<string> _updaterRunRootProvider;

    public ClientUpgradeService(ITrayService trayService)
        : this(
            new ProcessClientUpgradeProcessLauncher(),
            new TrayClientUpgradeApplicationExit(trayService),
            () => Environment.ProcessId,
            () => Environment.ProcessPath,
            () => AppContext.BaseDirectory,
            GetDefaultUpdaterRunRoot)
    {
    }

    internal ClientUpgradeService(
        IClientUpgradeProcessLauncher launcher,
        IClientUpgradeApplicationExit applicationExit,
        Func<int> currentProcessIdProvider,
        Func<string?> currentExecutablePathProvider,
        Func<string> baseDirectoryProvider,
        Func<string>? updaterRunRootProvider = null)
    {
        _launcher = launcher;
        _applicationExit = applicationExit;
        _currentProcessIdProvider = currentProcessIdProvider;
        _currentExecutablePathProvider = currentExecutablePathProvider;
        _baseDirectoryProvider = baseDirectoryProvider;
        _updaterRunRootProvider = updaterRunRootProvider ?? GetDefaultUpdaterRunRoot;
    }

    public static string GetDefaultUpdaterRunRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DibClient",
            "updater-runs");
    }

    public ClientUpgradeStartResult StartUpgrade(string packagePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
            {
                return new ClientUpgradeStartResult(false, "客户端升级不可用", "升级包不存在。");
            }

            var targetDir = Path.GetFullPath(_baseDirectoryProvider());
            if (!Directory.Exists(targetDir))
            {
                return new ClientUpgradeStartResult(false, "客户端升级不可用", $"客户端目录不存在：{targetDir}");
            }

            var executablePath = _currentExecutablePathProvider();
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return new ClientUpgradeStartResult(false, "客户端升级不可用", "无法解析当前客户端主程序路径。");
            }

            var sourceUpdaterPath = Path.Combine(targetDir, UpdaterFileName);
            if (!File.Exists(sourceUpdaterPath))
            {
                return new ClientUpgradeStartResult(false, "客户端升级不可用", $"升级助手不存在：{sourceUpdaterPath}");
            }

            var runDirectory = CreateUpdaterRunDirectory(_updaterRunRootProvider());
            CopyUpdaterFiles(targetDir, runDirectory);
            var updaterPath = Path.Combine(runDirectory, UpdaterFileName);
            var arguments = BuildArguments(packagePath, targetDir, executablePath, _currentProcessIdProvider());

            _launcher.Launch(new ClientUpgradeProcessRequest(updaterPath, arguments, runDirectory));
            _applicationExit.ExitApplication();
            return new ClientUpgradeStartResult(true, "客户端升级已启动", "DIB 将退出，升级助手会完成覆盖并重新启动。");
        }
        catch (Exception ex)
        {
            return new ClientUpgradeStartResult(false, "客户端升级启动失败", ex.Message);
        }
    }

    private static string CreateUpdaterRunDirectory(string runRoot)
    {
        var runDirectory = Path.Combine(runRoot, DateTime.Now.ToString("yyyyMMddHHmmssfff"));
        Directory.CreateDirectory(runDirectory);
        return runDirectory;
    }

    private static void CopyUpdaterFiles(string sourceDirectory, string runDirectory)
    {
        var updaterFiles = Directory.GetFiles(sourceDirectory, "DibClient.Updater.*", SearchOption.TopDirectoryOnly);
        foreach (var sourcePath in updaterFiles.Append(Path.Combine(sourceDirectory, UpdaterFileName)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(sourcePath))
            {
                continue;
            }

            File.Copy(sourcePath, Path.Combine(runDirectory, Path.GetFileName(sourcePath)), overwrite: true);
        }
    }

    private static string BuildArguments(string packagePath, string targetDir, string executablePath, int processId)
    {
        return string.Join(
            " ",
            "--package", Quote(packagePath),
            "--target-dir", Quote(targetDir),
            "--exe", Quote(executablePath),
            "--process-id", processId.ToString(),
            "--restart");
    }

    private static string Quote(string value)
    {
        var builder = new StringBuilder();
        builder.Append('"');
        var backslashCount = 0;

        foreach (var character in value)
        {
            if (character == '\\')
            {
                backslashCount++;
                continue;
            }

            if (character == '"')
            {
                builder.Append('\\', backslashCount * 2 + 1);
                builder.Append('"');
                backslashCount = 0;
                continue;
            }

            builder.Append('\\', backslashCount);
            backslashCount = 0;
            builder.Append(character);
        }

        builder.Append('\\', backslashCount * 2);
        builder.Append('"');
        return builder.ToString();
    }

    private sealed class ProcessClientUpgradeProcessLauncher : IClientUpgradeProcessLauncher
    {
        public void Launch(ClientUpgradeProcessRequest request)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = request.FileName,
                Arguments = request.Arguments,
                WorkingDirectory = request.WorkingDirectory,
                UseShellExecute = true
            });
        }
    }

    private sealed class TrayClientUpgradeApplicationExit : IClientUpgradeApplicationExit
    {
        private readonly ITrayService _trayService;

        public TrayClientUpgradeApplicationExit(ITrayService trayService)
        {
            _trayService = trayService;
        }

        public void ExitApplication()
        {
            _trayService.ExitApplication();
        }
    }
}
