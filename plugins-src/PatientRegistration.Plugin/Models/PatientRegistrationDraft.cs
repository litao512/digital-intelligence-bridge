namespace PatientRegistration.Plugin.Models;

public class PatientRegistrationDraft
{
    public string PatientName { get; set; } = string.Empty;

    public string Gender { get; set; } = "unknown";

    public DateTime BirthDate { get; set; }

    public string IdType { get; set; } = "id_card";

    public string IdNumber { get; set; } = string.Empty;

    public string? ContactPhone { get; set; }

    public string? Department { get; set; }

    public string? DoctorName { get; set; }

    public string? VisitTimeRange { get; set; }

    public IReadOnlyList<string> PlannedTreatmentItemNames { get; set; } = [];

    public string? Notes { get; set; }
}
