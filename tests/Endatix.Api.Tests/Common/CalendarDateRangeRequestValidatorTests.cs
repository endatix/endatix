using Endatix.Api.Common;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Common;

public sealed class CalendarDateRangeRequestValidatorTests
{
    private readonly CreatedRangeRequestValidator _validator = new();

    private sealed class CreatedRangeStub : ICreatedRange
    {
        public string? CreatedFrom { get; set; }
        public string? CreatedTo { get; set; }
    }

    [Fact]
    public void Validate_RejectsFromAfterTo()
    {
        CreatedRangeStub request = new()
        {
            CreatedFrom = "2024-02-01",
            CreatedTo = "2024-01-01",
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor("CreatedFrom");
    }

    [Fact]
    public void Validate_AllowsEqualBounds()
    {
        CreatedRangeStub request = new()
        {
            CreatedFrom = "2024-01-15",
            CreatedTo = "2024-01-15",
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
