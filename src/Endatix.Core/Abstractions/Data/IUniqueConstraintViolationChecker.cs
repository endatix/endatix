namespace Endatix.Core.Abstractions.Data;

/// <summary>
/// Interprets persistence exceptions (e.g. from EF Core save) for unique constraint violations and related metadata.
/// </summary>
public interface IUniqueConstraintViolationChecker
{
    /// <summary>
    /// Inspects <paramref name="exception"/> and returns whether it represents a unique constraint violation,
    /// plus provider-reported constraint and column names when available.
    /// </summary>
    UniqueConstraintViolationResult AnalyzeUniqueConstraint(Exception exception);
}
