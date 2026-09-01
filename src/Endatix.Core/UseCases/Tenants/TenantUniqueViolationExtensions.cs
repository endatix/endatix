using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;

namespace Endatix.Core.UseCases.Tenants;

/// <summary>
/// Extensions for the <see cref="UniqueConstraintViolationResult"/> class.
/// </summary>
public static class TenantUniqueViolationExtensions
{
    /// <summary>
    /// Checks if the violation is a tenant short URL violation.
    /// </summary>
    /// <param name="violation">The violation to check.</param>
    /// <returns>True if the violation is a tenant short URL violation, false otherwise.</returns>
    public static bool IsTenantShortUrlViolation(this UniqueConstraintViolationResult violation) =>
        string.Equals(violation.ConstraintName, Tenant.UniqueConstraints.ShortUrl, StringComparison.OrdinalIgnoreCase)
        || string.Equals(violation.ColumnName, nameof(Tenant.ShortUrl), StringComparison.OrdinalIgnoreCase);
}
