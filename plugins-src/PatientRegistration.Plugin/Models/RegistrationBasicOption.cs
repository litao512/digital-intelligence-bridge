namespace PatientRegistration.Plugin.Models;

public class RegistrationBasicOption
{
    public string Value { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string DisplayText => $"{Label}（{Value}）";
}

