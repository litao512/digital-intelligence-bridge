using PatientRegistration.Plugin.Models;

namespace PatientRegistration.Plugin.Services;

public interface IQrPrintService
{
    Task PrintAsync(PatientRegistrationPrintPayload payload, CancellationToken cancellationToken = default);
}
