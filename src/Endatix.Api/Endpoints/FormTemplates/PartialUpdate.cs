using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.FormTemplates.PartialUpdate;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Api.Common;

namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Endpoint for partially updating a form template.
/// </summary>
public class PartialUpdate(IMediator mediator) : Endpoint<PartialUpdateFormTemplateRequest, Results<Ok<PartialUpdateFormTemplateResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Patch("form-templates/{formTemplateId}");
        Permissions(Actions.Templates.Edit);
        Summary(s =>
        {
            s.Summary = "Partially update a form template";
            s.Description = "Partially updates a form template.";
            s.Responses[200] = "Form template updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form template not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<PartialUpdateFormTemplateResponse>, ProblemHttpResult>> ExecuteAsync(PartialUpdateFormTemplateRequest request, CancellationToken ct)
    {
        var folderId = request.FolderId.ParseToLong();

        var result = await mediator.Send(
            new PartialUpdateFormTemplateCommand(request.FormTemplateId, request.Name, request.Description, request.JsonData)
            {
                ClearFolderId = request.ClearFolderId,
                FolderId = folderId,
            },
            ct);

        return TypedResultsBuilder
         .MapResult(
            result,
            formTemplate => formTemplate.ToFormTemplateModel<PartialUpdateFormTemplateResponse>()
        )
        .SetTypedResults<Ok<PartialUpdateFormTemplateResponse>, ProblemHttpResult>();
    }
}
