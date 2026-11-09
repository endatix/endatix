namespace Endatix.Core.Entities;

/// <summary>The answers, definition, page and metadata one save writes to a <see cref="Submission"/>.</summary>
/// <param name="JsonData">The answers.</param>
/// <param name="FormDefinitionId">The definition the answers were given against.</param>
/// <param name="CurrentPage">The page the respondent is on.</param>
/// <param name="Metadata">Optional metadata JSON.</param>
public readonly record struct SubmissionContent(string JsonData, long FormDefinitionId, int CurrentPage = 1, string? Metadata = null);
