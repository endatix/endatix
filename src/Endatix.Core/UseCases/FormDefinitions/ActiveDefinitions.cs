using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.FormDefinitions;

internal sealed class ActiveDefinitions(IFormsRepository forms, ContentQueryScope scope)
{
    internal async Task<Form?> FindFormAsync(long id, CancellationToken ct)
    {
        var spec = new ActiveFormDefinitionByFormIdSpec(id);
        scope.Apply(spec);
        var form = await forms.SingleOrDefaultAsync(spec, ct);
        return scope.TenantId is long tenant && form?.ActiveDefinition?.TenantId != tenant ? null : form;
    }

    internal Task UpdateAsync(Form form, CancellationToken ct) => forms.UpdateAsync(form, ct);
}
