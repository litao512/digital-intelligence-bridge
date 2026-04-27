using System.Diagnostics;
using System.IO.Compression;

namespace DibClient.Updater;

public sealed class UpgradeExecutor
{
    private readonly UpgradeExecutorOptions _options;

    public UpgradeExecutor(UpgradeExecutorOptions? options = null)
    {
        _options = options ?? new UpgradeExecutorOptions();
    }

    public int Execute(UpgradeOptions options)
    {
        try
        {
            WriteLog("升级开始。");
            Validate(options);
            WaitForProcessExit(options.ProcessId);

            var extractionDirectory = Path.Combine(Path.GetTempPath(), "dib-client-upgrade", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(extractionDirectory);
            ZipFile.ExtractToDirectory(options.PackagePath, extractionDirectory, overwriteFiles: true);

            ReplaceDirectoryContents(extractionDirectory, options.TargetDirectory);
            WriteLog("文件覆盖完成。");

            if ((_options.RestartAfterUpgrade || options.Restart) && File.Exists(options.ExecutablePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = options.ExecutablePath,
                    WorkingDirectory = options.TargetDirectory,
                    UseShellExecute = true
                });
                WriteLog("已启动新版客户端。");
            }

            TryDeleteDirectory(extractionDirectory);
            return 0;
        }
        catch (Exception ex)
        {
            WriteLog("升级失败：" + ex);
            return 1;
        }
    }

    private static void Validate(UpgradeOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.PackagePath) || !File.Exists(options.PackagePath))
        {
            throw new FileNotFoundException("升级包不存在。", options.PackagePath);
        }

        if (string.IsNullOrWhiteSpace(options.TargetDirectory) || !Directory.Exists(options.TargetDirectory))
        {
            throw new DirectoryNotFoundException("客户端目录不存在：" + options.TargetDirectory);
        }

        if (string.IsNullOrWhiteSpace(options.ExecutablePath))
        {
            throw new InvalidOperationException("主程序路径为空。");
        }
    }

    private static void WaitForProcessExit(int? processId)
    {
        if (processId is null or <= 0)
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(processId.Value);
            process.WaitForExit(60_000);
        }
        catch (ArgumentException)
        {
        }
    }

    private static void ReplaceDirectoryContents(string sourceDirectory, string targetDirectory)
    {
        foreach (var targetEntry in Directory.EnumerateFileSystemEntries(targetDirectory))
        {
            var name = Path.GetFileName(targetEntry);
            if (name.Equals("logs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            DeleteEntryWithRetry(targetEntry);
        }

        CopyDirectory(sourceDirectory, targetDirectory);
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, directory)));
        }

        foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destination = Path.Combine(targetDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            CopyFileWithRetry(file, destination);
        }
    }

    private static void DeleteEntryWithRetry(string path)
    {
        Retry(() =>
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        });
    }

    private static void CopyFileWithRetry(string source, string destination)
    {
        Retry(() => File.Copy(source, destination, overwrite: true));
    }

    private static void Retry(Action action)
    {
        Exception? lastException = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (IOException ex)
            {
                lastException = ex;
                Thread.Sleep(250);
            }
            catch (UnauthorizedAccessException ex)
            {
                lastException = ex;
                Thread.Sleep(250);
            }
        }

        throw lastException ?? new IOException("文件操作失败。");
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
        }
    }

    private void WriteLog(string message)
    {
        try
        {
            var logPath = _options.LogPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DibClient",
                "logs",
                "client-updater.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
