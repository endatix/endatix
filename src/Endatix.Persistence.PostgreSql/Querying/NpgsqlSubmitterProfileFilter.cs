using System.Linq.Expressions;
using System.Text.Json;
using Endatix.Infrastructure.Data.Querying;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Persistence.PostgreSql.Querying;

/// <summary>
/// PostgreSQL submitter profile filter: <c>@&gt;</c> on the <c>jsonb</c> snapshot, which the GIN index serves.
/// </summary>
public sealed class NpgsqlSubmitterProfileFilter : RelationalSubmitterProfileFilterBase
{
    /// <inheritdoc />
    protected override Expression<Func<string?, bool>> HasAnyOf(string field, List<string> values)
    {
        var payloads = ContainsPayloads(field, values);
        return snapshot => snapshot != null &&
                           payloads.Any(payload => EF.Functions.JsonContains(snapshot, payload));
    }

    /// <inheritdoc />
    protected override Expression<Func<string?, bool>> HasNoneOf(string field, List<string> values)
    {
        var payloads = ContainsPayloads(field, values);
        return snapshot => snapshot == null ||
                           payloads.All(payload => !EF.Functions.JsonContains(snapshot, payload));
    }

    private static List<string> ContainsPayloads(string field, List<string> values) =>
        values
            .Select(value => JsonSerializer.Serialize(new Dictionary<string, string> { [field] = value }))
            .ToList();
}
