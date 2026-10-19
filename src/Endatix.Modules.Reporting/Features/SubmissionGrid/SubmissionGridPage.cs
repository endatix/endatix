using System.Text.Json.Serialization;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Modules.Reporting.Features.SubmissionGrid;

/// <summary>
/// One page of the grid's submissions list: the standard paged shape, plus what answer filters could not check.
/// </summary>
public sealed class SubmissionGridPage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubmissionGridPage"/> class.
    /// </summary>
    /// <param name="page">The page of rows.</param>
    /// <param name="answerFilterCoverage">What the answer filters could not check; <c>null</c> without them.</param>
    public SubmissionGridPage(Paged<SubmissionGridRow> page, AnswerFilterCoverage? answerFilterCoverage)
    {
        Page = page.Page;
        PageSize = page.PageSize;
        TotalRecords = page.TotalRecords;
        TotalPages = page.TotalPages;
        Items = page.Items;
        AnswerFilterCoverage = answerFilterCoverage;
    }

    /// <summary>The 1-based page number.</summary>
    public int Page { get; }

    /// <summary>The most rows a page holds.</summary>
    public int PageSize { get; }

    /// <summary>The submissions matching the filters, on every page.</summary>
    public int TotalRecords { get; }

    /// <summary>The number of pages.</summary>
    public int TotalPages { get; }

    /// <summary>The rows on this page.</summary>
    public IReadOnlyList<SubmissionGridRow> Items { get; }

    /// <summary>Set only when the request has answer filters; left out of the JSON otherwise.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AnswerFilterCoverage? AnswerFilterCoverage { get; }
}

/// <summary>
/// The submissions inside the metadata filters whose answers could not be checked, because they are not prepared
/// from their current version. They are left out of a filtered page, and counted here instead.
/// </summary>
/// <param name="UncheckedCompleted">Completed submissions; preparing the form's reporting data checks them.</param>
/// <param name="UncheckedInProgress">In-progress submissions; only including them in preparation checks them.</param>
public sealed record AnswerFilterCoverage(int UncheckedCompleted, int UncheckedInProgress);
