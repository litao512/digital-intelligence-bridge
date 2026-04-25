using System.Text.Json;
using MedicalDrugImport.Plugin.Configuration;
using MedicalDrugImport.Plugin.Models;
using Npgsql;
using NpgsqlTypes;

namespace MedicalDrugImport.Plugin.Services;

public class DrugImportRepository : IDrugImportRepository, IDrugCatalogSyncRepository
{
    private static readonly AsyncLocal<ImportSession?> CurrentSession = new();
    private readonly string _connectionString;
    private const string ImportSchema = "etl";

    public DrugImportRepository(string? connectionString)
    {
        ConnectionString = NormalizeConnectionString(connectionString) ?? string.Empty;
        _connectionString = ConnectionString;
    }

    public string ConnectionString { get; }

    public virtual async Task ExecuteInImportSessionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        if (CurrentSession.Value is not null)
        {
            await work(cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("Postgres.ConnectionString 未配置，无法执行插件内 PostgreSQL 导入。");
        }

        await using var connection = new NpgsqlConnection(_connectionString);
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

    public virtual Task<DrugImportBatch> CreateBatchAsync(string sourceFile, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DrugImportBatch
        {
            BatchId = Guid.NewGuid(),
            SourceFile = sourceFile,
            Stage = "Import",
            Result = "Pending"
        });
    }

    public virtual Task InsertRawBatchAsync(IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken = default)
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

    public virtual Task InsertCleanBatchAsync(IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken = default)
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

    public virtual Task InsertErrorBatchAsync(IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken = default)
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

    public virtual Task MergeBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
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
                from {ImportSchema}.drug_import_clean
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

        return ExecuteNonQueryAsync(commandText, [new NpgsqlParameter("batch_id", batchId)], cancellationToken);
    }

    public virtual async IAsyncEnumerable<DrugImportRow> GetAffectedCatalogRowsAsync(Guid batchId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("Postgres.ConnectionString 未配置，无法查询待同步药品目录。");
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

        await using var connection = new NpgsqlConnection(_connectionString);
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

    public virtual async Task<int> CountAffectedCatalogRowsAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("Postgres.ConnectionString 未配置，无法统计待同步药品目录。");
        }

        const string commandText = """
            select count(1)
            from biz.drug_catalog
            where source_batch_id = @batch_id;
            """;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(commandText, connection);
        command.Parameters.AddWithValue("batch_id", batchId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 0 : Convert.ToInt32(result);
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

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("Postgres.ConnectionString 未配置，无法执行插件内 PostgreSQL 导入。");
        }

        await using var connection = new NpgsqlConnection(_connectionString);
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

    private Task ImportRawWithCopyAsync(NpgsqlConnection connection, IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken)
    {
        return ImportWithCopyAsync(
            connection,
            BuildCopyCommandText(ImportSchema, "drug_import_raw", "batch_id, source_file, source_sheet, row_no, row_data"),
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

    private Task ImportCleanWithCopyAsync(NpgsqlConnection connection, IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken)
    {
        return ImportWithCopyAsync(
            connection,
            BuildCopyCommandText(ImportSchema, "drug_import_clean", "batch_id, source_sheet, row_no, biz_key, normalized_data"),
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

    private Task ImportErrorWithCopyAsync(NpgsqlConnection connection, IReadOnlyList<DrugImportRow> rows, CancellationToken cancellationToken)
    {
        return ImportWithCopyAsync(
            connection,
            BuildCopyCommandText(ImportSchema, "drug_import_error", "batch_id, source_sheet, row_no, error_code, error_message, row_data"),
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
            insert into {ImportSchema}.{tableName}
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
            Database = uri.AbsolutePath.Trim('/')
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

        var query = ParseQueryString(uri.Query);
        if (query.TryGetValue("sslmode", out var sslModeValue) &&
            Enum.TryParse<SslMode>(sslModeValue, true, out var sslMode))
        {
            builder.SslMode = sslMode;
        }

        if (query.TryGetValue("keepalives", out var keepaliveValue) &&
            int.TryParse(keepaliveValue, out var keepalive))
        {
            builder.KeepAlive = keepalive;
        }

        if (query.TryGetValue("keepalives_idle", out var keepaliveIdleValue) &&
            int.TryParse(keepaliveIdleValue, out var keepaliveIdle))
        {
            builder.TcpKeepAliveTime = keepaliveIdle;
        }

        if (query.TryGetValue("keepalives_interval", out var keepaliveIntervalValue) &&
            int.TryParse(keepaliveIntervalValue, out var keepaliveInterval))
        {
            builder.TcpKeepAliveInterval = keepaliveInterval;
        }

        return builder.ConnectionString;
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        foreach (var segment in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = segment.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            result[key] = value;
        }

        return result;
    }
}
