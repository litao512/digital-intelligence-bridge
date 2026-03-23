using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Models;
using DigitalIntelligenceBridge.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class ApplicationServiceTests
{
    [Fact]
    public void DrugImportModels_ShouldCaptureBatchRowAndPreviewState()
    {
        var batchId = Guid.NewGuid();
        var batch = new DrugImportBatch
        {
            BatchId = batchId,
            SourceFile = @"C:\imports\medical-drug.xlsx",
            Stage = "Import",
            Result = "Running",
            RawCount = 10,
            CleanCount = 8,
            ErrorCount = 2,
            LastError = "row 8 invalid"
        };

        var row = new DrugImportRow
        {
            BatchId = batchId,
            SourceSheet = "总表（270419）",
            RowNumber = 8,
            BusinessKey = "XA01ABD075A002010100483",
            RawData = new Dictionary<string, string?>
            {
                ["药品代码"] = "XA01ABD075A002010100483",
                ["注册名称"] = "地喹氯铵含片"
            },
            NormalizedData = new Dictionary<string, string?>
            {
                ["drug_code"] = "XA01ABD075A002010100483",
                ["drug_name_cn"] = "地喹氯铵含片"
            },
            ErrorCode = "REQUIRED_FIELD_MISSING",
            ErrorMessage = "药品代码不能为空"
        };

        var preview = new DrugImportPreview
        {
            FilePath = batch.SourceFile,
            IsValid = true,
            Summary = "工作表完整",
            SheetRowCounts = new Dictionary<string, int>
            {
                ["总表（270419）"] = 270419,
                ["新增（559）"] = 559
            },
            Errors = new List<string>
            {
                "变更（449）存在 1 行空药品代码"
            }
        };

        Assert.Equal(batchId, batch.BatchId);
        Assert.Equal("Running", batch.Result);
        Assert.Equal("总表（270419）", row.SourceSheet);
        Assert.Equal("XA01ABD075A002010100483", row.BusinessKey);
        Assert.Equal("地喹氯铵含片", row.NormalizedData["drug_name_cn"]);
        Assert.True(preview.IsValid);
        Assert.Equal(270419, preview.SheetRowCounts["总表（270419）"]);
        Assert.Single(preview.Errors);
    }

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
