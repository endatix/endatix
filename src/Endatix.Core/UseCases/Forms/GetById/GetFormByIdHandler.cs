using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Forms.GetById;

public class GetFormByIdHandler(IFormsRepository repository) : IQueryHandler<GetFormByIdQuery, Result<FormDto>>
{
    public async Task<Result<FormDto>> Handle(GetFormByIdQuery request, CancellationToken cancellationToken)
    {
        var spec = new FormByIdWithSubmissionsCountSpec(request.FormId);
        var form = (await repository.ListAsync(spec, cancellationToken)).FirstOrDefault();
        if (form == null)
        {
            return Result.NotFound("Form not found.");
        }
        return Result.Success(form);
    }
}