using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Endatix.Modules.Personalization.Persistence;

/// <summary>
/// Detects a PostgreSQL unique-index violation (SQLSTATE 23505) for one constraint.
/// </summary>
internal static class UniqueIndexViolation
{
    public const string MembersIdentifier = "IX_Members_Identifier";
    public const string MembershipsMember = "IX_Memberships_Member";
    public const string PropertiesVariableName = "IX_Properties_VariableName";

    public static bool Is(DbUpdateException exception, string constraintName)
    {
        Exception? current = exception.InnerException;
        while (current is not null)
        {
            if (current is PostgresException postgres)
            {
                return postgres.SqlState == PostgresErrorCodes.UniqueViolation
                    && postgres.ConstraintName == constraintName;
            }

            current = current.InnerException;
        }

        return false;
    }
}
