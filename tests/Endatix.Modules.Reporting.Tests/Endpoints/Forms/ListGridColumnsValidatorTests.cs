using Endatix.Core.Common.Translations;
using Endatix.Modules.Reporting.Endpoints.Forms;

namespace Endatix.Modules.Reporting.Tests.Endpoints.Forms;

public sealed class ListGridColumnsValidatorTests
{
    private readonly ListGridColumnsValidator _validator = new();

    [Fact]
    public void Validate_FormIdAndLocale_IsValid()
    {
        // Arrange
        ListGridColumnsRequest request = new() { FormId = 1, Locale = "es" };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_FormIdZero_IsInvalid()
    {
        // Arrange
        ListGridColumnsRequest request = new() { FormId = 0 };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(ListGridColumnsRequest.FormId));
    }

    [Fact]
    public void Validate_LocaleOverMaxLength_IsInvalid()
    {
        // Arrange
        ListGridColumnsRequest request = new()
        {
            FormId = 1,
            Locale = new string('a', IHasTranslations.MAX_CULTURE_CODE_LENGTH + 1),
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(ListGridColumnsRequest.Locale));
    }
}
