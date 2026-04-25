using System.Text.Json;
using DigitalIntelligenceBridge.Plugin.Abstractions;
using Npgsql;

namespace PatientRegistration.Plugin.Configuration;

internal static class RuntimeResourceResolver
{
    private const string RegistrationDatabaseUsageKey = "registration-db";

    public static string ResolveRegistrationDbConnectionString(
        string pluginDirectory,
        PluginSettings settings,
        IPluginHostContext? hostContext)
    {
        if (hostContext is not null && hostContext.TryGetResource(RegistrationDatabaseUsageKey, out var resource) && resource is not null)
        {
            var connectionString = BuildPostgresConnectionString(resource.Configuration);
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                hostContext.LogInformation("已应用宿主下发的 registration-db 资源配置");
                return connectionString;
            }

            hostContext.LogInformation("宿主已授权 registration-db，但未提供可用 PostgreSQL 连接参数");
        }

        if (!settings.DevelopmentMode.Enabled)
        {
            return string.Empty;
        }

        return DevelopmentResourceLoader.Load(pluginDirectory).RegistrationDbConnectionString;
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

        if (TryGetString(configuration, "sslMode", out var sslMode))
        {
            builder.SslMode = ParseSslMode(sslMode);
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

    private static SslMode ParseSslMode(string sslMode)
    {
        return Enum.TryParse<SslMode>(sslMode, ignoreCase: true, out var parsedSslMode)
            ? parsedSslMode
            : SslMode.Prefer;
    }
}
