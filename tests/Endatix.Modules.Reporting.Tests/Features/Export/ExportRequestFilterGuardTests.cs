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
            includeTestSubmissions: true,
            createdAfter: DateTime.UtcNow.AddDays(-1),
            createdBefore: null,
            completedAfter: null,
            completedBefore: null,
            minSubmissionId: 10,
            maxSubmissionId: null,
            locale: "es",
            columnScope: null);

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
            includeTestSubmissions: null,
            createdAfter: null,
            createdBefore: null,
            completedAfter: null,
            completedBefore: null,
            minSubmissionId: null,
            maxSubmissionId: null,
            locale: "es",
            columnScope: null);

        disallowed.Should().Equal(ExportRequestFilterWireNames.Locale);
    }

    [Fact]
    public void GetDisallowedWireNames_WhenSubmissionsReceivesAllFilters_ReturnsEmpty()
    {
        var disallowed = ExportRequestFilterGuard.GetDisallowedWireNames(
            ExportRequestFilterSets.Submissions,
            includeTestSubmissions: false,
            createdAfter: DateTime.UtcNow.AddDays(-7),
            createdBefore: DateTime.UtcNow,
            completedAfter: DateTime.UtcNow.AddDays(-7),
            completedBefore: DateTime.UtcNow,
            minSubmissionId: 1,
            maxSubmissionId: 100,
            locale: "es",
            columnScope: ["q1"]);

        disallowed.Should().BeEmpty();
    }
}
