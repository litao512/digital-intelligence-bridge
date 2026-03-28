using PatientRegistration.Plugin.Utils;
using Xunit;

namespace DigitalIntelligenceBridge.UnitTests;

public class RegistrationCodeFormatterTests
{
    [Fact]
    public void Format_ShouldReturnRegPrefixAndFirst8Chars()
    {
        var id = Guid.Parse("123e4567-e89b-12d3-a456-426614174000");

        var code = RegistrationCodeFormatter.Format(id);

        Assert.Equal("REG-123E4567", code);
    }
}

