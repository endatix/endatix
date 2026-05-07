namespace Endatix.Core.Abstractions.Data;

/// <summary>
/// Result of inspecting an exception for a database unique constraint violation.
/// </summary>
/// <param name="IsUniqueConstraintViolation">True when the exception chain indicates a unique violation.</param>
/// <param name="ConstraintName">Constraint or unique index name when the provider exposes it.</param>
/// <param name="ColumnName">Physical column name when the provider exposes it (e.g. PostgreSQL).</param>
public sealed record UniqueConstraintViolationResult(
    bool IsUniqueConstraintViolation,
    string? ConstraintName,
    string? ColumnName);
