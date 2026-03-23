using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace DigitalIntelligenceBridge.Services;

/// <summary>
/// SQL Server 药品目录同步服务
/// </summary>
public class SqlServerDrugSyncService : ISqlServerDrugSyncService
{
    private static readonly AsyncLocal<SqlSession?> CurrentSession = new();
    private readonly SqlServerConnectionConfig _config;
    private readonly IDrugCatalogSyncRepository _repository;

    public SqlServerDrugSyncService(IOptions<AppSettings> settings, IDrugCatalogSyncRepository repository)
    {
        _config = settings.Value.MedicalDrugImport.SqlServer;
        _repository = repository;
    }

    public async Task<DrugImportBatch> SyncBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var result = new DrugImportBatch
        {
            BatchId = batchId,
            Stage = "Sync",
            Result = "Running"
        };

        await ExecuteInSessionAsync(async ct =>
        {
            await foreach (var row in _repository.GetAffectedCatalogRowsAsync(batchId, ct))
            {
                await ExecuteNonQueryAsync(BuildUpsertCommandText(), BuildUpsertParameters(row), ct);
                result.SyncUpdateCount++;
            }

            await ExecuteNonQueryAsync(BuildSyncLogCommandText(), BuildSyncLogParameters(batchId, result.SyncUpdateCount), ct);
        }, cancellationToken);

        result.SyncedAt = DateTimeOffset.Now;
        result.Result = "Succeeded";
        return result;
    }

    protected virtual async Task ExecuteInSessionAsync(
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken)
    {
        if (CurrentSession.Value is not null)
        {
            await work(cancellationToken);
            return;
        }

        await using var connection = new SqlConnection(BuildConnectionString());
        await connection.OpenAsync(cancellationToken);
        var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        CurrentSession.Value = new SqlSession(connection, transaction);
        try
        {
            await work(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            CurrentSession.Value = null;
        }
    }

    protected virtual async Task ExecuteNonQueryAsync(
        string commandText,
        IReadOnlyCollection<DbParameter> parameters,
        CancellationToken cancellationToken)
    {
        if (CurrentSession.Value is { } session)
        {
            await using var transactionalCommand = new SqlCommand(commandText, session.Connection, session.Transaction);
            transactionalCommand.Parameters.AddRange(parameters is SqlParameter[] sessionParameters ? sessionParameters : parameters.ToArray());
            await transactionalCommand.ExecuteNonQueryAsync(cancellationToken);
            return;
        }

        await using var connection = new SqlConnection(BuildConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(commandText, connection);
        command.Parameters.AddRange(parameters is SqlParameter[] sqlParameters ? sqlParameters : parameters.ToArray());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private string BuildConnectionString()
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"{_config.Host},{_config.Port}",
            InitialCatalog = _config.Database,
            UserID = _config.Username,
            Password = _config.Password,
            Encrypt = _config.Encrypt,
            TrustServerCertificate = _config.TrustServerCertificate
        };

        return builder.ConnectionString;
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
            new SqlParameter("@operator_name", "DigitalIntelligenceBridge")
        ];
    }

    private sealed record SqlSession(SqlConnection Connection, SqlTransaction Transaction);
}
