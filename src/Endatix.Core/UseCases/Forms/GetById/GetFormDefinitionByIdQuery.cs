using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Forms.GetById;

/// <summary>
/// Query for getting a form by ID.
/// </summary>
public record GetFormByIdQuery : IQuery<Result<Form>>
{
    public long FormId { get; init; }

    public GetFormByIdQuery(long formId)
    {
        Guard.Against.NegativeOrZero(formId);

        FormId = formId;
    }
}
