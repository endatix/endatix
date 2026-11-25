using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Forms.Delete;

internal sealed class DeletableForms(IFormsRepository forms, ContentQueryScope scope)
{
    internal Task<bool> ExistsAsync(long id, CancellationToken ct)
    {
        var spec = new FormSpecifications.ByIdReadOnly(id);
        scope.Apply(spec);
        return forms.AnyAsync(spec, ct);
    }

    internal async Task<Form?> DeleteAsync(long id, CancellationToken ct)
    {
        var spec = new FormWithDefinitionsSpec(id);
        scope.Apply(spec);
        var form = await forms.SingleOrDefaultAsync(spec, ct);
        if (form is not null)
        {
            await forms.DeleteSubmissionsAsync(form, ct);
            form.Delete();
            await forms.SaveChangesAsync(ct);
        }
        return form;
    }
}
