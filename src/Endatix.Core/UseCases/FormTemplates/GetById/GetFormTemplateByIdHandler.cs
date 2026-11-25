using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormTemplates.GetById;

/// <summary>Reads a visible form template.</summary>
[DirectCallOnly]
public class GetFormTemplateByIdHandler(IRepository<FormTemplate> repository) : IQueryHandler<GetFormTemplateByIdQuery, Result<FormTemplate>>
{
    private ContentQueryScope Selection { get; init; } = ContentQueryScope.Repository;

    public async Task<Result<FormTemplate>> Handle(GetFormTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        var formTemplate = await Selection.FindAsync(repository, request.FormTemplateId, cancellationToken);
        if (formTemplate == null)
        {
            return Result.NotFound("Form template not found.");
        }
        return Result.Success(formTemplate);
    }

    internal sealed class Registered(IRepository<FormTemplate> repository, ContentQueryScope scope)
        : RegisteredContentHandler<GetFormTemplateByIdQuery, Result<FormTemplate>>(
            new GetFormTemplateByIdHandler(repository) { Selection = scope });
}
