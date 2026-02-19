using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using DigitalIntelligenceBridge.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DigitalIntelligenceBridge.Services;

public class SupabaseService : ISupabaseService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SupabaseService> _logger;
    private readonly SupabaseConfig _config;

    public SupabaseService(
        HttpClient httpClient,
        ILogger<SupabaseService> logger,
        IOptions<AppSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _config = settings.Value.Supabase;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_config.Url) &&
        !string.IsNullOrWhiteSpace(_config.AnonKey);

    public async Task<SupabaseConnectionResult> CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new SupabaseConnectionResult(
                IsSuccess: false,
                IsReachable: false,
                StatusCode: null,
                Message: "Supabase configuration is incomplete. Url or AnonKey is missing.");
        }

        try
        {
            var request = CreateRequest(HttpMethod.Get, "/rest/v1/");

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var reachable = true;
            var statusCode = response.StatusCode;

            // Reachability and auth are treated separately so startup can degrade gracefully.
            if (response.IsSuccessStatusCode)
            {
                return new SupabaseConnectionResult(true, reachable, statusCode, "Supabase is reachable and authentication succeeded.");
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return new SupabaseConnectionResult(false, reachable, statusCode, "Supabase is reachable, but credentials are invalid or unauthorized.");
            }

            if ((int)response.StatusCode >= 500)
            {
                return new SupabaseConnectionResult(false, reachable, statusCode, "Supabase is reachable but returned a server-side error.");
            }

            return new SupabaseConnectionResult(false, reachable, statusCode, "Supabase is reachable, but request was rejected.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Supabase connection check failed.");
            return new SupabaseConnectionResult(
                IsSuccess: false,
                IsReachable: false,
                StatusCode: null,
                Message: $"Failed to reach Supabase: {ex.Message}");
        }
    }

    public async Task<SupabaseConnectionResult> CheckTableAccessAsync(string tableName, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new SupabaseConnectionResult(
                IsSuccess: false,
                IsReachable: false,
                StatusCode: null,
                Message: "Supabase configuration is incomplete. Url or AnonKey is missing.");
        }

        if (string.IsNullOrWhiteSpace(tableName))
        {
            return new SupabaseConnectionResult(
                IsSuccess: false,
                IsReachable: false,
                StatusCode: null,
                Message: "Table name is required.");
        }

        try
        {
            var request = CreateRequest(HttpMethod.Get, $"/rest/v1/{tableName}?select=id&limit=1");
            var schema = string.IsNullOrWhiteSpace(_config.Schema) ? "public" : _config.Schema;
            request.Headers.TryAddWithoutValidation("Accept-Profile", schema);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var reachable = true;
            var statusCode = response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                return new SupabaseConnectionResult(true, reachable, statusCode, $"Table '{schema}.{tableName}' is accessible.");
            }

            return new SupabaseConnectionResult(false, reachable, statusCode, $"Failed to access table '{schema}.{tableName}'.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Supabase table access check failed.");
            return new SupabaseConnectionResult(
                IsSuccess: false,
                IsReachable: false,
                StatusCode: null,
                Message: $"Failed to reach Supabase: {ex.Message}");
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath)
    {
        var baseUrl = _config.Url.TrimEnd('/');
        var request = new HttpRequestMessage(method, $"{baseUrl}{relativePath}");
        request.Headers.Add("apikey", _config.AnonKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.AnonKey);
        return request;
    }
}
