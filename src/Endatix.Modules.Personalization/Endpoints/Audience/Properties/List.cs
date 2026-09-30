using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Modules.Personalization.Features.Properties;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Personalization.Endpoints.Audience.Properties;

/// <summary>
/// Lists audience properties for a form.
/// </summary>
public sealed class List(
    IMediator mediator,
    ITenantContext tenantContext)
    : Endpoint<ListAudiencePropertiesRequest, Results<Ok<IReadOnlyList<AudiencePropertyResponse>>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("forms/{formId}/audience/properties");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "List audience properties";
            summary.Description = "Lists property definitions for a form's audience.";
            summary.Responses[200] = "Properties retrieved.";
            summary.Responses[404] = "Form not found.";
        });
        Description(builder => builder
            .Produces<IReadOnlyList<AudiencePropertyResponse>>(200, "application/json")
            .ProducesProblem(404));
    }

    public override async Task<Results<Ok<IReadOnlyList<AudiencePropertyResponse>>, ProblemHttpResult>> ExecuteAsync(
        ListAudiencePropertiesRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new ListAudiencePropertiesQuery(tenantContext.TenantId, request.FormId),
            ct);

        return TypedResultsBuilder
            .MapResult(result, Map)
            .SetTypedResults<Ok<IReadOnlyList<AudiencePropertyResponse>>, ProblemHttpResult>();
    }

    private static IReadOnlyList<AudiencePropertyResponse> Map(IReadOnlyList<AudiencePropertyDto> properties) =>
        properties.Select(AudiencePropertyResponse.FromDto).ToList();
}

/// <summary>
/// Validator for list audience properties.
/// </summary>
public sealed class ListAudiencePropertiesValidator : Validator<ListAudiencePropertiesRequest>
{
    public ListAudiencePropertiesValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
    }
}

/// <summary>
/// Request to list audience properties.
/// </summary>
public sealed class ListAudiencePropertiesRequest
{
    public long FormId { get; init; }
}
