using System.Data.Common;
using MedicalDrugImport.Plugin.Configuration;
using MedicalDrugImport.Plugin.Models;
using Microsoft.Data.SqlClient;

namespace MedicalDrugImport.Plugin.Services;

public class SqlServerDrugSyncService : ISqlServerDrugSyncService
{
    private static readonly AsyncLocal<SqlConnection?> CurrentConnection = new();
    private readonly string _connectionString;
    private readonly IDrugCatalogSyncRepository _repository;
    private readonly int _batchSize;
    private readonly int _maxSyncRowsPerRun;
    private readonly bool _allowUnsafeFullSync;
    private readonly bool _enableWrites;

    public SqlServerDrugSyncService(PluginSettings settings, string? connectionString, IDrugCatalogSyncRepository repository)
    {
        _repository = repository;
        ConnectionString = connectionString ?? string.Empty;
        _connectionString = ConnectionString;
        _enableWrites = settings.SqlServer.EnableWrites;
        _batchSize = Math.Max(1, settings.Import.BatchSize);
        _maxSyncRowsPerRun = Math.Max(1, settings.Import.MaxSyncRowsPerRun);
        _allowUnsafeFullSync = settings.Import.AllowUnsafeFullSync;
    }

    public string ConnectionString { get; }

    public async Task<DrugImportBatch> PreviewBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var preview = new DrugImportBatch
        {
            BatchId = batchId,
            Stage = "SyncPreview",
            Result = "Ready"
        };

        preview.SyncUpdateCount = await _repository.CountAffectedCatalogRowsAsync(batchId, cancellationToken);

        if (!_allowUnsafeFullSync && preview.SyncUpdateCount > _maxSyncRowsPerRun)
        {
            preview.Result = "Blocked";
            preview.LastError = $"当前批次待同步记录已超过安全阈值 MaxSyncRowsPerRun={_maxSyncRowsPerRun}。";
            return preview;
        }

        if (!_enableWrites)
        {
            preview.Result = "ReadOnly";
            preview.LastError = "当前插件处于只读模式。若需真实写入 SQL Server，请显式开启 SqlServer.EnableWrites。";
            return preview;
        }

        preview.LastError = _allowUnsafeFullSync
            ? "已显式开启 AllowUnsafeFullSync，可执行超阈值同步。"
            : $"当前批次在安全阈值内，可执行同步。MaxSyncRowsPerRun={_maxSyncRowsPerRun}";

        return preview;
    }

    public async Task<DrugImportBatch> SyncBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var result = new DrugImportBatch
        {
            BatchId = batchId,
            Stage = "Sync",
            Result = "Running"
        };

        if (!_enableWrites)
        {
            throw new InvalidOperationException("当前插件未开启真实 SQL Server 写入。若确认窗口安全，请显式设置 SqlServer.EnableWrites=true。");
        }

        if (!_allowUnsafeFullSync)
        {
            await SyncProtectedBatchAsync(batchId, result, cancellationToken);
            return result;
        }

        await SyncUnsafeBatchAsync(batchId, result, cancellationToken);

        result.SyncedAt = DateTimeOffset.Now;
        result.Result = "Succeeded";
        return result;
    }

    private async Task SyncProtectedBatchAsync(Guid batchId, DrugImportBatch result, CancellationToken cancellationToken)
    {
        var protectedRows = new List<DrugImportRow>(Math.Min(_maxSyncRowsPerRun, _batchSize));

        await foreach (var row in _repository.GetAffectedCatalogRowsAsync(batchId, cancellationToken))
        {
            protectedRows.Add(row);
            if (protectedRows.Count > _maxSyncRowsPerRun)
            {
                throw new InvalidOperationException(
                    $"当前批次待同步记录已超过安全阈值 MaxSyncRowsPerRun={_maxSyncRowsPerRun}。若确认窗口安全，请显式开启 AllowUnsafeFullSync。");
            }
        }

        var pendingRows = new List<DrugImportRow>(_batchSize);
        foreach (var row in protectedRows)
        {
            pendingRows.Add(row);
            if (pendingRows.Count >= _batchSize)
            {
                await FlushBatchAsync(pendingRows, result, cancellationToken);
            }
        }

        await FlushBatchAsync(pendingRows, result, cancellationToken);

        await ExecuteInSessionAsync(
            ct => ExecuteNonQueryAsync(BuildSyncLogCommandText(), BuildSyncLogParameters(batchId, result.SyncUpdateCount), ct),
            cancellationToken);

        result.SyncedAt = DateTimeOffset.Now;
        result.Result = "Succeeded";
    }

    private async Task SyncUnsafeBatchAsync(Guid batchId, DrugImportBatch result, CancellationToken cancellationToken)
    {
        var pendingRows = new List<DrugImportRow>(_batchSize);

        await foreach (var row in _repository.GetAffectedCatalogRowsAsync(batchId, cancellationToken))
        {
            pendingRows.Add(row);
            if (pendingRows.Count >= _batchSize)
            {
                await FlushBatchAsync(pendingRows, result, cancellationToken);
            }
        }

        await FlushBatchAsync(pendingRows, result, cancellationToken);

        await ExecuteInSessionAsync(
            ct => ExecuteNonQueryAsync(BuildSyncLogCommandText(), BuildSyncLogParameters(batchId, result.SyncUpdateCount), ct),
            cancellationToken);
    }

    private async Task FlushBatchAsync(
        List<DrugImportRow> rows,
        DrugImportBatch result,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return;
        }

        await ExecuteInSessionAsync(async ct =>
        {
            await ExecuteNonQueryAsync(BuildSessionSetupCommandText(), Array.Empty<DbParameter>(), ct);

            foreach (var row in rows)
            {
                await ExecuteNonQueryAsync(BuildUpsertCommandText(), BuildUpsertParameters(row), ct);
                result.SyncUpdateCount++;
            }
        }, cancellationToken);

        rows.Clear();
    }

    protected virtual async Task ExecuteInSessionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        if (CurrentConnection.Value is not null)
        {
            await work(cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("SqlServer.ConnectionString 未配置，无法执行插件内 SQL Server 同步。");
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        CurrentConnection.Value = connection;
        try
        {
            await work(cancellationToken);
        }
        finally
        {
            CurrentConnection.Value = null;
        }
    }

    protected virtual async Task ExecuteNonQueryAsync(string commandText, IReadOnlyCollection<DbParameter> parameters, CancellationToken cancellationToken)
    {
        if (CurrentConnection.Value is { } activeConnection)
        {
            await using var activeCommand = new SqlCommand(commandText, activeConnection);
            activeCommand.Parameters.AddRange(parameters is SqlParameter[] sqlParametersInSession ? sqlParametersInSession : parameters.ToArray());
            await activeCommand.ExecuteNonQueryAsync(cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("SqlServer.ConnectionString 未配置，无法执行插件内 SQL Server 同步。");
        }

        await using var transientConnection = new SqlConnection(_connectionString);
        await transientConnection.OpenAsync(cancellationToken);

        await using var transientCommand = new SqlCommand(commandText, transientConnection);
        transientCommand.Parameters.AddRange(parameters is SqlParameter[] sqlParameters ? sqlParameters : parameters.ToArray());
        await transientCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string BuildUpsertCommandText()
    {
        return """
            if exists (select 1 from [dbo].[yb_药品目录] where [药品编码] = @drug_code)
            begin
                update [dbo].[yb_药品目录]
                set
                    [中文名称] = coalesce(@drug_name_cn, [中文名称]),
                    [剂型] = coalesce(@dosage_form, [剂型]),
                    [规格] = coalesce(@specification, [规格]),
                    [update_time] = sysutcdatetime()
                where [药品编码] = @drug_code;
            end
            else
            begin
                insert into [dbo].[yb_药品目录]
                    ([药品编码], [中文名称], [剂型], [规格], [create_time], [update_time])
                values
                    (@drug_code, @drug_name_cn, @dosage_form, @specification, sysutcdatetime(), sysutcdatetime());
            end
            """;
    }

    private static IReadOnlyCollection<DbParameter> BuildUpsertParameters(DrugImportRow row)
    {
        return
        [
            new SqlParameter("@drug_code", row.NormalizedData.GetValueOrDefault("drug_code") ?? row.BusinessKey),
            new SqlParameter("@drug_name_cn", (object?)row.NormalizedData.GetValueOrDefault("drug_name_cn") ?? DBNull.Value),
            new SqlParameter("@dosage_form", (object?)row.NormalizedData.GetValueOrDefault("dosage_form") ?? DBNull.Value),
            new SqlParameter("@specification", (object?)row.NormalizedData.GetValueOrDefault("specification") ?? DBNull.Value)
        ];
    }

    private static string BuildSyncLogCommandText()
    {
        return """
            insert into [dbo].[yb_同步记录]
                ([同步记录], [操作人姓名], [createTime])
            values
                (@message, @operator_name, sysutcdatetime());
            """;
    }

    private static IReadOnlyCollection<DbParameter> BuildSyncLogParameters(Guid batchId, int syncCount)
    {
        return
        [
            new SqlParameter("@message", $"批次 {batchId} 同步药品目录 {syncCount} 条"),
            new SqlParameter("@operator_name", "MedicalDrugImport.Plugin")
        ];
    }

    private static string BuildSessionSetupCommandText()
    {
        return """
            set deadlock_priority low;
            set lock_timeout 5000;
            """;
    }
}
