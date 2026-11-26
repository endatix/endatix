using Ardalis.Specification;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Submissions.ListByFormId;

internal interface ISubmissionListParent
{
    Task<bool> HasDefinitionsAsync(long formId, CancellationToken ct);
}

internal sealed class RepositoryDefinitionParent(IRepository<FormDefinition> definitions) : ISubmissionListParent
{
    public Task<bool> HasDefinitionsAsync(long formId, CancellationToken ct) =>
        definitions.AnyAsync(new FormDefinitionsByFormIdSpec(formId), ct);
}

internal sealed class TenantFormParent(IRepository<Form> forms, ITenantContext tenant) : ISubmissionListParent
{
    public Task<bool> HasDefinitionsAsync(long formId, CancellationToken ct)
    {
        var id = tenant.TenantId;
        if (id <= 0)
        {
            return Task.FromResult(false);
        }
        var parent = new FormSpecifications.ByIdForTenant(formId, id);
        parent.Query.Where(form => !form.IsDeleted && form.FormDefinitions.Any(definition => definition.TenantId == id && !definition.IsDeleted));
        return forms.AnyAsync(parent, ct);
    }
}
