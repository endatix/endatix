using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormTemplates.GetById;

public class GetFormTemplateByIdHandler(IRepository<FormTemplate> repository) 
    : IQueryHandler<GetFormTemplateByIdQuery, Result<FormTemplate>>
{
    public async Task<Result<FormTemplate>> Handle(GetFormTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var formTemplate = await repository.GetByIdAsync(request.FormTemplateId, cancellationToken);
        if (formTemplate == null)
        {
            return Result.NotFound("Form template not found.");
        }
        return Result.Success(formTemplate);
    }
}
