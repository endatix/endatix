using Endatix.Infrastructure.Features.Submitters;
using Endatix.Modules.Reporting.Endpoints.Submissions;
using Endatix.Modules.Reporting.Features.SubmissionGrid;
using Microsoft.Extensions.Options;

namespace Endatix.Modules.Reporting.Tests.Endpoints.Submissions;

public sealed class ListGridSubmissionsValidatorTests
{
    private readonly ListGridSubmissionsValidator _validator = new(Options.Create(new SubmitterOptions()));

    [Fact]
    public void Validate_FormIdAndColumns_IsValid()
    {
        // Arrange
        ListGridSubmissionsRequest request = new() { FormId = 1, Columns = "q1,q2" };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_TooManyColumns_IsInvalid()
    {
        // Arrange
        var columns = string.Join(',', Enumerable.Range(0, SubmissionGridColumnKeys.MaxColumns + 1).Select(i => $"q{i}"));
        ListGridSubmissionsRequest request = new() { FormId = 1, Columns = columns };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(ListGridSubmissionsRequest.Columns));
    }

    [Fact]
    public void Validate_ColumnKeyTooLong_IsInvalid()
    {
        // Arrange
        ListGridSubmissionsRequest request = new()
        {
            FormId = 1,
            Columns = new string('q', SubmissionGridColumnKeys.MaxKeyLength + 1),
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.Errors.Should().ContainSingle(error => error.PropertyName == nameof(ListGridSubmissionsRequest.Columns));
    }

    [Fact]
    public void Validate_ListRulesStillApply()
    {
        // Arrange
        ListGridSubmissionsRequest request = new() { FormId = 0 };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.Errors.Should().Contain(error => error.PropertyName == nameof(ListGridSubmissionsRequest.FormId));
    }
}
