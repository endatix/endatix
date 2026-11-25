using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormTemplates.GetById;

/// <summary>Reads a visible form template.</summary>
public class GetFormTemplateByIdHandler(IRepository<FormTemplate> repository, ManagedContentScope? scope)
    : IQueryHandler<GetFormTemplateByIdQuery, Result<FormTemplate>>
{
    /// <summary>Constructs the ordinary operation without account standing enforcement.</summary>
    public GetFormTemplateByIdHandler(IRepository<FormTemplate> repository)
        : this(repository, null) { }

    public async Task<Result<FormTemplate>> Handle(GetFormTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        if (scope is not null && await scope.TargetRefusalAsync<FormTemplate, FormTemplate>(repository, new(request.FormTemplateId, "Form template not found."), cancellationToken) is { } refusal)
        {
            return refusal;
        }

        var formTemplate = await repository.FindAsync(request.FormTemplateId, scope, cancellationToken);
        if (formTemplate == null)
        {
            return Result.NotFound("Form template not found.");
        }
        return Result.Success(formTemplate);
    }
}
