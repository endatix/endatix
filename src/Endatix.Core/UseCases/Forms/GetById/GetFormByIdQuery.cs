using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Forms;

namespace Endatix.Core.UseCases.Forms.GetById;

/// <summary>
/// Query for getting a form by ID.
/// </summary>
public record GetFormByIdQuery : IQuery<Result<FormDto>>
{
    public long FormId { get; init; }

    public GetFormByIdQuery(long formId)
    {
        Guard.Against.NegativeOrZero(formId);

        FormId = formId;
    }
}
