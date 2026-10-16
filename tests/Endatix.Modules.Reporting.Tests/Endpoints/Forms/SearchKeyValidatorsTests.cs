using Endatix.Modules.Reporting.Endpoints.Forms;
using Endatix.Modules.Reporting.Features.SubmissionGrid;

namespace Endatix.Modules.Reporting.Tests.Endpoints.Forms;

public sealed class AddSearchKeyValidatorTests
{
    private readonly AddSearchKeyValidator _validator = new();

    [Theory]
    [InlineData(1, "q1", true)]
    [InlineData(0, "q1", false)]
    [InlineData(1, "", false)]
    [InlineData(1, null, false)]
    public void Validate_FormIdAndKey_IsValidOnlyWithBoth(long formId, string? key, bool expected)
    {
        // Arrange
        AddSearchKeyRequest request = new() { FormId = formId, Key = key };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().Be(expected);
    }

    [Fact]
    public void Validate_KeyLongerThanAColumnKey_IsInvalid()
    {
        // Arrange
        AddSearchKeyRequest request = new() { FormId = 1, Key = new string('k', SubmissionGridColumnKeys.MaxKeyLength + 1) };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }
}

public sealed class RemoveSearchKeyValidatorTests
{
    private readonly RemoveSearchKeyValidator _validator = new();

    [Theory]
    [InlineData(1, "q1", true)]
    [InlineData(0, "q1", false)]
    [InlineData(1, "", false)]
    public void Validate_FormIdAndKey_IsValidOnlyWithBoth(long formId, string? key, bool expected)
    {
        // Arrange
        RemoveSearchKeyRequest request = new() { FormId = formId, Key = key };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().Be(expected);
    }
}
