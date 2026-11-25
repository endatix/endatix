using Ardalis.GuardClauses;
using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Models.Themes;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Themes;

internal sealed class ThemeChanges(IRepository<Theme> themes, ContentQueryScope scope)
{
    internal async Task<Result<Edit>> PrepareEditAsync(ThemeChange change, CancellationToken ct)
    {
        var id = change.Id!.Value;
        var target = await scope.FindAsync(themes, id, ct);
        if (target is null)
        {
            return Result.NotFound("Theme not found");
        }
        var data = await DataAsync(change, ct);
        if (!data.IsSuccess)
        {
            return data.ToErrorResult<Edit>();
        }
        return new Edit(target, data.Value);
    }

    internal async Task<Result<ThemeJsonData?>> DataAsync(ThemeChange change, CancellationToken ct)
    {
        if (change.CheckName && await NameConflictAsync(change.Name!, change.Id, ct) is { } conflict)
        {
            return conflict.ToErrorResult<ThemeJsonData?>();
        }
        if (change.RequiresName)
        {
            var name = change.Name;
            Guard.Against.NullOrEmpty(name);
        }
        var data = change.Json is null ? null : ThemeJsonData.Create(change.Json);
        if (data is { IsSuccess: false })
        {
            return data.ToErrorResult<ThemeJsonData?>();
        }
        return Result<ThemeJsonData?>.Success(data?.Value);
    }

    internal sealed record ThemeChange(long? Id, string? Name, string? Json)
    {
        internal bool RequiresName { get; init; }
        internal bool CheckName => RequiresName || !string.IsNullOrEmpty(Name);
    }
    internal sealed record Edit(Theme Target, ThemeJsonData? Data);

    private async Task<Result<Theme>?> NameConflictAsync(string name, long? id, CancellationToken ct)
    {
        var named = new ThemeSpecifications.ByName(name);
        scope.Apply(named);
        // Match the ordinary first-by-name check, including packs with repeated names.
        var first = scope.TenantId.HasValue ? await themes.FirstOrDefaultAsync(named.WithProjectionOf(new ThemeId()), ct)
            : (await themes.FirstOrDefaultAsync(named, ct))?.Id;
        return first is not null && first != id ? Duplicate(name, id) : null;
    }

    private static Result<Theme> Duplicate(string name, long? id) => id.HasValue
        ? Result<Theme>.Error($"Another theme with the name '{name}' already exists")
        : Result.Invalid(new ValidationError($"A theme with the name '{name}' already exists"));

    private sealed class ThemeId : Specification<Theme, long?>
    {
        internal ThemeId() => Query.Select(theme => (long?)theme.Id);
    }
}
