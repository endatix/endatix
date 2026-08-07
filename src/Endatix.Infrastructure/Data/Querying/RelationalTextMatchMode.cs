namespace Endatix.Infrastructure.Data.Querying;

/// <summary>
/// Text match modes for provider LIKE / ILIKE filters.
/// </summary>
public enum RelationalTextMatchMode
{
    Contains = 0,
    StartsWith = 1,
    Exact = 2
}
