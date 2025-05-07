using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
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
    ITenantContext tenantContext
) : ICommandHandler<CreateCustomQuestionCommand, Result<CustomQuestion>>
{
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

        // Check if a custom question with the same name already exists for this tenant
        var existingQuestion = await customQuestionsRepository.FirstOrDefaultAsync(
            new CustomQuestionSpecifications.ByName(request.Name),
            cancellationToken);

        if (existingQuestion != null)
        {
            return Result.Invalid(new ValidationError($"A custom question with the name '{request.Name}' already exists"));
        }

        var customQuestion = new CustomQuestion(tenantContext.TenantId, request.Name, request.JsonData, request.Description);
        await customQuestionsRepository.AddAsync(customQuestion, cancellationToken);

        return Result<CustomQuestion>.Created(customQuestion);
    }
} 