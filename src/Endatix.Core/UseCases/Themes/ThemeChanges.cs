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
    internal async Task<Result<Edit>> PrepareEditAsync(EditRequest change, CancellationToken ct)
    {
        var target = await scope.FindAsync(themes, change.Id, ct);
        if (target is null)
        {
            return Result.NotFound("Theme not found");
        }
        if (change.RequiresName)
        {
            RequireName(change.Name);
        }
        var data = await ValidateAsync(new Content(change.Name, change.Json), change.Id, ct);
        if (!data.IsSuccess)
        {
            return data.ToErrorResult<Edit>();
        }
        return new Edit(target, data.Value);
    }

    private static void RequireName(string? name) => Guard.Against.NullOrEmpty(name);

    internal Task<Result<ThemeJsonData?>> PrepareCreateAsync(Content content, CancellationToken ct) =>
        ValidateAsync(content, null, ct);

    private async Task<Result<ThemeJsonData?>> ValidateAsync(Content change, long? id, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(change.Name) && await NameConflictAsync(change.Name, id, ct) is { } conflict)
        {
            return conflict.ToErrorResult<ThemeJsonData?>();
        }
        var data = change.Json is null ? null : ThemeJsonData.Create(change.Json);
        if (data is { IsSuccess: false })
        {
            return data.ToErrorResult<ThemeJsonData?>();
        }
        return Result<ThemeJsonData?>.Success(data?.Value);
    }

    internal sealed record Content(string? Name, string? Json);
    internal sealed record EditRequest(long Id, string? Name, string? Json)
    {
        internal bool RequiresName { get; init; }
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

    private Result<Theme> Duplicate(string name, long? id) => id.HasValue && scope.TenantId is null
        ? Result<Theme>.Error($"Another theme with the name '{name}' already exists")
        : Result.Invalid(new ValidationError($"A theme with the name '{name}' already exists"));

    private sealed class ThemeId : Specification<Theme, long?>
    {
        internal ThemeId() => Query.Select(theme => (long?)theme.Id);
    }
}
