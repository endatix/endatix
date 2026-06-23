namespace Endatix.Infrastructure.Data;

/// <summary>
/// Named global query filter keys used with EF Core 10 named query filters.
/// </summary>
public static class EndatixQueryFilterNames
{
    public const string SoftDelete = nameof(SoftDelete);

    public const string Tenant = nameof(Tenant);
}
