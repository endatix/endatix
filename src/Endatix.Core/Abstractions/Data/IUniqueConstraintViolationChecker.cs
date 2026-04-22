namespace Endatix.Core.Abstractions.Data;

/// <summary>
/// Checks if the exception is a unique constraint violation.
/// </summary>
public interface IUniqueConstraintViolationChecker
{
    /// <summary>
    /// Checks if the exception is a unique constraint violation.
    /// </summary>
    /// <param name="exception">The exception to check.</param>
    /// <returns>True if the exception is a unique constraint violation, false otherwise.</returns>
    bool IsUniqueConstraintViolation(Exception exception);
}
