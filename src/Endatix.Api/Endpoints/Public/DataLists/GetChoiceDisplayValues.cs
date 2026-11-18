using Endatix.Api.Common;
using Endatix.Api.Common.Security;
using Endatix.Api.Endpoints.Common;
using Endatix.Api.Endpoints.DataLists;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization.PublicForm;
using Endatix.Core.Authorization.Access;
using Endatix.Core.UseCases.DataLists.Search;
using Endatix.Infrastructure.Features.AccessControl;
using Endatix.Infrastructure.Identity.Authorization;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Public.DataLists;

/// <summary>
/// Public endpoint to get choice display values.
/// </summary>
public sealed class GetChoiceDisplayValues(
    IMediator mediator,
    IResourceAccessQuery<PublicFormAccessData, PublicFormAccessContext> publicFormAccessPolicy,
    IFormRuntimeScopeReader runtimeScopeReader)
    : Endpoint<GetChoiceDisplayValuesRequest, Results<Ok<IReadOnlyCollection<DataListPublicChoiceModel>>, ProblemHttpResult>>
{
    /// <summary>Display-value reads one client may make per minute.</summary>
    public const int REQUESTS_PER_MINUTE = 300;

    /// <inheritdoc />
    public override void Configure()
    {
        Get("forms/{formId}/data-lists/{dataListId}/display-values");
        Group<PublicApiGroup>();
        Policies(AuthorizationPolicies.PublicResourceAccess);
        Throttle(REQUESTS_PER_MINUTE, 60);
        Summary(s =>
        {
            s.Summary = "Resolve data list display values";
            s.Description =
                "Returns { value, labels: { default, ... } } for stored values in a runtime form context. Requires the form access JWT as Authorization: Bearer {jwt}. Use includeLocales to add catalog locales to labels. " +
                "Answers only for a data list a definition of the form binds (the active one, a draft or an older one), so older submissions keep their labels; any other list is not found. " +
                $"Limited to {REQUESTS_PER_MINUTE} requests a minute per client.";
            s.Responses[200] = "Data list display values resolved successfully.";
            s.Responses[400] = "Invalid request or access data.";
            s.Responses[401] = "Unauthorized. Send Authorization: Bearer <jwt>";
            s.Responses[404] = "Form or data list not found.";
            s.Responses[429] = "Too many requests.";

        });
        Description(builder => builder
            .Produces<IReadOnlyCollection<DataListPublicChoiceModel>>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests));
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<IReadOnlyCollection<DataListPublicChoiceModel>>, ProblemHttpResult>> ExecuteAsync(
        GetChoiceDisplayValuesRequest request,
        CancellationToken ct)
    {
        var token = FormAccessTokenReader.ReadToken(HttpContext.Request);
        if (string.IsNullOrWhiteSpace(token))
        {
            return TypedResults.Problem(
                title: "Unauthorized",
                detail: "A form access token is required. Send Authorization: Bearer <jwt>",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        PublicFormAccessContext accessContext = new(request.FormId, token, SubmissionTokenType.FormToken);
        var accessDataResult = await publicFormAccessPolicy
            .GetAccessData(accessContext, ct);

        if (!accessDataResult.IsSuccess)
        {
            return accessDataResult.ToProblem();
        }

        var result = await PublicDataListScope.RefuseUnlessFormReachesAsync<IReadOnlyCollection<DataListChoiceDisplayValueDto>>(
            runtimeScopeReader,
            request.FormId,
            request.DataListId,
            DataListReach.AnyDefinition,
            ct);
        if (result is null)
        {
            GetDataListChoiceDisplayValuesQuery query = new(request.DataListId, request.Values, request.IncludeLocales);
            result = await mediator.Send(query, ct);
        }

        return TypedResultsBuilder
            .MapResult(result, items => (IReadOnlyCollection<DataListPublicChoiceModel>)items.Select(DataListMapper.MapPublic).ToArray())
            .SetTypedResults<Ok<IReadOnlyCollection<DataListPublicChoiceModel>>, ProblemHttpResult>();
    }
}


/// <summary>
/// Request to get choice display values.
/// </summary>
public sealed class GetChoiceDisplayValuesRequest
{
    /// <summary>
    /// The ID of the form that owns the runtime context for this data list.
    /// </summary>
    public long FormId { get; init; }

    /// <summary>
    /// The ID of the data list.
    /// </summary>
    public long DataListId { get; init; }

    /// <summary>
    /// The values to get display values for.
    /// </summary>
    public IReadOnlyCollection<string> Values { get; init; } = [];

    /// <summary>
    /// Locales to return as nested <c>labels</c>, repeated (<c>includeLocales=es&amp;includeLocales=fr</c>)
    /// or comma-separated (<c>includeLocales=es,fr</c>). Locales outside the catalog are ignored.
    /// </summary>
    public IReadOnlyCollection<string> IncludeLocales { get; init; } = [];
}

/// <summary>
/// Validator for the GetChoiceDisplayValuesRequest.
/// </summary>
public sealed class GetChoiceDisplayValuesValidator : Validator<GetChoiceDisplayValuesRequest>
{
    public GetChoiceDisplayValuesValidator()
    {
        RuleFor(x => x.FormId).GreaterThan(0);
        RuleFor(x => x.DataListId).GreaterThan(0);
        RuleFor(x => x.Values)
            .NotNull()
            .Must(values => values.Count > 0)
            .WithMessage("'values' query parameter is required.");
        RuleFor(x => x.IncludeLocales)
            .IsIncludeLocales()
            .When(x => x.IncludeLocales is { Count: > 0 });
    }
}
