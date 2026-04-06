using System.Net;
using System.Net.Http;
using System.Text;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class ReleaseCenterServiceTests
{
    [Fact]
    public async Task CheckForUpdatesAsync_ShouldReturnAvailableUpdates_WhenManifestHasNewerClientAndPlugins()
    {
        var service = CreateService("""
{
  "latestVersion": "1.2.0",
  "minUpgradeVersion": "1.0.0"
}
""", """
{
  "plugins": [
    { "name": "就诊登记", "version": "1.0.1" },
    { "name": "床旁巡视", "version": "1.0.0" }
  ]
}
""");

        var result = await service.CheckForUpdatesAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("发现 2 类可用更新", result.Summary);
        Assert.Contains("1.2.0", result.ClientSummary);
        Assert.Contains("2 个可用插件", result.PluginSummary);
        Assert.Contains("就诊登记 1.0.1", result.PluginSummary);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ShouldReturnConfiguredFailure_WhenReleaseCenterDisabled()
    {
        var handler = new StubHttpMessageHandler(_ => throw new Xunit.Sdk.XunitException("不应发起 HTTP 请求"));
        var service = new ReleaseCenterService(
            new HttpClient(handler),
            NullLogger<ReleaseCenterService>.Instance,
            Options.Create(new AppSettings
            {
                Application = new ApplicationConfig { Version = "1.0.0" },
                ReleaseCenter = new ReleaseCenterConfig { Enabled = false, BaseUrl = "http://example.com", Channel = "stable" }
            }));

        var result = await service.CheckForUpdatesAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("未配置发布中心", result.Summary);
        Assert.Equal("客户端更新：未配置", result.ClientSummary);
    }

    [Theory]
    [InlineData("1.0.1", "1.0.0", 1)]
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("1.0.0-beta.1", "1.0.0", 0)]
    [InlineData("1.0", "1.0.0", 0)]
    public void CompareVersions_ShouldReturnExpectedResult(string left, string right, int expectedSign)
    {
        var result = ReleaseCenterService.CompareVersions(left, right);

        Assert.Equal(expectedSign, Math.Sign(result));
    }

    private static ReleaseCenterService CreateService(string clientManifest, string pluginManifest)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var content = request.RequestUri!.AbsoluteUri.Contains("client-manifest", StringComparison.OrdinalIgnoreCase)
                ? clientManifest
                : pluginManifest;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };
        });

        return new ReleaseCenterService(
            new HttpClient(handler),
            NullLogger<ReleaseCenterService>.Instance,
            Options.Create(new AppSettings
            {
                Application = new ApplicationConfig { Version = "1.0.0" },
                ReleaseCenter = new ReleaseCenterConfig
                {
                    Enabled = true,
                    BaseUrl = "http://release-center.local",
                    Channel = "stable"
                }
            }));
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}


