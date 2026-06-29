using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;

namespace Endatix.Infrastructure.Features.Submitters;

internal static class SubmitterUniqueViolationExtensions
{
    public static bool IsSubmitterIdentityViolation(this UniqueConstraintViolationResult violation) =>
        IsAppUserViolation(violation) || IsExternalSubjectViolation(violation);

    private static bool IsAppUserViolation(UniqueConstraintViolationResult violation) =>
        string.Equals(violation.ConstraintName, Submitter.UniqueConstraints.AppUserPerTenant, StringComparison.OrdinalIgnoreCase)
        || string.Equals(violation.ColumnName, nameof(Submitter.AppUserId), StringComparison.OrdinalIgnoreCase);

    private static bool IsExternalSubjectViolation(UniqueConstraintViolationResult violation) =>
        string.Equals(violation.ConstraintName, Submitter.UniqueConstraints.ExternalSubjectPerTenant, StringComparison.OrdinalIgnoreCase)
        || string.Equals(violation.ColumnName, nameof(Submitter.ExternalSubjectId), StringComparison.OrdinalIgnoreCase);
}
