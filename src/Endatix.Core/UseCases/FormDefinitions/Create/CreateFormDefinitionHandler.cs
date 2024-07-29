using System.Threading;
using System.Threading.Tasks;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormDefinitions.Create;

public class CreateFormDefinitionHandler(IRepository<FormDefinition> definitionsRepository, IRepository<Form> formsRepository) : ICommandHandler<CreateFormDefinitionCommand, Result<FormDefinition>>
{
    public async Task<Result<FormDefinition>> Handle(CreateFormDefinitionCommand request, CancellationToken cancellationToken)
    {
        var form = await formsRepository.GetByIdAsync(request.FormId, cancellationToken);
        if (form == null)
        {
            return Result.NotFound("Form not found.");
        }

        var newFormDefinition = new FormDefinition(request.IsDraft, request.JsonData, request.IsActive)
        {
            FormId = request.FormId
        };

        await definitionsRepository.AddAsync(newFormDefinition, cancellationToken);
        return Result<FormDefinition>.Created(newFormDefinition);
    }
}
