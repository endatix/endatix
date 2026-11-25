using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.FormDefinitions.GetFields;

internal sealed class FormFieldDefinitions(IFormsRepository forms, IRepository<FormDefinition> definitions, ContentQueryScope scope)
{
    internal async Task<IReadOnlyList<FormDefinition>?> ReadAsync(long id, CancellationToken ct)
    {
        var parent = new FormSpecifications.ByIdReadOnly(id);
        scope.Apply(parent);
        if (await forms.SingleOrDefaultAsync(parent, ct) is null)
        {
            return null;
        }
        var children = new FormDefinitionsByFormIdSpec(id);
        scope.Apply(children);
        return await definitions.ListAsync(children, ct);
    }
}
