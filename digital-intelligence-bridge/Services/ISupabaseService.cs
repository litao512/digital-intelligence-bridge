using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalIntelligenceBridge.Services;

public interface ISupabaseService
{
    bool IsConfigured { get; }

    Task<SupabaseConnectionResult> CheckConnectionAsync(CancellationToken cancellationToken = default);

    Task<SupabaseConnectionResult> CheckTableAccessAsync(string tableName, CancellationToken cancellationToken = default);
}

public sealed record SupabaseConnectionResult(
    bool IsSuccess,
    bool IsReachable,
    HttpStatusCode? StatusCode,
    string Message);
