namespace Endatix.Modules.Reporting.Features.SubmissionGrid;

/// <summary>
/// The submissions-grid columns of a form, and how much of the form's data is prepared for them.
/// </summary>
/// <param name="FormId">The form.</param>
/// <param name="SchemaReady">Whether the form has a compiled schema. Without one, <paramref name="Columns"/> is empty.</param>
/// <param name="Readiness">How many of the form's submissions are prepared.</param>
/// <param name="Columns">The columns in schema order.</param>
/// <param name="SearchSlots">
/// How many columns answer filters can read, and how many more people may add; all zero but the caps without a
/// compiled schema.
/// </param>
public sealed record SubmissionGridColumns(
    long FormId,
    bool SchemaReady,
    SubmissionGridReadiness Readiness,
    IReadOnlyList<SubmissionGridColumn> Columns,
    SubmissionGridSearchSlots SearchSlots);

/// <summary>
/// One submissions-grid column.
/// </summary>
/// <param name="Key">The key of the column in prepared submission data.</param>
/// <param name="Kind">The schema column kind, for example <c>Simple</c> or <c>ChoiceIndicator</c>.</param>
/// <param name="Title">The header in the requested locale, falling back to the default locale.</param>
/// <param name="QuestionType">The SurveyJS type of the question the column comes from, when it comes from one.</param>
/// <param name="FilterType">
/// One of <see cref="SubmissionGridFilterTypes"/>, or <see langword="null"/> when the column cannot be filtered.
/// </param>
/// <param name="PageName">The page the question is on.</param>
/// <param name="PageTitle">The page title in the requested locale, falling back to the default locale.</param>
/// <param name="PanelTitle">The nearest titled panel around the question, in the default locale.</param>
/// <param name="Searchable">Whether answer filters can read the column: it is one of the form's search keys.</param>
/// <param name="SearchPending">
/// Whether some prepared rows have not been rebuilt with the column's search key yet; filters on it count them as not
/// checked.
/// </param>
/// <param name="SearchSource">
/// <c>auto</c> when the schema made the column searchable, <c>user</c> when a person did (and may undo it), or
/// <see langword="null"/> when it is not searchable.
/// </param>
public sealed record SubmissionGridColumn(
    string Key,
    string Kind,
    string Title,
    string? QuestionType,
    string? FilterType,
    string? PageName,
    string? PageTitle,
    string? PanelTitle,
    bool Searchable,
    bool SearchPending,
    string? SearchSource);

/// <summary>
/// How many of a form's submissions are prepared. Deleted submissions are not counted.
/// </summary>
/// <param name="Submissions">Every submission of the form, test submissions included.</param>
/// <param name="CompletedSubmissions">The completed ones among them.</param>
/// <param name="InProgress">The ones not completed, whether or not they have a prepared row.</param>
/// <param name="Prepared">Submissions with prepared data.</param>
/// <param name="Pending">Submissions not prepared yet, queued or being prepared.</param>
/// <param name="Failed">Submissions whose preparation failed.</param>
/// <param name="SkippedInProgress">In-progress submissions that were not prepared because they were not complete.</param>
public sealed record SubmissionGridReadiness(
    int Submissions,
    int CompletedSubmissions,
    int InProgress,
    int Prepared,
    int Pending,
    int Failed,
    int SkippedInProgress);
