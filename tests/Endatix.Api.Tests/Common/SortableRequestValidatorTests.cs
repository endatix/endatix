using Endatix.Api.Common;
using Endatix.Core.Infrastructure.Paging;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Common;

public sealed class SortableRequestValidatorTests
{
    private enum TestSortField
    {
        Name,
        CreatedAt,
    }

    private readonly SortableRequestValidator<TestSortField> _validator = new();

    [Fact]
    public void Validate_AcceptsNullSortValues()
    {
        TestSortableRequest request = new();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_RejectsInvalidSortField()
    {
        TestSortableRequest request = new()
        {
            SortBy = (TestSortField)999,
            Direction = SortDirection.Asc,
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.SortBy);
    }

    [Fact]
    public void ToSortRequest_AppliesDefaultWhenSortMissing()
    {
        TestSortableRequest request = new();

        var result = request.ToSortRequest(TestSortField.Name, SortDirection.Desc);

        result.Field.Should().Be(TestSortField.Name);
        result.Direction.Should().Be(SortDirection.Desc);
    }

    private sealed class TestSortableRequest : ISortableRequest<TestSortField>
    {
        public TestSortField? SortBy { get; set; }
        public SortDirection? Direction { get; set; }
    }
}
