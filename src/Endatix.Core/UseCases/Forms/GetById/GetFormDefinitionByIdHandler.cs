using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Forms.GetById;

public class GetFormByIdHandler(IRepository<Form> repository) : IQueryHandler<GetFormByIdQuery, Result<Form>>
{
    public async Task<Result<Form>> Handle(GetFormByIdQuery request, CancellationToken cancellationToken)
    {
        var form = await repository.GetByIdAsync(request.FormId, cancellationToken);
        if (form == null)
        {
            return Result.NotFound("Form not found.");
        }
        return Result.Success(form);
    }
}