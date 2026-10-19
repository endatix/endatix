namespace Endatix.Modules.Reporting.Features.SearchCopy;

/// <summary>A column an answer filter can read, with the filter it supports.</summary>
/// <param name="Key">The column key.</param>
/// <param name="FilterType">One of <c>SubmissionGridFilterTypes</c>.</param>
/// <param name="Automatic">
/// Whether a compile may make it searchable on its own: its question is not read-only. A person may choose either.
/// </param>
internal sealed record SearchableColumn(string Key, string FilterType, bool Automatic = true);
