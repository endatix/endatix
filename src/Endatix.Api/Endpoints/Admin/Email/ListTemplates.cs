using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Email.Dtos;
using Endatix.Core.UseCases.Email.ListTemplates;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Admin.Email;

/// <summary>
/// Endpoint for listing registered email templates.
/// </summary>
public class ListTemplates(IMediator mediator)
    : EndpointWithoutRequest<Results<Ok<IEnumerable<EmailTemplateSummaryDto>>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint.
    /// </summary>
    public override void Configure()
    {
        Get("/admin/email/templates");
        Policies(SystemRole.PlatformAdmin.Name);
        PreProcessor<AuthorizedAccountStanding<EmptyRequest>>();
        Summary(s =>
        {
            s.Summary = "List email templates";
            s.Description = "Returns all registered email templates.";
            s.Responses[200] = "Email templates retrieved successfully.";
            s.Responses[401] = "Unauthenticated - valid credentials required.";
            s.Responses[403] = "Forbidden - insufficient privileges (PlatformAdmin required).";
            s.DescribeStandingProblems();
        });
        Description(builder => builder.ProducesStandingProblems()
            .Produces<IEnumerable<EmailTemplateSummaryDto>>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status401Unauthorized));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<IEnumerable<EmailTemplateSummaryDto>>, ProblemHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        var query = new ListEmailTemplatesQuery();
        var result = await mediator.Send(query, ct);

        return TypedResultsBuilder.FromResult(result)
            .SetTypedResults<Ok<IEnumerable<EmailTemplateSummaryDto>>, ProblemHttpResult>();
    }
}
