using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.FormTemplates.GetById;
using Endatix.Core.Abstractions.Authorization;

namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Endpoint for getting a form template by ID. 
/// </summary>
public class GetById(IMediator mediator) : Endpoint<GetFormTemplateByIdRequest, Results<Ok<FormTemplateModel>, BadRequest, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("form-templates/{formTemplateId}");
        Permissions(Actions.Templates.View);
        Summary(s =>
        {
            s.Summary = "Get a form template by ID";
            s.Description = "Gets a form template by its ID.";
            s.Responses[200] = "Form template retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form template not found.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<FormTemplateModel>, BadRequest, NotFound>> ExecuteAsync(GetFormTemplateByIdRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetFormTemplateByIdQuery(request.FormTemplateId),
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, FormTemplateMapper.Map<FormTemplateModel>)
            .SetTypedResults<Ok<FormTemplateModel>, BadRequest, NotFound>();
    }
}
