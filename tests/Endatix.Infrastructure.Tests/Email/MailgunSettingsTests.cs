using System.ComponentModel.DataAnnotations;
using Endatix.Infrastructure.Email;

namespace Endatix.Infrastructure.Tests.Email;

public class MailgunSettingsTests
{
    [Fact]
    public void Validate_NothingSet_RequiredFieldsFail()
    {
        // Arrange
        var settings = new MailgunSettings();
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(settings, new ValidationContext(settings), results, validateAllProperties: true);

        // Assert
        isValid.Should().BeFalse();
        results.SelectMany(r => r.MemberNames).Should().BeEquivalentTo(
            nameof(MailgunSettings.ApiKey), nameof(MailgunSettings.BaseUrl), nameof(MailgunSettings.Domain));
    }
}
