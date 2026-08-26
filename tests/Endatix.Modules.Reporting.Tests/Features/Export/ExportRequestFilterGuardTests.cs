using Endatix.Core.Infrastructure.Paging;
using Endatix.Modules.Reporting.Contracts.Export;
using FluentAssertions;

namespace Endatix.Modules.Reporting.Tests.Features.Export;

/// <summary>
/// Documents which request filters are present vs allowed for each capability set.
/// Presence rules: nullable fields count when HasValue / non-empty; whitespace locale is ignored.
/// </summary>
public sealed class ExportRequestFilterGuardTests
{
    private static readonly UtcDateTimeRange BoundRange = new(
        new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc));

    private static readonly UtcDateTimeRange FromOnly = new(
        new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        null);

    private static readonly UtcDateTimeRange ToOnly = new(
        null,
        new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc));

    private static ExportFilterContext EmptyFilters() =>
        new(IncludeTestSubmissions: null);

    [Fact]
    public void GetDisallowedWireNames_WhenNoFiltersPresent_ReturnsEmpty_ForAnyCapability()
    {
        foreach (ExportRequestFilters allowed in new[]
                 {
                     ExportRequestFilters.None,
                     ExportRequestFilterSets.Submissions,
                     ExportRequestFilterSets.NativeCodebook,
                     ExportRequestFilterSets.ShojiCodebook,
                 })
        {
            ExportRequestFilterGuard.GetDisallowedWireNames(allowed, EmptyFilters())
                .Should()
                .BeEmpty($"capability {allowed} must accept an empty filter context");
        }
    }

    [Fact]
    public void GetDisallowedWireNames_WhenCodebookReceivesRowFilters_ReturnsThoseFilters()
    {
        var disallowed = ExportRequestFilterGuard.GetDisallowedWireNames(
            ExportRequestFilterSets.ShojiCodebook,
            new ExportFilterContext(
                IncludeTestSubmissions: true,
                Created: FromOnly,
                MinSubmissionId: 10,
                Locale: "es"));

        disallowed.Should().BeEquivalentTo(
        [
            AllowedExportFilters.IncludeTestSubmissions,
            AllowedExportFilters.CreatedAtRange,
            AllowedExportFilters.SubmissionIdRange,
        ]);
    }

    [Fact]
    public void GetDisallowedWireNames_WhenCodebookReceivesCompletionStatus_ReturnsCompletionStatus()
    {
        var disallowed = ExportRequestFilterGuard.GetDisallowedWireNames(
            ExportRequestFilterSets.ShojiCodebook,
            EmptyFilters() with { CompletionStatus = ExportCompletionStatus.Completed });

        disallowed.Should().Equal(AllowedExportFilters.CompletionStatus);
    }

    [Fact]
    public void GetDisallowedWireNames_WhenSubmissionsReceivesLocale_ReturnsLocale()
    {
        var disallowed = ExportRequestFilterGuard.GetDisallowedWireNames(
            ExportRequestFilterSets.Submissions,
            EmptyFilters() with { Locale = "es" });

        disallowed.Should().Equal(AllowedExportFilters.Locale);
    }

    [Fact]
    public void GetDisallowedWireNames_WhenSubmissionsReceivesAllowedFilters_ReturnsEmpty()
    {
        var disallowed = ExportRequestFilterGuard.GetDisallowedWireNames(
            ExportRequestFilterSets.Submissions,
            new ExportFilterContext(
                IncludeTestSubmissions: false,
                Created: BoundRange,
                Modified: BoundRange,
                Started: BoundRange,
                Completed: BoundRange,
                MinSubmissionId: 1,
                MaxSubmissionId: 100,
                ColumnScope: ["q1"],
                CompletionStatus: ExportCompletionStatus.Completed));

        disallowed.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetDisallowedWireNames_WhenLocaleBlank_DoesNotCountAsPresent(string? locale)
    {
        var disallowed = ExportRequestFilterGuard.GetDisallowedWireNames(
            ExportRequestFilterSets.Submissions,
            EmptyFilters() with { Locale = locale });

        disallowed.Should().BeEmpty();
    }

    [Fact]
    public void GetDisallowedWireNames_WhenIncludeTestSubmissionsFalse_CountsAsPresent()
    {
        // Explicit false is still a filter override (vs omitted/null = format default).
        var disallowed = ExportRequestFilterGuard.GetDisallowedWireNames(
            ExportRequestFilterSets.NativeCodebook,
            EmptyFilters() with { IncludeTestSubmissions = false });

        disallowed.Should().Equal(AllowedExportFilters.IncludeTestSubmissions);
    }

    [Fact]
    public void GetDisallowedWireNames_WhenOnlyCreatedToPresent_CountsAsCreatedAtRange()
    {
        var disallowed = ExportRequestFilterGuard.GetDisallowedWireNames(
            ExportRequestFilterSets.ShojiCodebook,
            EmptyFilters() with { Created = ToOnly });

        disallowed.Should().Equal(AllowedExportFilters.CreatedAtRange);
    }

    [Fact]
    public void GetDisallowedWireNames_WhenOnlyModifiedFromPresent_CountsAsModifiedAtRange()
    {
        var disallowed = ExportRequestFilterGuard.GetDisallowedWireNames(
            ExportRequestFilterSets.ShojiCodebook,
            EmptyFilters() with { Modified = FromOnly });

        disallowed.Should().Equal(AllowedExportFilters.ModifiedAtRange);
    }

    [Fact]
    public void GetDisallowedWireNames_WhenOnlyCompletedFromPresent_CountsAsCompletedAtRange()
    {
        var disallowed = ExportRequestFilterGuard.GetDisallowedWireNames(
            ExportRequestFilterSets.NativeCodebook,
            EmptyFilters() with { Completed = FromOnly });

        disallowed.Should().Equal(AllowedExportFilters.CompletedAtRange);
    }

    [Fact]
    public void GetDisallowedWireNames_WhenOnlyMaxSubmissionIdPresent_CountsAsSubmissionIdRange()
    {
        var disallowed = ExportRequestFilterGuard.GetDisallowedWireNames(
            ExportRequestFilterSets.ShojiCodebook,
            EmptyFilters() with { MaxSubmissionId = 50 });

        disallowed.Should().Equal(AllowedExportFilters.SubmissionIdRange);
    }

    [Fact]
    public void GetDisallowedWireNames_WhenColumnScopeNull_DoesNotCountAsPresent()
    {
        ExportRequestFilterGuard.GetDisallowedWireNames(
                ExportRequestFilterSets.NativeCodebook,
                EmptyFilters() with { ColumnScope = null })
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void GetDisallowedWireNames_WhenColumnScopeEmpty_DoesNotCountAsPresent()
    {
        ExportRequestFilterGuard.GetDisallowedWireNames(
                ExportRequestFilterSets.NativeCodebook,
                EmptyFilters() with { ColumnScope = [] })
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void GetDisallowedWireNames_WhenColumnScopeNonEmpty_CountsAsPresent()
    {
        var disallowed = ExportRequestFilterGuard.GetDisallowedWireNames(
            ExportRequestFilterSets.NativeCodebook,
            EmptyFilters() with { ColumnScope = ["q1"] });

        disallowed.Should().Equal(AllowedExportFilters.ColumnScope);
    }

    [Fact]
    public void GetDisallowedWireNames_WhenCompletionStatusAll_CountsAsPresent()
    {
        // Explicit All is still a request filter (vs omitted).
        var disallowed = ExportRequestFilterGuard.GetDisallowedWireNames(
            ExportRequestFilterSets.ShojiCodebook,
            EmptyFilters() with { CompletionStatus = ExportCompletionStatus.All });

        disallowed.Should().Equal(AllowedExportFilters.CompletionStatus);
    }

    [Fact]
    public void GetDisallowedWireNames_ReturnsStableOrderMatchingWireCatalog()
    {
        var disallowed = ExportRequestFilterGuard.GetDisallowedWireNames(
            ExportRequestFilters.None,
            new ExportFilterContext(
                IncludeTestSubmissions: true,
                Created: FromOnly,
                Modified: FromOnly,
                Started: FromOnly,
                Completed: FromOnly,
                MinSubmissionId: 1,
                Locale: "en",
                ColumnScope: ["a"],
                CompletionStatus: ExportCompletionStatus.Completed));

        disallowed.Should().Equal(
            AllowedExportFilters.IncludeTestSubmissions,
            AllowedExportFilters.CreatedAtRange,
            AllowedExportFilters.ModifiedAtRange,
            AllowedExportFilters.StartedAtRange,
            AllowedExportFilters.CompletedAtRange,
            AllowedExportFilters.SubmissionIdRange,
            AllowedExportFilters.Locale,
            AllowedExportFilters.ColumnScope,
            AllowedExportFilters.CompletionStatus);
    }
}
