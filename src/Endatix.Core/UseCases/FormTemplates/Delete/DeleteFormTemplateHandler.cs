using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormTemplates.Delete;

/// <summary>Deletes a visible form template.</summary>
public class DeleteFormTemplateHandler(IRepository<FormTemplate> repository, ManagedContentScope? scope)
    : ICommandHandler<DeleteFormTemplateCommand, Result<FormTemplate>>
{
    /// <summary>Constructs the ordinary operation without account standing enforcement.</summary>
    public DeleteFormTemplateHandler(IRepository<FormTemplate> repository)
        : this(repository, null) { }

    public async Task<Result<FormTemplate>> Handle(DeleteFormTemplateCommand request, CancellationToken cancellationToken)
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

        formTemplate.Delete();
        await repository.UpdateAsync(formTemplate, cancellationToken);
        return Result.Success(formTemplate);
    }
}
