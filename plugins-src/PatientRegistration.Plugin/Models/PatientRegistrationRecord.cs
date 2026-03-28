namespace PatientRegistration.Plugin.Models;

public class PatientRegistrationRecord
{
    public Guid RegistrationId { get; set; }

    public string PatientName { get; set; } = string.Empty;

    public string? Department { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string QrCodeContent { get; set; } = string.Empty;

    public string IdType { get; set; } = string.Empty;

    public string IdNumberMasked { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string DisplayText => $"{CreatedAt:yyyy-MM-dd HH:mm} | {PatientName} | {Department ?? "未登记科室"} | {IdNumberMasked}";
}
