namespace Endatix.Modules.Reporting.Features.SearchCopy;

/// <summary>A column an answer filter can read, with the filter it supports.</summary>
internal sealed record SearchableColumn(string Key, string FilterType);
