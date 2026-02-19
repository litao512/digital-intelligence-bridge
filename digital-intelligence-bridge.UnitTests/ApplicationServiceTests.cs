using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class ApplicationServiceTests
{
    [Fact]
    public async Task OnStartedAsync_ShouldSkipChecks_WhenSupabaseNotConfigured()
    {
        var supabase = new StubSupabaseService
        {
            IsConfiguredValue = false
        };
        var service = CreateService(supabase);

        await service.OnStartedAsync();

        Assert.Equal(0, supabase.CheckConnectionCalls);
        Assert.Equal(0, supabase.CheckTableAccessCalls);
    }

    [Fact]
    public async Task OnStartedAsync_ShouldNotCheckTable_WhenConnectionNotSuccessful()
    {
        var supabase = new StubSupabaseService
        {
            IsConfiguredValue = true,
            ConnectionResult = new SupabaseConnectionResult(
                IsSuccess: false,
                IsReachable: true,
                StatusCode: HttpStatusCode.Unauthorized,
                Message: "unauthorized")
        };
        var service = CreateService(supabase);

        await service.OnStartedAsync();

        Assert.Equal(1, supabase.CheckConnectionCalls);
        Assert.Equal(0, supabase.CheckTableAccessCalls);
    }

    [Fact]
    public async Task OnStartedAsync_ShouldCheckTodosTable_WhenConnectionSuccessful()
    {
        var supabase = new StubSupabaseService
        {
            IsConfiguredValue = true,
            ConnectionResult = new SupabaseConnectionResult(
                IsSuccess: true,
                IsReachable: true,
                StatusCode: HttpStatusCode.OK,
                Message: "ok"),
            TableResult = new SupabaseConnectionResult(
                IsSuccess: true,
                IsReachable: true,
                StatusCode: HttpStatusCode.OK,
                Message: "table ok")
        };
        var service = CreateService(supabase);

        await service.OnStartedAsync();

        Assert.Equal(1, supabase.CheckConnectionCalls);
        Assert.Equal(1, supabase.CheckTableAccessCalls);
        Assert.Equal("todos", supabase.LastTableName);
    }

    private static ApplicationService CreateService(ISupabaseService supabaseService)
    {
        var settings = new AppSettings
        {
            Supabase = new SupabaseConfig
            {
                Url = "http://localhost:54321",
                AnonKey = "anon-key",
                Schema = "dib"
            }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        return new ApplicationService(
            NullLogger<ApplicationService>.Instance,
            configuration,
            Options.Create(settings),
            new StubTrayService(),
            supabaseService);
    }

    private sealed class StubSupabaseService : ISupabaseService
    {
        public bool IsConfiguredValue { get; set; }
        public int CheckConnectionCalls { get; private set; }
        public int CheckTableAccessCalls { get; private set; }
        public string? LastTableName { get; private set; }

        public SupabaseConnectionResult ConnectionResult { get; set; } = new(
            IsSuccess: true,
            IsReachable: true,
            StatusCode: HttpStatusCode.OK,
            Message: "ok");

        public SupabaseConnectionResult TableResult { get; set; } = new(
            IsSuccess: true,
            IsReachable: true,
            StatusCode: HttpStatusCode.OK,
            Message: "table ok");

        public bool IsConfigured => IsConfiguredValue;

        public Task<SupabaseConnectionResult> CheckConnectionAsync(CancellationToken cancellationToken = default)
        {
            CheckConnectionCalls++;
            return Task.FromResult(ConnectionResult);
        }

        public Task<SupabaseConnectionResult> CheckTableAccessAsync(string tableName, CancellationToken cancellationToken = default)
        {
            CheckTableAccessCalls++;
            LastTableName = tableName;
            return Task.FromResult(TableResult);
        }
    }

    private sealed class StubTrayService : ITrayService
    {
        public bool IsWindowVisible => true;
        public bool IsExiting => false;
        public void AddMenuItem(string header, Action callback, string? parentPath = null) { }
        public void AddSeparator(string? parentPath = null) { }
        public void ExitApplication() { }
        public void HideWindow() { }
        public void Initialize(Avalonia.Controls.Window mainWindow) { }
        public void RemoveMenuItem(string path) { }
        public void SetShowNotifications(bool show) { }
        public void SetTooltip(string tooltip) { }
        public void ShowNotification(string title, string message) { }
        public void ShowWindow() { }
        public void ToggleWindow() { }
    }
}
