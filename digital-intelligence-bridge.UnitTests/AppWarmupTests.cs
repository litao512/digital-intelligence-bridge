using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.Services;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public sealed class AppWarmupTests
{
    [Fact]
    public async Task WarmAuthorizedResourcesCacheAsync_ShouldSwallowFailures_WhenReleaseCenterUnavailable()
    {
        var service = new StubReleaseCenterService
        {
            AuthorizedResourcesException = new HttpRequestException("release center unavailable")
        };
        var logger = new RecordingLoggerService<App>();

        await App.WarmAuthorizedResourcesCacheAsync(service, logger, TimeSpan.FromSeconds(1));

        Assert.True(service.GetAuthorizedResourcesCalled);
        Assert.Contains(logger.WarningMessages, message => message.Contains("启动时预热授权资源缓存失败", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WarmAuthorizedResourcesCacheAsync_ShouldSwallowTimeout_WhenPreloadExceedsDeadline()
    {
        var service = new StubReleaseCenterService
        {
            WaitForCancellation = true
        };
        var logger = new RecordingLoggerService<App>();

        await App.WarmAuthorizedResourcesCacheAsync(service, logger, TimeSpan.FromMilliseconds(10));

        Assert.True(service.GetAuthorizedResourcesCalled);
        Assert.Contains(logger.WarningMessages, message => message.Contains("启动时预热授权资源缓存失败", StringComparison.Ordinal));
    }

    private sealed class StubReleaseCenterService : IReleaseCenterService
    {
        public bool GetAuthorizedResourcesCalled { get; private set; }

        public Exception? AuthorizedResourcesException { get; init; }

        public bool WaitForCancellation { get; init; }

        public bool IsConfigured => true;

        public Task<ReleaseCenterCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ResourceDiscoverySnapshot> DiscoverResourcesAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AuthorizedResourceSnapshot> GetAuthorizedResourcesAsync(CancellationToken cancellationToken = default)
        {
            GetAuthorizedResourcesCalled = true;
            if (WaitForCancellation)
            {
                return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
                    .ContinueWith<AuthorizedResourceSnapshot>(_ => new AuthorizedResourceSnapshot(), cancellationToken);
            }

            return AuthorizedResourcesException is null
                ? Task.FromResult(new AuthorizedResourceSnapshot())
                : Task.FromException<AuthorizedResourceSnapshot>(AuthorizedResourcesException);
        }

        public Task<ResourceApplicationSubmitResult> ApplyResourceAsync(string resourceId, string pluginCode, string reason, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ReleaseCenterClientDownloadResult> DownloadLatestClientPackageAsync(IProgress<ReleaseCenterDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ReleaseCenterPluginDownloadResult> DownloadAvailablePluginPackagesAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ReleaseCenterPluginPrepareResult> PrepareCachedPluginPackagesAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ReleaseCenterPluginActivateResult> ActivatePreparedPluginPackagesAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ReleaseCenterPluginRollbackResult> RestoreLatestPluginBackupAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingLoggerService<T> : ILoggerService<T>
    {
        public List<string> WarningMessages { get; } = [];

        public void LogDebug(string message, params object[] args)
        {
        }

        public void LogInformation(string message, params object[] args)
        {
        }

        public void LogWarning(string message, params object[] args)
        {
            WarningMessages.Add(Format(message, args));
        }

        public void LogError(string message, params object[] args)
        {
        }

        public void LogError(Exception exception, string message, params object[] args)
        {
        }

        public void LogCritical(string message, params object[] args)
        {
        }

        private static string Format(string message, object[] args)
        {
            return args.Length == 0 ? message : string.Format(message.Replace("{Message}", "{0}"), args);
        }
    }
}
