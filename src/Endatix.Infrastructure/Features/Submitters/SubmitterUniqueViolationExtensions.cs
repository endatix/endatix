using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;

namespace Endatix.Infrastructure.Features.Submitters;

internal static class SubmitterUniqueViolationExtensions
{
    private const string APP_USER_IDENTITY_PER_TENANT = "IX_Submitters_TenantId_AuthProvider_AppUserId";
    private const string EXTERNAL_SUBJECT_IDENTITY_PER_TENANT = "IX_Submitters_TenantId_AuthProvider_ExternalSubjectId";

    /// <summary>
    /// Checks if the violation is a submitter identity violation.
    /// </summary>
    /// <param name="violation">The violation result.</param>
    /// <returns>True if the violation is a submitter identity violation, false otherwise.</returns>
    public static bool IsSubmitterIdentityViolation(this UniqueConstraintViolationResult violation) =>
        string.Equals(violation.ConstraintName, Submitter.UniqueConstraints.IdentityPerTenant, StringComparison.OrdinalIgnoreCase)
        || string.Equals(violation.ConstraintName, APP_USER_IDENTITY_PER_TENANT, StringComparison.OrdinalIgnoreCase)
        || string.Equals(violation.ConstraintName, EXTERNAL_SUBJECT_IDENTITY_PER_TENANT, StringComparison.OrdinalIgnoreCase)
        || string.Equals(violation.ColumnName, nameof(Submitter.AppUserId), StringComparison.OrdinalIgnoreCase)
        || string.Equals(violation.ColumnName, nameof(Submitter.ExternalSubjectId), StringComparison.OrdinalIgnoreCase);
}
