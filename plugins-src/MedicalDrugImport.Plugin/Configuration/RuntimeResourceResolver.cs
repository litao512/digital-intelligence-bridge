using System.Text.Json;
using DigitalIntelligenceBridge.Plugin.Abstractions;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace MedicalDrugImport.Plugin.Configuration;

internal static class RuntimeResourceResolver
{
    private const string BusinessDbUsageKey = "business-db";
    private const string SyncTargetUsageKey = "sync-target";

    public static MedicalDrugImportRuntimeResources Resolve(
        string pluginDirectory,
        PluginSettings settings,
        IPluginHostContext? hostContext)
    {
        var development = settings.DevelopmentMode.Enabled
            ? DevelopmentResourceLoader.Load(pluginDirectory)
            : new DevelopmentResourceSettings();

        return new MedicalDrugImportRuntimeResources
        {
            BusinessDbConnectionString = ResolvePostgresConnectionString(hostContext, BusinessDbUsageKey, development.BusinessDbConnectionString),
            SyncTargetConnectionString = ResolveSqlServerConnectionString(hostContext, SyncTargetUsageKey, development.SyncTargetConnectionString)
        };
    }

    private static string ResolvePostgresConnectionString(IPluginHostContext? hostContext, string usageKey, string developmentConnectionString)
    {
        if (hostContext is not null && hostContext.TryGetResource(usageKey, out var resource) && resource is not null)
        {
            var connectionString = BuildPostgresConnectionString(resource.Configuration);
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                hostContext.LogInformation($"已应用宿主下发的 {usageKey} 资源配置");
                return connectionString;
            }
        }

        return developmentConnectionString;
    }

    private static string ResolveSqlServerConnectionString(IPluginHostContext? hostContext, string usageKey, string developmentConnectionString)
    {
        if (hostContext is not null && hostContext.TryGetResource(usageKey, out var resource) && resource is not null)
        {
            var connectionString = BuildSqlServerConnectionString(resource.Configuration);
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                hostContext.LogInformation($"已应用宿主下发的 {usageKey} 资源配置");
                return connectionString;
            }
        }

        return developmentConnectionString;
    }

    private static string BuildPostgresConnectionString(JsonElement configuration)
    {
        if (TryGetString(configuration, "connectionString", out var directConnectionString))
        {
            return directConnectionString;
        }

        if (!TryGetString(configuration, "host", out var host))
        {
            return string.Empty;
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host
        };

        if (TryGetInt(configuration, "port", out var port))
        {
            builder.Port = port;
        }

        if (TryGetString(configuration, "database", out var database))
        {
            builder.Database = database;
        }

        if (TryGetString(configuration, "username", out var username) || TryGetString(configuration, "user", out username))
        {
            builder.Username = username;
        }

        if (TryGetString(configuration, "password", out var password))
        {
            builder.Password = password;
        }

        if (TryGetString(configuration, "searchPath", out var searchPath))
        {
            builder.SearchPath = searchPath;
        }

        if (TryGetString(configuration, "sslMode", out var sslMode)
            && Enum.TryParse<SslMode>(sslMode, true, out var parsedSslMode))
        {
            builder.SslMode = parsedSslMode;
        }

        return builder.ConnectionString;
    }

    private static string BuildSqlServerConnectionString(JsonElement configuration)
    {
        if (TryGetString(configuration, "connectionString", out var directConnectionString))
        {
            return directConnectionString;
        }

        if (!TryGetString(configuration, "host", out var host) && !TryGetString(configuration, "server", out host))
        {
            return string.Empty;
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = TryGetInt(configuration, "port", out var port) ? $"{host},{port}" : host
        };

        if (TryGetString(configuration, "database", out var database))
        {
            builder.InitialCatalog = database;
        }

        if (TryGetString(configuration, "username", out var username) || TryGetString(configuration, "user", out username))
        {
            builder.UserID = username;
        }

        if (TryGetString(configuration, "password", out var password))
        {
            builder.Password = password;
        }

        if (TryGetBool(configuration, "encrypt", out var encrypt))
        {
            builder.Encrypt = encrypt;
        }

        if (TryGetBool(configuration, "trustServerCertificate", out var trustServerCertificate))
        {
            builder.TrustServerCertificate = trustServerCertificate;
        }

        return builder.ConnectionString;
    }

    private static bool TryGetString(JsonElement configuration, string propertyName, out string value)
    {
        value = string.Empty;
        if (configuration.ValueKind != JsonValueKind.Object
            || !configuration.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetInt(JsonElement configuration, string propertyName, out int value)
    {
        value = default;
        return configuration.ValueKind == JsonValueKind.Object
               && configuration.TryGetProperty(propertyName, out var property)
               && property.TryGetInt32(out value);
    }

    private static bool TryGetBool(JsonElement configuration, string propertyName, out bool value)
    {
        value = default;
        if (configuration.ValueKind != JsonValueKind.Object
            || !configuration.TryGetProperty(propertyName, out var property)
            || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }
}

internal sealed class MedicalDrugImportRuntimeResources
{
    public string BusinessDbConnectionString { get; init; } = string.Empty;

    public string SyncTargetConnectionString { get; init; } = string.Empty;
}
