using Endatix.Core.Abstractions;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormTemplates.Delete;

/// <summary>Deletes a visible form template.</summary>
[DirectCallOnly]
public class DeleteFormTemplateHandler(IRepository<FormTemplate> repository) : ICommandHandler<DeleteFormTemplateCommand, Result<FormTemplate>>
{
    private ContentQueryScope Selection { get; init; } = ContentQueryScope.Repository;

    public async Task<Result<FormTemplate>> Handle(DeleteFormTemplateCommand request, CancellationToken cancellationToken)
    {
        var formTemplate = await Selection.FindAsync(repository, request.FormTemplateId, cancellationToken);
        if (formTemplate == null)
        {
            return Result.NotFound("Form template not found.");
        }

        formTemplate.Delete();
        await repository.UpdateAsync(formTemplate, cancellationToken);
        return Result.Success(formTemplate);
    }

    internal sealed class Registered(IRepository<FormTemplate> repository, ContentQueryScope scope)
        : RegisteredContentHandler<DeleteFormTemplateCommand, Result<FormTemplate>>(
            new DeleteFormTemplateHandler(repository) { Selection = scope });
}
