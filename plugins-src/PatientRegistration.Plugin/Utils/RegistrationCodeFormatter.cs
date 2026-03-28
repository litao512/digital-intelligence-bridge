namespace PatientRegistration.Plugin.Utils;

public static class RegistrationCodeFormatter
{
    public static string Format(Guid registrationId)
    {
        var compact = registrationId.ToString("N").ToUpperInvariant();
        return $"REG-{compact[..8]}";
    }
}

