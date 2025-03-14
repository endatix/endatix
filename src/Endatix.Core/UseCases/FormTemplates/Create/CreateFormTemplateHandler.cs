using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormTemplates.Create;

public class CreateFormTemplateHandler(IRepository<FormTemplate> repository, ITenantContext tenantContext) 
    : ICommandHandler<CreateFormTemplateCommand, Result<FormTemplate>>
{
    public async Task<Result<FormTemplate>> Handle(CreateFormTemplateCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(tenantContext.TenantId);

        var formTemplate = new FormTemplate(
            tenantContext.TenantId,
            request.Name,
            request.Description,
            request.JsonData,
            request.IsEnabled
        );

        await repository.AddAsync(formTemplate, cancellationToken);
        return Result<FormTemplate>.Created(formTemplate);
    }
}
