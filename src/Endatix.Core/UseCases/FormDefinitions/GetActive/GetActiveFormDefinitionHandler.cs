using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.UseCases.FormDefinitions.GetActive;

public class GetActiveFormDefinitionHandler(
    IFormsRepository formRepository,
    IRepository<CustomQuestion> customQuestionsRepository) 
    : IQueryHandler<GetActiveFormDefinitionQuery, Result<ActiveDefinitionDto>>
{
    public async Task<Result<ActiveDefinitionDto>> Handle(GetActiveFormDefinitionQuery request, CancellationToken cancellationToken)
    {
        var spec = new ActiveFormDefinitionByFormIdSpec(request.FormId);
        var formWithActiveDefinition = await formRepository.SingleOrDefaultAsync(spec, cancellationToken);

        if (formWithActiveDefinition == null || formWithActiveDefinition.ActiveDefinition == null)
        {
            return Result.NotFound("Active form definition not found.");
        }

        var tenantId = formWithActiveDefinition.TenantId;

        var customQuestions = await customQuestionsRepository.ListAsync(
            new CustomQuestionSpecifications.ByTenantId(tenantId),
            cancellationToken);

        var customQuestionsJson = customQuestions?.Select(q => q.JsonData);

        var activeDefinitionDto = new ActiveDefinitionDto(
            formWithActiveDefinition.ActiveDefinition,
            formWithActiveDefinition.Theme?.JsonData,
            customQuestionsJson);

        return Result.Success(activeDefinitionDto);
    }
}
