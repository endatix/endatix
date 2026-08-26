using Endatix.Api.Common;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Common;

/// <summary>
/// Exercises the shared calendar-day range rules through <see cref="CreatedRangeValidator"/>,
/// the composable validator endpoints actually <c>Include</c>. The other stems
/// (Modified/Started/Completed/LastLogin) are the same helper bound to different properties.
/// </summary>
public sealed class CalendarDayRangeValidatorTests
{
    private sealed class CreatedRangeStub : ICreatedRange
    {
        public string? CreatedFrom { get; set; }
        public string? CreatedTo { get; set; }
    }

    private readonly CreatedRangeValidator _validator = new();

    [Fact]
    public void Validate_RejectsFromAfterTo()
    {
        CreatedRangeStub request = new()
        {
            CreatedFrom = "2024-02-01",
            CreatedTo = "2024-01-01",
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.CreatedFrom);
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

    [Fact]
    public void Validate_AllowsOmittedBounds()
    {
        var result = _validator.TestValidate(new CreatedRangeStub());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("2024-1-5")]
    [InlineData("01/15/2024")]
    [InlineData("2024-01-15T00:00:00Z")]
    [InlineData("not-a-date")]
    [InlineData("2024-02-30")]
    public void Validate_RejectsMalformedCalendarDay(string value)
    {
        var result = _validator.TestValidate(new CreatedRangeStub { CreatedFrom = value });

        result.ShouldHaveValidationErrorFor(x => x.CreatedFrom);
    }

    [Fact]
    public void Validate_ReportsCrossFieldErrorAgainstTheFromProperty()
    {
        CreatedRangeStub request = new()
        {
            CreatedFrom = "2024-02-01",
            CreatedTo = "2024-01-01",
        };

        var result = _validator.TestValidate(request);

        result.Errors.Should().OnlyContain(e => e.PropertyName == nameof(ICreatedRange.CreatedFrom));
    }
}
