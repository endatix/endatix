using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Forms.Create;

public class CreateFormHandler : ICommandHandler<CreateFormCommand, Result<Form>>
{
    private readonly IFormsRepository _formsRepository;
    private readonly IEntityFactory _entityFactory;

    public CreateFormHandler(IFormsRepository formsRepository, IEntityFactory entityFactory)
    {
        _formsRepository = formsRepository;
        _entityFactory = entityFactory;
    }

    public async Task<Result<Form>> Handle(CreateFormCommand request, CancellationToken cancellationToken)
    {
        var newForm = _entityFactory.CreateForm(request.Name, request.Description, request.IsEnabled);
        var newFormDefinition = _entityFactory.CreateFormDefinition(isDraft: true, jsonData: request.FormDefinitionJsonData);

        var form = await _formsRepository.CreateFormWithDefinitionAsync(newForm, newFormDefinition, cancellationToken);

        return Result<Form>.Created(form);
    }
}
