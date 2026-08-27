using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Forms.Update;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Endpoint for updating a form.
/// </summary>
public class Update(IMediator mediator) : Endpoint<UpdateFormRequest, Results<Ok<UpdateFormResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Put("forms/{formId}");
        Permissions(Actions.Forms.Edit);
        Summary(s =>
        {
            s.Summary = "Update a form";
            s.Description = "Updates a form.";
            s.ExampleRequest = new UpdateFormRequest
            {
                FormId = 1,
                Name = "Customer satisfaction",
                Description = "Updated description.",
                IsEnabled = true,
            };
            s.ResponseExamples[200] = new UpdateFormResponse
            {
                Id = "1",
                Name = "Customer satisfaction",
                Description = "Updated description.",
                IsEnabled = true,
                IsPublic = false,
            };
            s.Responses[200] = "Form updated successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form not found.";
        });
        Description(builder => builder
            .Produces<UpdateFormResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<UpdateFormResponse>, ProblemHttpResult>> ExecuteAsync(UpdateFormRequest request, CancellationToken ct)
    {
        var folderId = request.FolderId.ParseToLong();

        var result = await mediator.Send(
            new UpdateFormCommand(
                request.FormId,
                request.Name!,
                request.Description,
                request.IsEnabled!.Value,
                request.WebHookSettingsJson,
                request.LimitOnePerUser,
                request.SubmissionTokenExpiryHours,
                request.ClearSubmissionTokenExpiryHours,
                request.Metadata,
                folderId),
            ct);

        return TypedResultsBuilder
            .MapResult(result, FormMapper.Map<UpdateFormResponse>)
            .SetTypedResults<Ok<UpdateFormResponse>, ProblemHttpResult>();
    }
}
