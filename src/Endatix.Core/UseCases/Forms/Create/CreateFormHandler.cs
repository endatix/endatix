using System.Threading;
using System.Threading.Tasks;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Forms.Create;

public class CreateFormHandler(IRepository<Form> _repository) : ICommandHandler<CreateFormCommand, Result<Form>>
{
    public async Task<Result<Form>> Handle(CreateFormCommand request, CancellationToken cancellationToken)
    {
        var newForm = new Form(request.Name, request.Description, request.IsEnabled, request.FormDefinitionJsonData);
        await _repository.AddAsync(newForm, cancellationToken);
        return Result<Form>.Created(newForm);
    }
}
