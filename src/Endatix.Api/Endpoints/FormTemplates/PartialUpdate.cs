using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.FormTemplates.PartialUpdate;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Endpoint for partially updating a form template.
/// </summary>
public class PartialUpdate(IMediator mediator)
    : Endpoint<PartialUpdateFormTemplateRequest, Results<Ok<PartialUpdateFormTemplateResponse>, ProblemHttpResult>>
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
            s.ExampleRequest = new PartialUpdateFormTemplateRequest
            {
                FormTemplateId = 1,
                Name = "Customer satisfaction",
                Description = "Updated description.",
            };
            s.ResponseExamples[200] = new PartialUpdateFormTemplateResponse
            {
                Id = "1",
                Name = "Customer satisfaction",
                Description = "Updated description.",
                JsonData = "{}",
            };
            s.Responses[200] = "Form template updated successfully.";
            s.DescribeStandingProblems();
            s.DescribeFolderAssignment("Form template or folder not found.");
        });
        Description(builder => builder.ProducesStandingProblems()
            .Produces<PartialUpdateFormTemplateResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesFolderAssignmentProblems());
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<PartialUpdateFormTemplateResponse>, ProblemHttpResult>> ExecuteAsync(
        PartialUpdateFormTemplateRequest request,
        CancellationToken ct)
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
                formTemplate => formTemplate.ToFormTemplateModel<PartialUpdateFormTemplateResponse>())
            .SetTypedResults<Ok<PartialUpdateFormTemplateResponse>, ProblemHttpResult>();
    }
}
