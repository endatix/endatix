using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Forms.Create;
using Endatix.Core.Abstractions.Authorization;
using System.Text.Json;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Endpoint for creating a new form and an active form definition.
/// </summary>
public class Create(IMediator mediator) : Endpoint<CreateFormRequest, Results<Created<CreateFormResponse>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Post("forms");
        Permissions(Actions.Forms.Create);
        Summary(s =>
        {
            s.Summary = "Create a new form";
            s.Description = "Creates a new form and an active form definition for it.";
            s.ExampleRequest = new CreateFormRequest
            {
                Name = "Customer satisfaction",
                Description = "A customer satisfaction survey.",
                IsEnabled = true,
                FormDefinitionSchema = JsonSerializer.Deserialize<JsonElement>("{}"),
            };
            s.ResponseExamples[201] = new CreateFormResponse
            {
                Id = "1",
                Name = "Customer satisfaction",
                IsEnabled = true,
                IsPublic = false,
            };
            s.Responses[201] = "Form created successfully.";
            s.Responses[400] = "Invalid input data.";
        });
        Description(builder => builder
            .Produces<CreateFormResponse>(StatusCodes.Status201Created, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest));
    }

    /// <inheritdoc/>
    public override async Task<Results<Created<CreateFormResponse>, ProblemHttpResult>> ExecuteAsync(CreateFormRequest request, CancellationToken ct)
    {
        var formDefinitionJsonData = request.FormDefinitionSchema.HasValue
            ? JsonSerializer.Serialize(request.FormDefinitionSchema.Value)
            : request.FormDefinitionJsonData!;

        var webHookSettingsJson = request.WebHookSettings.HasValue
            ? JsonSerializer.Serialize(request.WebHookSettings.Value)
            : request.WebHookSettingsJson;

        var folderId = request.FolderId.ParseToLong();

        var createFormCommand = new CreateFormCommand(
            request.Name!,
            request.Description,
            request.IsEnabled!.Value,
            formDefinitionJsonData,
            webHookSettingsJson,
            request.LimitOnePerUser ?? false,
            request.SubmissionTokenExpiryHours,
            request.Metadata,
            folderId);

        var result = await mediator.Send(createFormCommand, ct);

        return TypedResultsBuilder
            .MapResult(result, FormMapper.Map<CreateFormResponse>)
            .SetTypedResults<Created<CreateFormResponse>, ProblemHttpResult>();
    }
}
