using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DigitalIntelligenceBridge.Configuration;
using DigitalIntelligenceBridge.Models;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace DigitalIntelligenceBridge.Services;

/// <summary>
/// PostgreSQL 医保药品导入仓储
/// </summary>
public class DrugImportRepository : IDrugImportRepository, IDrugCatalogSyncRepository
{
    private static readonly AsyncLocal<ImportSession?> CurrentSession = new();
    private readonly string _importSchema;

    public DrugImportRepository(IOptions<AppSettings> settings)
    {
        _importSchema = string.IsNullOrWhiteSpace(settings.Value.MedicalDrugImport.PostgresSchema)
            ? "etl"
            : settings.Value.MedicalDrugImport.PostgresSchema.Trim();
    }

    public Task<DrugImportBatch> CreateBatchAsync(string sourceFile, CancellationToken cancellationToken = default)
    {
        var batch = new DrugImportBatch
        {
            BatchId = Guid.NewGuid(),
            SourceFile = sourceFile,
            Stage = "Import",
            Result = "Pending"
        };

        return Task.FromResult(batch);
    }

    public async Task ExecuteInImportSessionAsync(
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
    {
        if (CurrentSession.Value is not null)
        {
            await work(cancellationToken);
            return;
        }

        var connectionString = NormalizeConnectionString(Environment.GetEnvironmentVariable("POSTGRES_URL"));
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("POSTGRES_URL 未配置，无法执行 PostgreSQL 导入仓储。");
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        CurrentSession.Value = new ImportSession(connection, transaction);
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

    public Task InsertRawAsync(DrugImportRow row, CancellationToken cancellationToken = default)
    {
        var commandText = $"""
            insert into {_importSchema}.drug_import_raw
                (batch_id, source_file, source_sheet, row_no, row_data)
            values
                (@batch_id, @source_file, @source_sheet, @row_no, cast(@row_data as jsonb));
            """;

        var parameters = new[]
        {
            new NpgsqlParameter("batch_id", row.BatchId),
            new NpgsqlParameter("source_file", string.Empty),
            new NpgsqlParameter("source_sheet", row.SourceSheet),
            new NpgsqlParameter("row_no", row.RowNumber),
            new NpgsqlParameter("row_data", JsonSerializer.Serialize(row.RawData))
        };

        return ExecuteNonQueryAsync(commandText, parameters, cancellationToken);
    }

    public Task InsertRawBatchAsync(IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken = default)
    {
        if (CurrentSession.Value is { } session)
        {
            return ImportRawWithCopyAsync(session.Connection, rows, cancellationToken);
        }

        return ExecuteBatchNonQueryAsync(
            rows,
            "drug_import_raw",
            "(batch_id, source_file, source_sheet, row_no, row_data)",
            (row, index) =>
            {
                var valueSql = $"(@batch_id_{index}, @source_file_{index}, @source_sheet_{index}, @row_no_{index}, cast(@row_data_{index} as jsonb))";
                var parameters = new NpgsqlParameter[]
                {
                    new($"batch_id_{index}", row.BatchId),
                    new($"source_file_{index}", string.Empty),
                    new($"source_sheet_{index}", row.SourceSheet),
                    new($"row_no_{index}", row.RowNumber),
                    new($"row_data_{index}", JsonSerializer.Serialize(row.RawData))
                };
                return (valueSql, parameters);
            },
            cancellationToken);
    }

    public Task InsertCleanAsync(DrugImportRow row, CancellationToken cancellationToken = default)
    {
        var commandText = $"""
            insert into {_importSchema}.drug_import_clean
                (batch_id, source_sheet, row_no, biz_key, normalized_data)
            values
                (@batch_id, @source_sheet, @row_no, @biz_key, cast(@normalized_data as jsonb));
            """;

        var parameters = new[]
        {
            new NpgsqlParameter("batch_id", row.BatchId),
            new NpgsqlParameter("source_sheet", row.SourceSheet),
            new NpgsqlParameter("row_no", row.RowNumber),
            new NpgsqlParameter("biz_key", row.BusinessKey),
            new NpgsqlParameter("normalized_data", JsonSerializer.Serialize(row.NormalizedData))
        };

        return ExecuteNonQueryAsync(commandText, parameters, cancellationToken);
    }

    public Task InsertCleanBatchAsync(IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken = default)
    {
        if (CurrentSession.Value is { } session)
        {
            return ImportCleanWithCopyAsync(session.Connection, rows, cancellationToken);
        }

        return ExecuteBatchNonQueryAsync(
            rows,
            "drug_import_clean",
            "(batch_id, source_sheet, row_no, biz_key, normalized_data)",
            (row, index) =>
            {
                var valueSql = $"(@batch_id_{index}, @source_sheet_{index}, @row_no_{index}, @biz_key_{index}, cast(@normalized_data_{index} as jsonb))";
                var parameters = new NpgsqlParameter[]
                {
                    new($"batch_id_{index}", row.BatchId),
                    new($"source_sheet_{index}", row.SourceSheet),
                    new($"row_no_{index}", row.RowNumber),
                    new($"biz_key_{index}", row.BusinessKey),
                    new($"normalized_data_{index}", JsonSerializer.Serialize(row.NormalizedData))
                };
                return (valueSql, parameters);
            },
            cancellationToken);
    }

    public Task InsertErrorAsync(DrugImportRow row, CancellationToken cancellationToken = default)
    {
        var commandText = $"""
            insert into {_importSchema}.drug_import_error
                (batch_id, source_sheet, row_no, error_code, error_message, row_data)
            values
                (@batch_id, @source_sheet, @row_no, @error_code, @error_message, cast(@row_data as jsonb));
            """;

        var parameters = new[]
        {
            new NpgsqlParameter("batch_id", row.BatchId),
            new NpgsqlParameter("source_sheet", row.SourceSheet),
            new NpgsqlParameter("row_no", row.RowNumber),
            new NpgsqlParameter("error_code", row.ErrorCode),
            new NpgsqlParameter("error_message", row.ErrorMessage),
            new NpgsqlParameter("row_data", JsonSerializer.Serialize(row.RawData))
        };

        return ExecuteNonQueryAsync(commandText, parameters, cancellationToken);
    }

    public Task InsertErrorBatchAsync(IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken = default)
    {
        if (CurrentSession.Value is { } session)
        {
            return ImportErrorWithCopyAsync(session.Connection, rows, cancellationToken);
        }

        return ExecuteBatchNonQueryAsync(
            rows,
            "drug_import_error",
            "(batch_id, source_sheet, row_no, error_code, error_message, row_data)",
            (row, index) =>
            {
                var valueSql = $"(@batch_id_{index}, @source_sheet_{index}, @row_no_{index}, @error_code_{index}, @error_message_{index}, cast(@row_data_{index} as jsonb))";
                var parameters = new NpgsqlParameter[]
                {
                    new($"batch_id_{index}", row.BatchId),
                    new($"source_sheet_{index}", row.SourceSheet),
                    new($"row_no_{index}", row.RowNumber),
                    new($"error_code_{index}", row.ErrorCode),
                    new($"error_message_{index}", row.ErrorMessage),
                    new($"row_data_{index}", JsonSerializer.Serialize(row.RawData))
                };
                return (valueSql, parameters);
            },
            cancellationToken);
    }

    public Task MergeBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var commandText = $"""
            with ranked as (
                select
                    normalized_data,
                    source_sheet,
                    row_no,
                    row_number() over (
                        partition by normalized_data->>'drug_code'
                        order by
                            case source_sheet
                                when '总表（270419）' then 1
                                when '变更（449）' then 2
                                when '新增（559）' then 3
                                else 9
                            end,
                            row_no desc
                    ) as rn
                from {_importSchema}.drug_import_clean
                where batch_id = @batch_id
                  and source_sheet <> '关联关系表'
                  and coalesce(normalized_data->>'drug_code', '') <> ''
            )
            insert into biz.drug_catalog
                (drug_code, drug_name_cn, dosage_form, specification, source_batch_id, source_sheet, row_hash, updated_at)
            select
                normalized_data->>'drug_code' as drug_code,
                normalized_data->>'drug_name_cn' as drug_name_cn,
                normalized_data->>'dosage_form' as dosage_form,
                normalized_data->>'specification' as specification,
                @batch_id as source_batch_id,
                source_sheet,
                md5(coalesce(normalized_data::text, '')) as row_hash,
                now() as updated_at
            from ranked
            where rn = 1
            on conflict (drug_code) where drug_code is not null do update
            set
                drug_name_cn = excluded.drug_name_cn,
                dosage_form = excluded.dosage_form,
                specification = excluded.specification,
                source_batch_id = excluded.source_batch_id,
                source_sheet = excluded.source_sheet,
                row_hash = excluded.row_hash,
                updated_at = excluded.updated_at;
            """;

        var parameters = new[]
        {
            new NpgsqlParameter("batch_id", batchId)
        };

        return ExecuteNonQueryAsync(commandText, parameters, cancellationToken);
    }

    public async IAsyncEnumerable<DrugImportRow> GetAffectedCatalogRowsAsync(
        Guid batchId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var connectionString = NormalizeConnectionString(Environment.GetEnvironmentVariable("POSTGRES_URL"));
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("POSTGRES_URL 未配置，无法查询待同步药品目录。");
        }

        const string commandText = """
            select
                drug_code,
                drug_name_cn,
                dosage_form,
                specification
            from biz.drug_catalog
            where source_batch_id = @batch_id
            order by drug_code;
            """;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(commandText, connection);
        command.Parameters.AddWithValue("batch_id", batchId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var drugCode = reader["drug_code"] as string ?? string.Empty;
            var row = new DrugImportRow
            {
                BatchId = batchId,
                BusinessKey = drugCode
            };

            row.NormalizedData["drug_code"] = drugCode;
            AddReaderValue(row, reader, "drug_name_cn");
            AddReaderValue(row, reader, "dosage_form");
            AddReaderValue(row, reader, "specification");
            yield return row;
        }
    }

    protected virtual async Task ExecuteNonQueryAsync(
        string commandText,
        IReadOnlyCollection<NpgsqlParameter> parameters,
        CancellationToken cancellationToken)
    {
        if (CurrentSession.Value is { } session)
        {
            await using var transactionalCommand = new NpgsqlCommand(commandText, session.Connection, session.Transaction);
            transactionalCommand.Parameters.AddRange(parameters.ToArray());
            await transactionalCommand.ExecuteNonQueryAsync(cancellationToken);
            return;
        }

        var connectionString = NormalizeConnectionString(Environment.GetEnvironmentVariable("POSTGRES_URL"));
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("POSTGRES_URL 未配置，无法执行 PostgreSQL 导入仓储。");
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(commandText, connection);
        command.Parameters.AddRange(parameters.ToArray());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddReaderValue(DrugImportRow row, NpgsqlDataReader reader, string columnName)
    {
        if (reader[columnName] is string value && !string.IsNullOrWhiteSpace(value))
        {
            row.NormalizedData[columnName] = value;
        }
    }

    private Task ImportRawWithCopyAsync(
        NpgsqlConnection connection,
        IReadOnlyList<DrugImportRow> rows,
        CancellationToken cancellationToken)
    {
        return ImportWithCopyAsync(
            connection,
            BuildCopyCommandText(_importSchema, "drug_import_raw", "batch_id, source_file, source_sheet, row_no, row_data"),
            rows,
            async (writer, row, ct) =>
            {
                await writer.StartRowAsync(ct);
                await writer.WriteAsync(row.BatchId, NpgsqlDbType.Uuid, ct);
                await writer.WriteAsync(string.Empty, NpgsqlDbType.Text, ct);
                await writer.WriteAsync(row.SourceSheet, NpgsqlDbType.Text, ct);
                await writer.WriteAsync(row.RowNumber, NpgsqlDbType.Integer, ct);
                await writer.WriteAsync(JsonSerializer.Serialize(row.RawData), NpgsqlDbType.Jsonb, ct);
            },
            cancellationToken);
    }

    private Task ImportCleanWithCopyAsync(
        NpgsqlConnection connection,
        IReadOnlyList<DrugImportRow> rows,
        CancellationToken cancellationToken)
    {
        return ImportWithCopyAsync(
            connection,
            BuildCopyCommandText(_importSchema, "drug_import_clean", "batch_id, source_sheet, row_no, biz_key, normalized_data"),
            rows,
            async (writer, row, ct) =>
            {
                await writer.StartRowAsync(ct);
                await writer.WriteAsync(row.BatchId, NpgsqlDbType.Uuid, ct);
                await writer.WriteAsync(row.SourceSheet, NpgsqlDbType.Text, ct);
                await writer.WriteAsync(row.RowNumber, NpgsqlDbType.Integer, ct);
                await writer.WriteAsync(row.BusinessKey, NpgsqlDbType.Text, ct);
                await writer.WriteAsync(JsonSerializer.Serialize(row.NormalizedData), NpgsqlDbType.Jsonb, ct);
            },
            cancellationToken);
    }

    private Task ImportErrorWithCopyAsync(
        NpgsqlConnection connection,
        IReadOnlyList<DrugImportRow> rows,
        CancellationToken cancellationToken)
    {
        return ImportWithCopyAsync(
            connection,
            BuildCopyCommandText(_importSchema, "drug_import_error", "batch_id, source_sheet, row_no, error_code, error_message, row_data"),
            rows,
            async (writer, row, ct) =>
            {
                await writer.StartRowAsync(ct);
                await writer.WriteAsync(row.BatchId, NpgsqlDbType.Uuid, ct);
                await writer.WriteAsync(row.SourceSheet, NpgsqlDbType.Text, ct);
                await writer.WriteAsync(row.RowNumber, NpgsqlDbType.Integer, ct);
                await writer.WriteAsync(row.ErrorCode, NpgsqlDbType.Text, ct);
                await writer.WriteAsync(row.ErrorMessage, NpgsqlDbType.Text, ct);
                await writer.WriteAsync(JsonSerializer.Serialize(row.RawData), NpgsqlDbType.Jsonb, ct);
            },
            cancellationToken);
    }

    private static async Task ImportWithCopyAsync(
        NpgsqlConnection connection,
        string copyCommandText,
        IReadOnlyList<DrugImportRow> rows,
        Func<NpgsqlBinaryImporter, DrugImportRow, CancellationToken, Task> writeRowAsync,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return;
        }

        await using var writer = await connection.BeginBinaryImportAsync(copyCommandText, cancellationToken);
        foreach (var row in rows)
        {
            await writeRowAsync(writer, row, cancellationToken);
        }

        await writer.CompleteAsync(cancellationToken);
    }

    private Task ExecuteBatchNonQueryAsync(
        IReadOnlyList<DrugImportRow> rows,
        string tableName,
        string columnsSql,
        Func<DrugImportRow, int, (string ValueSql, IReadOnlyCollection<NpgsqlParameter> Parameters)> valueFactory,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return Task.CompletedTask;
        }

        var valueSqlList = new List<string>(rows.Count);
        var parameters = new List<NpgsqlParameter>();

        for (var i = 0; i < rows.Count; i++)
        {
            var (valueSql, rowParameters) = valueFactory(rows[i], i);
            valueSqlList.Add(valueSql);
            parameters.AddRange(rowParameters);
        }

        var commandText = $"""
            insert into {_importSchema}.{tableName}
                {columnsSql}
            values
                {string.Join(",\n        ", valueSqlList)};
            """;

        return ExecuteNonQueryAsync(commandText, parameters, cancellationToken);
    }

    private static string BuildCopyCommandText(string schema, string tableName, string columnsSql)
    {
        return $"copy {schema}.{tableName} ({columnsSql}) from stdin (format binary)";
    }

    private sealed record ImportSession(NpgsqlConnection Connection, NpgsqlTransaction Transaction);

    private static string? NormalizeConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        if (!connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) &&
            !connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        var uri = new Uri(connectionString);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
        };

        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo.Length > 0)
        {
            builder.Username = Uri.UnescapeDataString(userInfo[0]);
        }

        if (userInfo.Length > 1)
        {
            builder.Password = Uri.UnescapeDataString(userInfo[1]);
        }

        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        if (!string.IsNullOrWhiteSpace(query["sslmode"]) &&
            Enum.TryParse<SslMode>(query["sslmode"], true, out var sslMode))
        {
            builder.SslMode = sslMode;
        }

        if (!string.IsNullOrWhiteSpace(query["keepalives"]) &&
            int.TryParse(query["keepalives"], out var keepalive))
        {
            builder.KeepAlive = keepalive;
        }

        if (!string.IsNullOrWhiteSpace(query["keepalives_idle"]) &&
            int.TryParse(query["keepalives_idle"], out var keepaliveIdle))
        {
            builder.TcpKeepAliveTime = keepaliveIdle;
        }

        if (!string.IsNullOrWhiteSpace(query["keepalives_interval"]) &&
            int.TryParse(query["keepalives_interval"], out var keepaliveInterval))
        {
            builder.TcpKeepAliveInterval = keepaliveInterval;
        }

        return builder.ConnectionString;
    }
}
