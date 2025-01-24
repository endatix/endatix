using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Forms.Delete;

/// <summary>
/// Command for deleting a form.
/// </summary>
public record DeleteFormCommand : ICommand<Result<Form>>
{
    public long FormId { get; init; }

    public DeleteFormCommand(long formId)
    {
        Guard.Against.NegativeOrZero(formId);
        FormId = formId;
    }
}
