namespace PatientRegistration.Plugin.Models;

public class PatientRegistrationOptionData
{
    public IReadOnlyList<string> Departments { get; set; } = [];

    public IReadOnlyList<RegistrationDoctorOption> Doctors { get; set; } = [];

    public IReadOnlyList<RegistrationTreatmentItemOption> TreatmentItems { get; set; } = [];
}

public class RegistrationDoctorOption
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Department { get; set; } = string.Empty;

    public string DisplayText => string.IsNullOrWhiteSpace(Department) ? Name : $"{Name}（{Department}）";
}

public class RegistrationTreatmentItemOption
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsSelected { get; set; }
}
