using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormTemplates.Delete;

public class DeleteFormTemplateHandler(IRepository<FormTemplate> repository) 
    : ICommandHandler<DeleteFormTemplateCommand, Result<FormTemplate>>
{
    public async Task<Result<FormTemplate>> Handle(DeleteFormTemplateCommand request, CancellationToken cancellationToken)
    {
        var formTemplate = await repository.GetByIdAsync(request.FormTemplateId, cancellationToken);
        if (formTemplate == null)
        {
            return Result.NotFound("Form template not found.");
        }

        formTemplate.Delete();
        await repository.UpdateAsync(formTemplate, cancellationToken);
        return Result.Success(formTemplate);
    }
}
