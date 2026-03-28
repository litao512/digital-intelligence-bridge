using PatientRegistration.Plugin.Models;

namespace PatientRegistration.Plugin.Services;

public interface IPatientRegistrationRepository
{
    Task<PatientRegistrationSaveResult> SaveAsync(PatientRegistrationDraft draft, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PatientRegistrationRecord>> GetRecentRegistrationsAsync(int limit = 20, CancellationToken cancellationToken = default);

    Task<PatientRegistrationOptionData> GetRegistrationOptionsAsync(CancellationToken cancellationToken = default);
}
