using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Forms.Update;

public class UpdateFormHandler(IRepository<Form> _repository) : ICommandHandler<UpdateFormCommand, Result<Form>>
{
    public async Task<Result<Form>> Handle(UpdateFormCommand request, CancellationToken cancellationToken)
    {
        var form = await _repository.GetByIdAsync(request.FormId, cancellationToken);
        if (form == null)
        {
            return Result.NotFound("Form not found.");
        }

        form.Name = request.Name;
        form.Description = request.Description;
        form.IsEnabled = request.IsEnabled;
        await _repository.UpdateAsync(form, cancellationToken);
        return Result.Success(form);
    }
}
