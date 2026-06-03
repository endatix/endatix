using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.UseCases.Email.Dtos;
using Endatix.Core.UseCases.Email.ListTemplates;
using Endatix.Core.Abstractions.Authorization;

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
        Summary(s =>
        {
            s.Summary = "List email templates";
            s.Description = "Returns all registered email templates.";
            s.Responses[200] = "Email templates retrieved successfully.";
            s.Responses[400] = "Invalid request or tenant context.";
        });
        Description(builder => builder
            .Produces<IEnumerable<EmailTemplateSummaryDto>>(200, "application/json")
            .ProducesProblem(400));
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
