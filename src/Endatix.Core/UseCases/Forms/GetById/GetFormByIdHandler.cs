using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;

namespace Endatix.Core.UseCases.Forms.GetById;

/// <summary>Reads a visible form with its submission count.</summary>
[DirectCallOnly]
public class GetFormByIdHandler(IFormsRepository repository) : IQueryHandler<GetFormByIdQuery, Result<FormDto>>
{
    private ContentQueryScope Selection { get; init; } = ContentQueryScope.Repository;

    public async Task<Result<FormDto>> Handle(GetFormByIdQuery request, CancellationToken cancellationToken)
    {
        if (Selection is { TenantId: <= 0 })
        {
            return Result.NotFound("Form not found.");
        }
        var spec = new FormByIdWithSubmissionsCountSpec(request.FormId);
        Selection.Apply(spec);
        var form = await repository.FirstOrDefaultAsync(spec, cancellationToken);
        if (form == null)
        {
            return Result.NotFound("Form not found.");
        }
        return Result.Success(form);
    }

    internal sealed class Registered(IFormsRepository repository, ContentQueryScope tenant)
        : RegisteredContentHandler<GetFormByIdQuery, Result<FormDto>>(
            new GetFormByIdHandler(repository) { Selection = tenant });
}
