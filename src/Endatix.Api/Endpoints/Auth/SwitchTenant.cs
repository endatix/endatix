using Endatix.Api.Infrastructure;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;
using Endatix.Core.UseCases.Identity.SwitchTenant;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Switches the current session to another tenant the user already belongs to.
/// </summary>
public sealed class SwitchTenant(IMediator mediator, IConfiguration configuration)
    : Endpoint<SwitchTenantRequest, Results<Ok<AssumeTenantResponse>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Post("auth/switch-tenant");
        Summary(s =>
        {
            s.Summary = "Switch tenant";
            s.Description = "Re-issues access and refresh tokens for a tenant the current user is a member of.";
            s.Responses[200] = "Switched session issued.";
            s.Responses[400] = "Invalid tenant id, or the session is assumed.";
            s.Responses[403] = "The current user is not a member of the tenant.";
            s.Responses[404] = "Multi-tenancy is disabled, or the tenant was not found.";
        });
    }

    public override async Task<Results<Ok<AssumeTenantResponse>, ProblemHttpResult>> ExecuteAsync(
        SwitchTenantRequest request,
        CancellationToken cancellationToken)
    {
        if (!MultiTenancyGate.IsEnabled(configuration))
        {
            return TypedResultsBuilder
                .FromResult(Result<AssumeTenantResponse>.NotFound(MultiTenancyGate.DisabledMessage))
                .SetTypedResults<Ok<AssumeTenantResponse>, ProblemHttpResult>();
        }

        var result = await mediator.Send(new SwitchTenantCommand(request.TenantId), cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, tokens => new AssumeTenantResponse(tokens.AccessToken.Token, tokens.RefreshToken.Token))
            .SetTypedResults<Ok<AssumeTenantResponse>, ProblemHttpResult>();
    }
}

public sealed class SwitchTenantRequest
{
    public long TenantId { get; set; }
}

public sealed class SwitchTenantValidator : Validator<SwitchTenantRequest>
{
    public SwitchTenantValidator()
    {
        RuleFor(request => request.TenantId).GreaterThan(0);
    }
}
