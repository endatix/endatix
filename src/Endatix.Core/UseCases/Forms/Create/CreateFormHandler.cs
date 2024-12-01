using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Forms.Create;

public class CreateFormHandler : ICommandHandler<CreateFormCommand, Result<Form>>
{
    private readonly IFormsRepository _formsRepository;

    public CreateFormHandler(IFormsRepository formsRepository)
    {
        _formsRepository = formsRepository;
    }

    public async Task<Result<Form>> Handle(CreateFormCommand request, CancellationToken cancellationToken)
    {
        var newForm = new Form(request.Name, request.Description, request.IsEnabled);
        var newFormDefinition = new FormDefinition(newForm, jsonData: request.FormDefinitionJsonData);

        var form = await _formsRepository.CreateFormWithDefinitionAsync(newForm, newFormDefinition, cancellationToken);

        return Result<Form>.Created(form);
    }
}
