using Endatix.Core.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Normalizes string values for stable comparisons (e.g. case-insensitive uniqueness).
/// Delegates to ASP.NET Core Identity <see cref="ILookupNormalizer"/> so domain normalization matches role/user name rules.
/// </summary>
public sealed class ValueNormalizer(ILookupNormalizer lookupNormalizer) : IValueNormalizer
{
    public string? Normalize(string? value) => lookupNormalizer.NormalizeName(value);
}
