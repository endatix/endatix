using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormDefinitions.GetActive;

/// <summary>
/// Query for getting the active form definition.
/// </summary>
public record GetActiveFormDefinitionQuery : IQuery<Result<ActiveDefinitionDto>>
{
    public long FormId { get; init; }
    public string? UserId { get; init; }
    public string RequiredPermission { get; init; }

    public GetActiveFormDefinitionQuery(long formId, string? userId, string requiredPermission)
    {
        Guard.Against.NegativeOrZero(formId);
        Guard.Against.NullOrEmpty(requiredPermission);
        
        FormId = formId;
        UserId = userId;
        RequiredPermission = requiredPermission;
    }
}
