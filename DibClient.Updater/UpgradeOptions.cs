namespace DibClient.Updater;

public sealed record UpgradeOptions(
    string PackagePath,
    string TargetDirectory,
    string ExecutablePath,
    int? ProcessId,
    bool Restart);

public sealed record UpgradeExecutorOptions(bool RestartAfterUpgrade = false, string? LogPath = null);
