namespace PatientRegistration.Plugin.Models;

public class PatientRegistrationSaveResult
{
    public Guid PatientId { get; set; }

    public Guid RegistrationId { get; set; }

    public Guid QrCodeId { get; set; }

    public string QrCodeContent { get; set; } = string.Empty;
}
