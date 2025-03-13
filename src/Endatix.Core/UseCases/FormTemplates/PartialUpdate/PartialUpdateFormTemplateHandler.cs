using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormTemplates.PartialUpdate;

public class PartialUpdateFormTemplateHandler(IRepository<FormTemplate> repository) 
    : ICommandHandler<PartialUpdateFormTemplateCommand, Result<FormTemplate>>
{
    public async Task<Result<FormTemplate>> Handle(PartialUpdateFormTemplateCommand request, CancellationToken cancellationToken)
    {
        var formTemplate = await repository.GetByIdAsync(request.FormTemplateId, cancellationToken);
        if (formTemplate == null)
        {
            return Result.NotFound("Form template not found.");
        }

        formTemplate.Name = request.Name ?? formTemplate.Name;
        formTemplate.Description = request.Description ?? formTemplate.Description;
        formTemplate.JsonData = request.JsonData ?? formTemplate.JsonData;
        formTemplate.IsEnabled = request.IsEnabled ?? formTemplate.IsEnabled;

        await repository.UpdateAsync(formTemplate, cancellationToken);
        return Result.Success(formTemplate);
    }
}
