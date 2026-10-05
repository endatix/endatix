using Endatix.Core.Abstractions.Data;

namespace Endatix.Modules.Audience.Persistence;

/// <summary>
/// Matches a save failure to one named unique index of this module.
/// </summary>
internal static class UniqueViolationExtensions
{
    public static bool IsViolationOf(
        this IUniqueConstraintViolationChecker checker,
        Exception exception,
        string constraintName)
    {
        UniqueConstraintViolationResult violation = checker.AnalyzeUniqueConstraint(exception);
        return violation.IsUniqueConstraintViolation
            && string.Equals(violation.ConstraintName, constraintName, StringComparison.OrdinalIgnoreCase);
    }
}
