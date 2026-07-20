using Endatix.Modules.Reporting.Contracts.Export;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

public sealed class ExportRequestFilterGuardTests
{
    [Fact]
    public void GetDisallowedWireNames_WhenCodebookReceivesRowFilters_ReturnsThoseFilters()
    {
        var disallowed = ExportRequestFilterGuard.GetDisallowedWireNames(
            ExportRequestFilterSets.ShojiCodebook,
            new ExportFilterContext(
                IncludeTestSubmissions: true,
                CreatedAfter: DateTime.UtcNow.AddDays(-1),
                CreatedBefore: null,
                CompletedAfter: null,
                CompletedBefore: null,
                MinSubmissionId: 10,
                MaxSubmissionId: null,
                Locale: "es",
                ColumnScope: null));

        disallowed.Should().BeEquivalentTo(
        [
            ExportRequestFilterWireNames.IncludeTestSubmissions,
            ExportRequestFilterWireNames.CreatedAtRange,
            ExportRequestFilterWireNames.SubmissionIdRange,
        ]);
    }

    [Fact]
    public void GetDisallowedWireNames_WhenNativeCodebookReceivesLocale_ReturnsLocale()
    {
        var disallowed = ExportRequestFilterGuard.GetDisallowedWireNames(
            ExportRequestFilterSets.NativeCodebook,
            new ExportFilterContext(
                IncludeTestSubmissions: null,
                CreatedAfter: null,
                CreatedBefore: null,
                CompletedAfter: null,
                CompletedBefore: null,
                MinSubmissionId: null,
                MaxSubmissionId: null,
                Locale: "es",
                ColumnScope: null));

        disallowed.Should().Equal(ExportRequestFilterWireNames.Locale);
    }

    [Fact]
    public void GetDisallowedWireNames_WhenSubmissionsReceivesAllFilters_ReturnsEmpty()
    {
        var disallowed = ExportRequestFilterGuard.GetDisallowedWireNames(
            ExportRequestFilterSets.Submissions,
            new ExportFilterContext(
                IncludeTestSubmissions: false,
                CreatedAfter: DateTime.UtcNow.AddDays(-7),
                CreatedBefore: DateTime.UtcNow,
                CompletedAfter: DateTime.UtcNow.AddDays(-7),
                CompletedBefore: DateTime.UtcNow,
                MinSubmissionId: 1,
                MaxSubmissionId: 100,
                Locale: "es",
                ColumnScope: ["q1"]));

        disallowed.Should().BeEmpty();
    }
}
