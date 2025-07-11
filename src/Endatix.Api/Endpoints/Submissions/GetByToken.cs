﻿using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Submissions.GetByToken;
using Errors = Microsoft.AspNetCore.Mvc;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Endpoint for getting a form submission by ID.
/// </summary>
public class GetByToken(IMediator mediator) : Endpoint<GetByTokenRequest, Results<Ok<SubmissionModel>, BadRequest<Errors.ProblemDetails>, NotFound>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("forms/{formId}/submissions/by-token/{submissionToken}");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get a single submission by token";
            s.Description = "Gets a single submission based on its token and its respective formId";
            s.Responses[200] = "The Submission was retrieved successfully";
            s.Responses[400] = "Invalid input data.";
            s.Responses[404] = "Form submission not found";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<SubmissionModel>, BadRequest<Errors.ProblemDetails>, NotFound>> ExecuteAsync(GetByTokenRequest request, CancellationToken cancellationToken)
    {
        var query = new GetByTokenQuery(request.FormId, request.SubmissionToken!);
        var result = await mediator.Send(query, cancellationToken);

        return TypedResultsBuilder
                    .MapResult(result, SubmissionMapper.Map<SubmissionModel>)
                    .SetTypedResults<Ok<SubmissionModel>, BadRequest<Errors.ProblemDetails>, NotFound>();

    }
}
