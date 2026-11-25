using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormTemplates.Delete;

/// <summary>Deletes a visible form template.</summary>
public class DeleteFormTemplateHandler(IRepository<FormTemplate> repository, ITenantContext scope)
    : ICommandHandler<DeleteFormTemplateCommand, Result<FormTemplate>>
{
    public async Task<Result<FormTemplate>> Handle(DeleteFormTemplateCommand request, CancellationToken cancellationToken)
    {
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
