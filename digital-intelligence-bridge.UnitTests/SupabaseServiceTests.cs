using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class SupabaseServiceTests
{
    [Fact]
    public async Task CheckConnectionAsync_ShouldReturnNotConfigured_WhenUrlOrKeyMissing()
    {
        var service = CreateService(new AppSettings
        {
            Supabase = new SupabaseConfig
            {
                Url = string.Empty,
                AnonKey = string.Empty
            }
        }, _ => new HttpResponseMessage(HttpStatusCode.OK));

        var result = await service.CheckConnectionAsync();

        Assert.False(service.IsConfigured);
        Assert.False(result.IsSuccess);
        Assert.False(result.IsReachable);
        Assert.Contains("incomplete", result.Message);
    }

    [Fact]
    public async Task CheckConnectionAsync_ShouldReturnReachable_WhenUnauthorized()
    {
        var service = CreateService(new AppSettings
        {
            Supabase = new SupabaseConfig
            {
                Url = "http://localhost:54321",
                AnonKey = "invalid-key"
            }
        }, _ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var result = await service.CheckConnectionAsync();

        Assert.True(service.IsConfigured);
        Assert.False(result.IsSuccess);
        Assert.True(result.IsReachable);
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
    }

    [Fact]
    public async Task CheckConnectionAsync_ShouldReturnSuccess_WhenResponseIsOk()
    {
        var service = CreateService(new AppSettings
        {
            Supabase = new SupabaseConfig
            {
                Url = "http://localhost:54321",
                AnonKey = "anon-key"
            }
        }, _ => new HttpResponseMessage(HttpStatusCode.OK));

        var result = await service.CheckConnectionAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.IsReachable);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    [Fact]
    public async Task CheckTableAccessAsync_ShouldReturnSuccess_WhenTableIsReachable()
    {
        HttpRequestMessage? captured = null;
        var service = CreateService(new AppSettings
        {
            Supabase = new SupabaseConfig
            {
                Url = "http://localhost:54321",
                AnonKey = "anon-key",
                Schema = "dib"
            }
        }, req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var result = await service.CheckTableAccessAsync("todos");

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        Assert.Contains("/rest/v1/todos", captured!.RequestUri!.ToString());
        Assert.True(captured.Headers.Contains("Accept-Profile"));
    }

    private static SupabaseService CreateService(AppSettings settings, Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHttpMessageHandler(responder);
        var httpClient = new HttpClient(handler);
        return new SupabaseService(httpClient, NullLogger<SupabaseService>.Instance, Options.Create(settings));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}
