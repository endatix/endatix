using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Forms.PartialUpdate;

public class PartialUpdateFormHandler(IRepository<Form> _repository) : ICommandHandler<PartialUpdateFormCommand, Result<Form>>
{
    public async Task<Result<Form>> Handle(PartialUpdateFormCommand request, CancellationToken cancellationToken)
    {
        var form = await _repository.GetByIdAsync(request.FormId, cancellationToken);
        if (form == null)
        {
            return Result.NotFound("Form not found.");
        }

        form.Name = request.Name ?? form.Name;
        form.Description = request.Description ?? form.Description;
        form.IsEnabled = request.IsEnabled ?? form.IsEnabled;
        await _repository.UpdateAsync(form, cancellationToken);
        return Result.Success(form);
    }
}
