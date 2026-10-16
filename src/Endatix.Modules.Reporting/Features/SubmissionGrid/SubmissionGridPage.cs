using Endatix.Core.Infrastructure.Result;

namespace Endatix.Modules.Reporting.Features.SubmissionGrid;

/// <summary>
/// One page of the grid's submissions list: the standard paged shape, plus what answer filters could not check.
/// </summary>
public sealed class SubmissionGridPage
{
    public SubmissionGridPage(Paged<SubmissionGridRow> page, AnswerFilterCoverage? answerFilterCoverage)
    {
        Page = page.Page;
        PageSize = page.PageSize;
        TotalRecords = page.TotalRecords;
        TotalPages = page.TotalPages;
        Items = page.Items;
        AnswerFilterCoverage = answerFilterCoverage;
    }

    public int Page { get; }

    public int PageSize { get; }

    public int TotalRecords { get; }

    public int TotalPages { get; }

    public IReadOnlyList<SubmissionGridRow> Items { get; }

    /// <summary>Set only when the request has answer filters.</summary>
    public AnswerFilterCoverage? AnswerFilterCoverage { get; }
}

/// <summary>
/// The submissions inside the metadata filters whose answers could not be checked, because they are not prepared
/// from their current version. They are left out of a filtered page, and counted here instead.
/// </summary>
/// <param name="UncheckedCompleted">Completed submissions; preparing the form's reporting data checks them.</param>
/// <param name="UncheckedInProgress">In-progress submissions; only including them in preparation checks them.</param>
public sealed record AnswerFilterCoverage(int UncheckedCompleted, int UncheckedInProgress);
