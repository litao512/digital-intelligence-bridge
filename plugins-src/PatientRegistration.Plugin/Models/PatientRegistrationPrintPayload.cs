namespace PatientRegistration.Plugin.Models;

public class PatientRegistrationPrintPayload
{
    public Guid RegistrationId { get; set; }

    public string PatientName { get; set; } = string.Empty;

    public string IdType { get; set; } = string.Empty;

    public string IdNumberMasked { get; set; } = string.Empty;

    public string QrCodeContent { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;
}
