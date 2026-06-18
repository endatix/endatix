using Endatix.Api.Common;
using Endatix.Core.Infrastructure.Paging;
using FluentValidation.TestHelper;

namespace Endatix.Api.Tests.Common;

public sealed class SearchablePagedRequestValidatorTests
{
    private readonly SearchablePagedRequestValidator _validator = new();

    [Fact]
    public void Validate_AcceptsDefaults()
    {
        TestSearchablePagedRequest request = new();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_RejectsPageSizeOutsideLimits(int pageSize)
    {
        TestSearchablePagedRequest request = new() { PageSize = pageSize };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Validate_RejectsSearchAboveMaxLength()
    {
        TestSearchablePagedRequest request = new()
        {
            Search = new string('a', PagedRequestLimits.MAX_SEARCH_LENGTH + 1),
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Search);
    }

    [Fact]
    public void ResolvePageAndPageSize_AppliesSharedDefaults()
    {
        TestSearchablePagedRequest request = new();

        request.ResolvePage().Should().Be(PagedRequestLimits.DEFAULT_PAGE);
        request.ResolvePageSize().Should().Be(PagedRequestLimits.DEFAULT_PAGE_SIZE);
    }

    private sealed class TestSearchablePagedRequest : ISearchablePagedRequest
    {
        public int? Page { get; set; }
        public int? PageSize { get; set; }
        public string? Search { get; set; }
    }
}
