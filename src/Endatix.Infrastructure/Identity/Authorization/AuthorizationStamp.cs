namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// The single row whose value changes, as the last statement of the transaction, whenever an input to a user's
/// authorization data changes: a role, a role's permissions, a user's roles, or a user's tenant. Cached authorization
/// data is keyed on this value, so every API instance stops serving the old data on its next request.
/// </summary>
public sealed class AuthorizationStamp
{
    /// <summary>The id of the one row.</summary>
    public const int RowId = 1;

    /// <summary>The value before the first change, and the value read when the row is missing.</summary>
    public static readonly Guid Initial = Guid.Empty;

    private AuthorizationStamp()
    {
    }

    public int Id { get; private set; }

    public Guid Value { get; private set; }
}
