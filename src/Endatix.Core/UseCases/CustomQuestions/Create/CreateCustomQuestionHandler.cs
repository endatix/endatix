using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.CustomQuestions.Create;

/// <summary>
/// Handler for creating a new custom question.
/// </summary>
public class CreateCustomQuestionHandler(
    IRepository<CustomQuestion> customQuestionsRepository,
    ITenantContext tenantContext,
    AccountStanding standing
) : ICommandHandler<CreateCustomQuestionCommand, Result<CustomQuestion>>
{
    /// <summary>Creates a handler with dormant account standing.</summary>
    public CreateCustomQuestionHandler(IRepository<CustomQuestion> customQuestionsRepository, ITenantContext tenantContext)
        : this(customQuestionsRepository, tenantContext, new AccountStanding()) { }

    /// <summary>
    /// Handles the creation of a new custom question.
    /// </summary>
    /// <param name="request">The command containing custom question creation data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the created custom question.</returns>
    public async Task<Result<CustomQuestion>> Handle(CreateCustomQuestionCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(request.Name);
        Guard.Against.NullOrEmpty(request.JsonData);
        Guard.Against.NegativeOrZero(tenantContext.TenantId);

        var duplicate = new CustomQuestionSpecifications.ByName(tenantContext.TenantId, request.Name);
        if (await customQuestionsRepository.AnyAsync(duplicate, cancellationToken))
        {
            return Result.Invalid(new ValidationError($"A custom question with the name '{request.Name}' already exists"));
        }

        return await standing.RefusalAsync<CustomQuestion>(cancellationToken) ?? await CreateAsync(request, cancellationToken);
    }

    private async Task<Result<CustomQuestion>> CreateAsync(CreateCustomQuestionCommand request, CancellationToken cancellationToken)
    {
        var customQuestion = new CustomQuestion(tenantContext.TenantId, request.Name, request.JsonData, request.Description);
        await customQuestionsRepository.AddAsync(customQuestion, cancellationToken);

        return Result<CustomQuestion>.Created(customQuestion);
    }
}
