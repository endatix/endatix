﻿using MediatR;
using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.UseCases.Forms.List;
using Endatix.Api.Infrastructure;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Endpoint for listing forms.
/// </summary>
public class List(IMediator mediator) : Endpoint<FormsListRequest, Results<Ok<IEnumerable<FormModel>>, BadRequest>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("forms");
        Permissions(Allow.AllowAll);
        Summary(s =>
        {
            s.Summary = "List forms";
            s.Description = "Lists all forms with optional pagination.";
            s.Responses[200] = "Forms retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<IEnumerable<FormModel>>, BadRequest>> ExecuteAsync(FormsListRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new ListFormsQuery(request.Page, request.PageSize),
            cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, forms => forms.ToFormModel())
            .SetTypedResults<Ok<IEnumerable<FormModel>>, BadRequest>();
    }
}
