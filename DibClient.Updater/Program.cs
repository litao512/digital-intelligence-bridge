using DibClient.Updater;

return new UpgradeExecutor().Execute(ParseArgs(args));

static UpgradeOptions ParseArgs(string[] args)
{
    string? packagePath = null;
    string? targetDirectory = null;
    string? executablePath = null;
    int? processId = null;
    var restart = false;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--package" when i + 1 < args.Length:
                packagePath = args[++i];
                break;
            case "--target-dir" when i + 1 < args.Length:
                targetDirectory = args[++i];
                break;
            case "--exe" when i + 1 < args.Length:
                executablePath = args[++i];
                break;
            case "--process-id" when i + 1 < args.Length && int.TryParse(args[++i], out var parsedProcessId):
                processId = parsedProcessId;
                break;
            case "--restart":
                restart = true;
                break;
        }
    }

    return new UpgradeOptions(
        packagePath ?? string.Empty,
        targetDirectory ?? string.Empty,
        executablePath ?? string.Empty,
        processId,
        restart);
}
