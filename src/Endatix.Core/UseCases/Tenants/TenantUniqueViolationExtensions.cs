using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;

namespace Endatix.Core.UseCases.Tenants;

public static class TenantUniqueViolationExtensions
{
    public static bool IsTenantShortUrlViolation(this UniqueConstraintViolationResult violation) =>
        string.Equals(violation.ConstraintName, Tenant.UniqueConstraints.ShortUrl, StringComparison.OrdinalIgnoreCase)
        || string.Equals(violation.ColumnName, nameof(Tenant.ShortUrl), StringComparison.OrdinalIgnoreCase);
}
