using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Email.GetSettings;

namespace Endatix.Api.Endpoints.Admin.Email;

/// <summary>
/// Endpoint for retrieving the active email settings.
/// </summary>
public class GetEmailSettings(IMediator mediator)
    : EndpointWithoutRequest<Results<Ok<EmailSettingsDto>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint.
    /// </summary>
    public override void Configure()
    {
        Get("/admin/email/settings");
        Policies(SystemRole.PlatformAdmin.Name);
        Summary(s =>
        {
            s.Summary = "Get email settings";
            s.Description = "Returns the active email provider settings.";
            s.Responses[200] = "Email settings retrieved successfully.";
            s.Responses[400] = "Invalid request or tenant context.";
        });
        Description(builder => builder
            .Produces<EmailSettingsDto>(200, "application/json")
            .ProducesProblem(400));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<EmailSettingsDto>, ProblemHttpResult>> ExecuteAsync(CancellationToken ct)
    {
        var query = new GetEmailSettingsQuery();
        var result = await mediator.Send(query, ct);

        return TypedResultsBuilder.FromResult(result)
            .SetTypedResults<Ok<EmailSettingsDto>, ProblemHttpResult>();
    }
}
