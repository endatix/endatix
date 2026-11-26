using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Forms.PartialUpdate;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Endpoint for partially updating a form.
/// </summary>
public class PartialUpdate(IMediator mediator) : Endpoint<PartialUpdateFormRequest, Results<Ok<PartialUpdateFormResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Patch("forms/{formId}");
        Permissions(Actions.Forms.Edit);
        Summary(s =>
        {
            s.Summary = "Partially update a form";
            s.Description = "Partially updates a form.";
            s.ExampleRequest = new PartialUpdateFormRequest
            {
                FormId = 1,
                Name = "Customer satisfaction",
                IsEnabled = true,
            };
            s.ResponseExamples[200] = new PartialUpdateFormResponse
            {
                Id = "1",
                Name = "Customer satisfaction",
                IsEnabled = true,
                IsPublic = false,
            };
            s.Responses[200] = "Form updated successfully.";
            s.DescribeStandingProblems();
            s.DescribeFolderAssignment("Form or folder not found.");
        });
        Description(builder => builder.ProducesStandingProblems()
            .Produces<PartialUpdateFormResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesFolderAssignmentProblems());
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<PartialUpdateFormResponse>, ProblemHttpResult>> ExecuteAsync(PartialUpdateFormRequest request, CancellationToken ct)
    {
        var folderId = request.FolderId.ParseToLong();

        var result = await mediator.Send(
            new PartialUpdateFormCommand(request.FormId)
            {
                Name = request.Name,
                Description = request.Description,
                IsEnabled = request.IsEnabled,
                IsPublic = request.IsPublic,
                ThemeId = request.ThemeId,
                WebHookSettingsJson = request.WebHookSettingsJson,
                LimitOnePerUser = request.LimitOnePerUser,
                SubmissionTokenExpiryHours = request.SubmissionTokenExpiryHours,
                ClearSubmissionTokenExpiryHours = request.ClearSubmissionTokenExpiryHours,
                Metadata = request.Metadata,
                ClearFolderId = request.ClearFolderId,
                FolderId = folderId,
            },
            ct);

        return TypedResultsBuilder
            .MapResult(result, FormMapper.Map<PartialUpdateFormResponse>)
            .SetTypedResults<Ok<PartialUpdateFormResponse>, ProblemHttpResult>();
    }
}
