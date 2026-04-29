using Endatix.Api.Common.FeatureFlags;
using Endatix.Api.Infrastructure;
using Endatix.Core.Authorization.Access;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.DataLists.Search;
using Endatix.Framework.FeatureFlags;
using Endatix.Infrastructure.Features.AccessControl;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.DataLists;

/// <summary>
/// Endpoint to search data list items.
/// </summary>
public sealed class Search(
    IMediator mediator,
    IResourceAccessQuery<PublicFormAccessData, PublicFormAccessContext> publicFormAccessPolicy)
    : Endpoint<SearchDataListItemsRequest, Results<Ok<Paged<DataListPublicChoiceModel>>, ProblemHttpResult>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get(ApiRoutes.Public("forms/{formId}/data-lists/{dataListId}/search"));
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Search data list items";
            s.Description =
                "Searches data list choices in a runtime form context. Access is evaluated like access/public/forms/{formId} (public vs authenticated, optional token + tokenType query parameters; uses the same cached policy).";
        });
        FeatureFlag<EndpointFeatureGate>(FeatureFlags.DataLists);
    }

    public override async Task<Results<Ok<Paged<DataListPublicChoiceModel>>, ProblemHttpResult>> ExecuteAsync(SearchDataListItemsRequest request, CancellationToken ct)
    {
        PublicFormAccessContext accessContext = new(request.FormId, request.Token, request.TokenType);
        var accessDataResult = await publicFormAccessPolicy.GetAccessData(accessContext, ct).ConfigureAwait(false);

        if (!accessDataResult.IsSuccess)
        {
            return accessDataResult.ToProblem();
        }

        SearchDataListItemsQuery query = new(request.DataListId, request.Query, request.Skip, request.Take);
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
    /// Optional access or submission token (same semantics as <c>access/public/forms/{formId}</c>).
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// Token type when <see cref="Token"/> is set.
    /// </summary>
    public SubmissionTokenType? TokenType { get; init; }

    /// <summary>
    /// The query to search for.
    /// </summary>
    public string? Query { get; init; }

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
        RuleFor(x => x.Token)
            .NotEmpty()
            .When(x => x.Token is not null);
        RuleFor(x => x.TokenType)
            .NotNull()
            .IsInEnum()
            .When(x => !string.IsNullOrEmpty(x.Token));
        RuleFor(x => x.TokenType)
            .Null()
            .When(x => string.IsNullOrEmpty(x.Token))
            .WithMessage("Token must be provided when Token Type is specified.");
        RuleFor(x => x.Skip).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Take).GreaterThan(0).LessThanOrEqualTo(SearchDataListItemsQuery.MAX_TAKE);
    }
}

