using Endatix.Modules.Reporting.Contracts.Export;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

/// <summary>
/// Documents the capability → allowedFilters API-name contract used by Hub and the export API.
/// </summary>
public sealed class AllowedExportFiltersTests
{
    [Fact]
    public void ToAllowedFilterNames_WhenSubmissions_ReturnsRowFiltersWithoutLocale()
    {
        IReadOnlyList<string> names = AllowedExportFilters.ToAllowedFilterNames(
            ExportRequestFilterSets.Submissions);

        names.Should().Equal(
            AllowedExportFilters.IncludeTestSubmissions,
            AllowedExportFilters.CreatedAtRange,
            AllowedExportFilters.CompletedAtRange,
            AllowedExportFilters.SubmissionIdRange,
            AllowedExportFilters.ColumnScope,
            AllowedExportFilters.CompletionStatus);
        names.Should().NotContain(AllowedExportFilters.Locale);
    }

    [Fact]
    public void ToAllowedFilterNames_WhenNativeCodebook_ReturnsEmpty()
    {
        AllowedExportFilters.ToAllowedFilterNames(ExportRequestFilterSets.NativeCodebook)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void ToAllowedFilterNames_WhenShojiCodebook_ReturnsLocaleOnly()
    {
        AllowedExportFilters.ToAllowedFilterNames(ExportRequestFilterSets.ShojiCodebook)
            .Should()
            .Equal(AllowedExportFilters.Locale);
    }

    [Fact]
    public void ToAllowedFilterNames_WhenNone_ReturnsEmpty()
    {
        AllowedExportFilters.ToAllowedFilterNames(ExportRequestFilters.None)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void ToAllowedFilterNames_WhenSingleFlag_ReturnsMatchingName()
    {
        AllowedExportFilters.ToAllowedFilterNames(ExportRequestFilters.CompletionStatus)
            .Should()
            .Equal(AllowedExportFilters.CompletionStatus);
    }

    [Fact]
    public void FilterNameConstants_MatchPublicApiContract()
    {
        AllowedExportFilters.IncludeTestSubmissions.Should().Be("includeTestSubmissions");
        AllowedExportFilters.CreatedAtRange.Should().Be("createdAtRange");
        AllowedExportFilters.CompletedAtRange.Should().Be("completedAtRange");
        AllowedExportFilters.SubmissionIdRange.Should().Be("submissionIdRange");
        AllowedExportFilters.Locale.Should().Be("locale");
        AllowedExportFilters.ColumnScope.Should().Be("columnScope");
        AllowedExportFilters.CompletionStatus.Should().Be("completionStatus");
    }
}
