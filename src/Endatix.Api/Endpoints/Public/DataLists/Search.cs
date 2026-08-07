using Endatix.Api.Common;
using Endatix.Api.Common.Security;
using Endatix.Api.Endpoints.DataLists;
using Endatix.Api.Infrastructure;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Common.Translations;
using Endatix.Core.Infrastructure.Result;
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
/// Endpoint to search data list items.
/// </summary>
public sealed class Search(
    IMediator mediator,
    IResourceAccessQuery<PublicFormAccessData, PublicFormAccessContext> publicFormAccessPolicy)
    : Endpoint<SearchDataListItemsRequest, Results<Ok<Paged<DataListPublicChoiceModel>>, ProblemHttpResult>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("forms/{formId}/data-lists/{dataListId}/search");
        Group<PublicApiGroup>();
        Policies(AuthorizationPolicies.PublicResourceAccess);
        Summary(s =>
        {
            s.Summary = "Search data list items";
            s.Description =
                "Searches data list choice labels in a runtime form context. Requires the short-lived form access JWT from POST .../forms/{formId}/access-tokens as Authorization: Bearer {jwt}. Match is labels-only (not Value). Optional locale selects Labels.default or a catalog culture key (e.g. es).";
            s.Responses[200] = "Data list choices searched successfully.";
            s.Responses[400] = "Invalid request or access data.";
            s.Responses[401] = "Unauthorized. Send Authorization: Bearer <jwt>";
            s.Responses[404] = "Form not found.";

        });
    }

    /// <inheritdoc />
    public override async Task<Results<Ok<Paged<DataListPublicChoiceModel>>, ProblemHttpResult>> ExecuteAsync(SearchDataListItemsRequest request, CancellationToken ct)
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
        var accessDataResult = await publicFormAccessPolicy.GetAccessData(accessContext, ct);

        if (!accessDataResult.IsSuccess)
        {
            return accessDataResult.ToProblem();
        }

        SearchDataListItemsQuery query = new(
            request.DataListId,
            request.Query,
            request.Skip,
            request.Take,
            request.MatchMode,
            request.Locale);
        var result = await mediator.Send(query, ct);
        if (!result.IsSuccess)
        {
            return result.ToProblem();
        }

        var mapped = result.Value.MapToPaged(DataListMapper.MapPublic);

        return TypedResults.Ok(mapped);
    }
}


/// <summary>
/// Request to search data list items.
/// </summary>
public sealed class SearchDataListItemsRequest
{
    /// <summary>
    /// The ID of the form that owns the runtime context for this data list.
    /// </summary>
    public long FormId { get; init; }

    /// <summary>
    /// The ID of the data list to search.
    /// </summary>
    public long DataListId { get; init; }

    /// <summary>
    /// The query to search for against item labels.
    /// </summary>
    public string? Query { get; init; }

    /// <summary>
    /// Match mode for <see cref="Query"/> against the resolved label key.
    /// Defaults to <see cref="DataListSearchMatchMode.Contains"/>.
    /// </summary>
    public DataListSearchMatchMode MatchMode { get; init; } = DataListSearchMatchMode.Contains;

    /// <summary>
    /// Optional locale for which label key to search.
    /// Omitted, <c>default</c>, or the list default culture searches <c>Labels.default</c>;
    /// a catalog locale (e.g. <c>es</c>) searches that key.
    /// </summary>
    public string? Locale { get; init; }

    /// <summary>
    /// The number of items to skip.
    /// </summary>
    public int Skip { get; init; } = 0;

    /// <summary>
    /// The number of items to take.
    /// </summary>
    public int Take { get; init; } = 25;
}


/// <summary>
/// Validator for the SearchDataListItemsRequest.
/// </summary>
public sealed class SearchDataListItemsValidator : Validator<SearchDataListItemsRequest>
{
    public SearchDataListItemsValidator()
    {
        RuleFor(x => x.FormId).GreaterThan(0);
        RuleFor(x => x.DataListId).GreaterThan(0);
        RuleFor(x => x.Skip).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Take).GreaterThan(0).LessThanOrEqualTo(SearchDataListItemsQuery.MaxTake);
        RuleFor(x => x.MatchMode).IsInEnum();
        RuleFor(x => x.Locale)
            .MaximumLength(IHasTranslations.MAX_CULTURE_CODE_LENGTH)
            .Must(locale => TranslationCultureNormalizer.IsValidCultureCode(locale))
            .WithMessage("Locale must be a valid culture code (e.g. 'es' or 'en-US') or 'default'.")
            .When(x => !string.IsNullOrWhiteSpace(x.Locale));
    }
}
