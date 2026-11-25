using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Themes.Delete;

internal sealed class ThemeRemoval(IRepository<Theme> themes, IRepository<Form> forms, ContentQueryScope scope)
{
    internal async Task<long?> RemoveAsync(long id, CancellationToken ct)
    {
        var theme = await scope.FindAsync(themes, id, ct);
        if (theme is null)
        {
            return null;
        }
        var assigned = new FormSpecifications.ByThemeId(id);
        scope.Apply(assigned);
        foreach (var form in await forms.ListAsync(assigned, ct))
        {
            form.SetTheme(null);
        }
        await themes.DeleteAsync(theme, ct);
        return theme.Id;
    }
}
