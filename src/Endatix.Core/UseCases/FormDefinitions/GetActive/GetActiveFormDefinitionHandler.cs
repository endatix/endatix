using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Features.ReCaptcha;

namespace Endatix.Core.UseCases.FormDefinitions.GetActive;

public class GetActiveFormDefinitionHandler(
    IFormsRepository formRepository,
    IRepository<CustomQuestion> customQuestionsRepository,
    IReCaptchaPolicyService reCaptchaPolicyService,
    ICurrentUserAuthorizationService authorizationService
    )
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

        if (!formWithActiveDefinition.IsPublic)
        {
            var accessResult = await authorizationService.ValidateAccessAsync(
                request.RequiredPermission,
                cancellationToken);

            if (!accessResult.IsSuccess)
            {
                return accessResult;
            }
        }

        var tenantId = formWithActiveDefinition.TenantId;

        var customQuestions = await customQuestionsRepository.ListAsync(
            new CustomQuestionSpecifications.ByTenantId(tenantId),
            cancellationToken);

        var customQuestionsJson = customQuestions?.Select(q => q.JsonData);

        var activeDefinitionDto = new ActiveDefinitionDto(formWithActiveDefinition.ActiveDefinition)
        {
            ThemeJsonData = formWithActiveDefinition.Theme?.JsonData,
            RequiresReCaptcha = reCaptchaPolicyService.RequiresReCaptcha(formWithActiveDefinition),
            CustomQuestions = customQuestionsJson ?? []
        };

        return Result.Success(activeDefinitionDto);
    }
}
