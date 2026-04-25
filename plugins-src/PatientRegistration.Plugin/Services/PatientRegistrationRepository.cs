using System.Text.RegularExpressions;
using Npgsql;
using NpgsqlTypes;
using PatientRegistration.Plugin.Configuration;
using PatientRegistration.Plugin.Models;

namespace PatientRegistration.Plugin.Services;

public class PatientRegistrationRepository : IPatientRegistrationRepository
{
    private readonly string _connectionString;

    public PatientRegistrationRepository(string? connectionString)
    {
        _connectionString = NormalizeConnectionString(connectionString) ?? string.Empty;
    }

    public async Task<PatientRegistrationSaveResult> SaveAsync(
        PatientRegistrationDraft draft,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("Postgres.ConnectionString 未配置，无法保存就诊登记。");
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var patientId = await UpsertPatientAsync(connection, transaction, draft, cancellationToken);
            var qr = await GetOrCreateIdentityQrAsync(connection, transaction, patientId, cancellationToken);
            var doctorId = await ResolveDoctorIdAsync(connection, transaction, draft.DoctorName, cancellationToken);
            var plannedItemIds = await ResolveTreatmentItemIdsAsync(connection, transaction, draft.PlannedTreatmentItemNames, cancellationToken);
            var (visitStart, visitEnd) = ParseVisitTimeRange(draft.VisitTimeRange);

            var registrationId = await InsertRegistrationAsync(
                connection,
                transaction,
                patientId,
                qr.qrCodeId,
                doctorId,
                plannedItemIds,
                draft,
                visitStart,
                visitEnd,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return new PatientRegistrationSaveResult
            {
                PatientId = patientId,
                RegistrationId = registrationId,
                QrCodeId = qr.qrCodeId,
                QrCodeContent = qr.qrCode
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<PatientRegistrationRecord>> GetRecentRegistrationsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("Postgres.ConnectionString 未配置，无法查询登记记录。");
        }

        const string sql = """
            select
                r.id as registration_id,
                u.name as patient_name,
                r.department,
                r.created_at,
                coalesce(q.code, '') as qr_code_content,
                coalesce(p.id_type, '') as id_type,
                coalesce(r.notes, '') as notes,
                case
                    when p.id_number is null or length(p.id_number) <= 4 then coalesce(p.id_number, '')
                    else repeat('*', greatest(length(p.id_number) - 4, 0)) || right(p.id_number, 4)
                end as id_number_masked
            from app.patient_visit_registrations r
            join app.users u on u.id = r.patient_id
            left join app.patient_profiles p on p.patient_id = r.patient_id
            left join app.qr_codes q on q.id = r.qr_code_id
            order by r.created_at desc
            limit @limit;
            """;

        var result = new List<PatientRegistrationRecord>();
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new PatientRegistrationRecord
            {
                RegistrationId = reader.GetGuid(reader.GetOrdinal("registration_id")),
                PatientName = reader["patient_name"] as string ?? string.Empty,
                Department = reader["department"] as string,
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at")),
                QrCodeContent = reader["qr_code_content"] as string ?? string.Empty,
                IdType = reader["id_type"] as string ?? string.Empty,
                Notes = reader["notes"] as string ?? string.Empty,
                IdNumberMasked = reader["id_number_masked"] as string ?? string.Empty
            });
        }

        return result;
    }

    public async Task<PatientRegistrationOptionData> GetRegistrationOptionsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("Postgres.ConnectionString 未配置，无法查询登记选项。");
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var departments = new List<string>();
        const string departmentSql = """
            select distinct department
            from app.doctors
            where coalesce(department, '') <> ''
            order by department;
            """;
        await using (var command = new NpgsqlCommand(departmentSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                departments.Add(reader.GetString(0));
            }
        }

        var doctors = new List<RegistrationDoctorOption>();
        const string doctorSql = """
            select id::text, name, coalesce(department, '')
            from app.doctors
            where coalesce(name, '') <> ''
            order by department, name;
            """;
        await using (var command = new NpgsqlCommand(doctorSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                doctors.Add(new RegistrationDoctorOption
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    Department = reader.GetString(2)
                });
            }
        }

        var treatmentItems = new List<RegistrationTreatmentItemOption>();
        const string itemSql = """
            select id::text, name
            from app.treatment_items
            where active = true and coalesce(name, '') <> ''
            order by name;
            """;
        await using (var command = new NpgsqlCommand(itemSql, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                treatmentItems.Add(new RegistrationTreatmentItemOption
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    IsSelected = false
                });
            }
        }

        return new PatientRegistrationOptionData
        {
            Departments = departments,
            Doctors = doctors,
            TreatmentItems = treatmentItems
        };
    }

    private static async Task<Guid> UpsertPatientAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        PatientRegistrationDraft draft,
        CancellationToken cancellationToken)
    {
        var existingPatientId = await FindPatientByProfileAsync(connection, transaction, draft.IdType, draft.IdNumber, cancellationToken);
        if (existingPatientId.HasValue)
        {
            const string updateUserSql = """
                update app.users
                set
                    name = @name,
                    phone = nullif(@phone, ''),
                    updated_at = now()
                where id = @patient_id;
                """;

            await using var updateUser = new NpgsqlCommand(updateUserSql, connection, transaction);
            updateUser.Parameters.AddWithValue("name", draft.PatientName.Trim());
            updateUser.Parameters.AddWithValue("phone", draft.ContactPhone?.Trim() ?? string.Empty);
            updateUser.Parameters.AddWithValue("patient_id", existingPatientId.Value);
            await updateUser.ExecuteNonQueryAsync(cancellationToken);

            await UpsertPatientProfileAsync(connection, transaction, existingPatientId.Value, draft, cancellationToken);
            return existingPatientId.Value;
        }

        var patientId = Guid.NewGuid();
        var fallbackEmail = BuildPatientEmail(draft.IdType, draft.IdNumber);

        const string insertUserSql = """
            insert into app.users (id, email, role, name, phone)
            values (@id, @email, 'patient', @name, nullif(@phone, ''));
            """;

        await using (var insertUser = new NpgsqlCommand(insertUserSql, connection, transaction))
        {
            insertUser.Parameters.AddWithValue("id", patientId);
            insertUser.Parameters.AddWithValue("email", fallbackEmail);
            insertUser.Parameters.AddWithValue("name", draft.PatientName.Trim());
            insertUser.Parameters.AddWithValue("phone", draft.ContactPhone?.Trim() ?? string.Empty);
            await insertUser.ExecuteNonQueryAsync(cancellationToken);
        }

        await UpsertPatientProfileAsync(connection, transaction, patientId, draft, cancellationToken);
        return patientId;
    }

    private static async Task UpsertPatientProfileAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid patientId,
        PatientRegistrationDraft draft,
        CancellationToken cancellationToken)
    {
        const string upsertProfileSql = """
            insert into app.patient_profiles (
                patient_id,
                id_type,
                id_number,
                gender,
                birth_date,
                contact_phone,
                source_channel
            ) values (
                @patient_id,
                @id_type,
                @id_number,
                @gender,
                @birth_date,
                nullif(@contact_phone, ''),
                'dib'
            )
            on conflict (patient_id) do update
            set
                id_type = excluded.id_type,
                id_number = excluded.id_number,
                gender = excluded.gender,
                birth_date = excluded.birth_date,
                contact_phone = excluded.contact_phone,
                source_channel = 'mixed',
                updated_at = now();
            """;

        await using var command = new NpgsqlCommand(upsertProfileSql, connection, transaction);
        command.Parameters.AddWithValue("patient_id", patientId);
        command.Parameters.AddWithValue("id_type", draft.IdType.Trim());
        command.Parameters.AddWithValue("id_number", draft.IdNumber.Trim());
        command.Parameters.AddWithValue("gender", NormalizeGender(draft.Gender));
        command.Parameters.AddWithValue("birth_date", draft.BirthDate.Date);
        command.Parameters.AddWithValue("contact_phone", draft.ContactPhone?.Trim() ?? string.Empty);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Guid?> FindPatientByProfileAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string idType,
        string idNumber,
        CancellationToken cancellationToken)
    {
        const string sql = """
            select p.patient_id
            from app.patient_profiles p
            where p.id_type = @id_type and p.id_number = @id_number
            limit 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id_type", idType.Trim());
        command.Parameters.AddWithValue("id_number", idNumber.Trim());
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid guid ? guid : null;
    }

    private static async Task<(Guid qrCodeId, string qrCode)> GetOrCreateIdentityQrAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid patientId,
        CancellationToken cancellationToken)
    {
        const string findSql = """
            select id, code
            from app.qr_codes
            where patient_id = @patient_id and type = 'service_lookup'
            order by created_at desc
            limit 1;
            """;

        await using (var find = new NpgsqlCommand(findSql, connection, transaction))
        {
            find.Parameters.AddWithValue("patient_id", patientId);
            await using var reader = await find.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetGuid(0);
                var code = reader.GetString(1);
                return (id, code);
            }
        }

        var qrCode = $"REG_{patientId:N}";

        const string insertSql = """
            insert into app.qr_codes (patient_id, appointment_id, code, type, used)
            values (@patient_id, null, @code, 'service_lookup', false)
            returning id;
            """;

        await using var insert = new NpgsqlCommand(insertSql, connection, transaction);
        insert.Parameters.AddWithValue("patient_id", patientId);
        insert.Parameters.AddWithValue("code", qrCode);
        var result = await insert.ExecuteScalarAsync(cancellationToken);
        if (result is not Guid qrCodeId)
        {
            throw new InvalidOperationException("创建身份二维码失败，未返回二维码 ID。");
        }

        return (qrCodeId, qrCode);
    }

    private static async Task<Guid?> ResolveDoctorIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string? doctorName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(doctorName))
        {
            return null;
        }

        const string sql = """
            select id
            from app.doctors
            where name = @name
            order by updated_at desc
            limit 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("name", doctorName.Trim());
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is Guid doctorId ? doctorId : null;
    }

    private static async Task<Guid[]> ResolveTreatmentItemIdsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<string> itemNames,
        CancellationToken cancellationToken)
    {
        var normalizedNames = itemNames
            .Select(name => name.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedNames.Length == 0)
        {
            return [];
        }

        const string sql = """
            select id
            from app.treatment_items
            where active = true and name = any(@names);
            """;

        var result = new List<Guid>();

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("names", NpgsqlDbType.Array | NpgsqlDbType.Text, normalizedNames);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(reader.GetGuid(0));
        }

        return [.. result];
    }

    private static async Task<Guid> InsertRegistrationAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid patientId,
        Guid qrCodeId,
        Guid? doctorId,
        Guid[] plannedItemIds,
        PatientRegistrationDraft draft,
        DateTimeOffset? visitTimeStart,
        DateTimeOffset? visitTimeEnd,
        CancellationToken cancellationToken)
    {
        const string sql = """
            insert into app.patient_visit_registrations (
                patient_id,
                qr_code_id,
                department,
                doctor_id,
                planned_treatment_item_ids,
                visit_time_start,
                visit_time_end,
                status,
                registration_source,
                notes,
                registered_by
            ) values (
                @patient_id,
                @qr_code_id,
                nullif(@department, ''),
                @doctor_id,
                @planned_treatment_item_ids,
                @visit_time_start,
                @visit_time_end,
                'registered',
                'desk',
                nullif(@notes, ''),
                null
            )
            returning id;
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("patient_id", patientId);
        command.Parameters.AddWithValue("qr_code_id", qrCodeId);
        command.Parameters.AddWithValue("department", draft.Department?.Trim() ?? string.Empty);
        command.Parameters.Add(new NpgsqlParameter("doctor_id", NpgsqlDbType.Uuid)
        {
            Value = doctorId.HasValue ? doctorId.Value : DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("planned_treatment_item_ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
        {
            Value = plannedItemIds.Length == 0 ? DBNull.Value : plannedItemIds
        });
        command.Parameters.Add(new NpgsqlParameter("visit_time_start", NpgsqlDbType.TimestampTz)
        {
            Value = visitTimeStart.HasValue ? visitTimeStart.Value.UtcDateTime : DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("visit_time_end", NpgsqlDbType.TimestampTz)
        {
            Value = visitTimeEnd.HasValue ? visitTimeEnd.Value.UtcDateTime : DBNull.Value
        });
        command.Parameters.AddWithValue("notes", draft.Notes?.Trim() ?? string.Empty);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not Guid registrationId)
        {
            throw new InvalidOperationException("保存就诊登记失败，未返回登记 ID。");
        }

        return registrationId;
    }

    private static string BuildPatientEmail(string idType, string idNumber)
    {
        var normalizedIdType = Regex.Replace(idType.Trim().ToLowerInvariant(), "[^a-z0-9]", string.Empty);
        var normalizedIdNumber = Regex.Replace(idNumber.Trim().ToLowerInvariant(), "[^a-z0-9]", string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedIdType))
        {
            normalizedIdType = "id";
        }

        if (string.IsNullOrWhiteSpace(normalizedIdNumber))
        {
            normalizedIdNumber = Guid.NewGuid().ToString("N");
        }

        return $"{normalizedIdType}-{normalizedIdNumber}@dib.local";
    }

    private static string NormalizeGender(string gender)
    {
        return gender.Trim().ToLowerInvariant() switch
        {
            "male" => "male",
            "female" => "female",
            _ => "unknown"
        };
    }

    private static (DateTimeOffset? Start, DateTimeOffset? End) ParseVisitTimeRange(string? visitTimeRange)
    {
        if (string.IsNullOrWhiteSpace(visitTimeRange))
        {
            return (null, null);
        }

        var matches = Regex.Matches(
            visitTimeRange,
            @"\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}",
            RegexOptions.CultureInvariant);

        if (matches.Count < 2)
        {
            return (null, null);
        }

        if (!DateTimeOffset.TryParse(matches[0].Value, out var start))
        {
            return (null, null);
        }

        if (!DateTimeOffset.TryParse(matches[1].Value, out var end))
        {
            return (start, null);
        }

        return (start, end);
    }

    private static string? NormalizeConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        if (!connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            && !connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
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
        if (query.TryGetValue("sslmode", out var sslModeValue)
            && Enum.TryParse<SslMode>(sslModeValue, true, out var sslMode))
        {
            builder.SslMode = sslMode;
        }

        if (query.TryGetValue("keepalives", out var keepaliveValue)
            && int.TryParse(keepaliveValue, out var keepalive))
        {
            builder.KeepAlive = keepalive;
        }

        if (query.TryGetValue("keepalives_idle", out var keepaliveIdleValue)
            && int.TryParse(keepaliveIdleValue, out var keepaliveIdle))
        {
            builder.TcpKeepAliveTime = keepaliveIdle;
        }

        if (query.TryGetValue("keepalives_interval", out var keepaliveIntervalValue)
            && int.TryParse(keepaliveIntervalValue, out var keepaliveInterval))
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
