using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Features.TestSubmissions;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.TestSubmissions;

/// <summary>The route and the OpenAPI text a test-submission endpoint publishes.</summary>
public readonly record struct TestRoute(
    string Route,
    string Summary,
    string Description,
    string Created,
    string BadRequest,
    string NotFound);

/// <summary>
/// The shared shape of the test-submission endpoints: both need <c>forms.test</c> and <c>forms.edit</c>, because the
/// submission carries the person's identifier and values, which every other Audience route shows only to
/// <c>forms.edit</c>, and both answer with the same created response.
/// </summary>
public abstract class TestSubmissionEndpoint<TRequest>(IMediator mediator, ITenantContext tenantContext)
    : Endpoint<TRequest, Results<Created<TestSubmissionResponse>, ProblemHttpResult>>
    where TRequest : notnull
{
    protected IMediator Mediator { get; } = mediator;

    protected long TenantId => tenantContext.TenantId;

    protected void ConfigureRoute(TestRoute route)
    {
        Post(route.Route);
        PermissionsAll(Actions.Forms.Test, Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = route.Summary;
            summary.Description = route.Description;
            summary.Responses[201] = route.Created;
            summary.Responses[400] = route.BadRequest;
            summary.Responses[404] = route.NotFound;
        });
        Description(builder => builder
            .Produces<TestSubmissionResponse>(StatusCodes.Status201Created, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404));
    }

    protected Results<Created<TestSubmissionResponse>, ProblemHttpResult> Respond(
        Result<OpenedTestSubmissionDto> result) =>
        TypedResultsBuilder
            .MapResult(result, TestSubmissionResponse.From)
            .SetTypedResults<Created<TestSubmissionResponse>, ProblemHttpResult>();
}
