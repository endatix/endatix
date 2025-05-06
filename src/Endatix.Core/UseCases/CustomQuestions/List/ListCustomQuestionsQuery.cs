using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.CustomQuestions.List;

/// <summary>
/// Query for retrieving all custom questions for the current tenant.
/// </summary>
public record ListCustomQuestionsQuery : IQuery<Result<IEnumerable<CustomQuestion>>>
{
} 