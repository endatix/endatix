using Endatix.Api.Infrastructure;
using Endatix.Core.Authorization.Access;
using Endatix.Core.UseCases.DataLists.Search;
using Endatix.Infrastructure.Features.AccessControl;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.DataLists;

/// <summary>
/// Public endpoint to get choice display values.
/// </summary>
public sealed class GetChoiceDisplayValues(
    IMediator mediator,
    IResourceAccessQuery<PublicFormAccessData, PublicFormAccessContext> publicFormAccessPolicy)
    : Endpoint<GetChoiceDisplayValuesRequest, Results<Ok<IReadOnlyCollection<DataListPublicChoiceModel>>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get(ApiRoutes.Public("forms/{formId}/data-lists/{dataListId}/display-values"));
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Resolve data list display values";
            s.Description =
                "Returns label/value pairs for stored values in a runtime form context. Access is evaluated like access/public/forms/{formId} (optional token + tokenType query parameters; same cached policy).";
            s.Responses[200] = "Data list display values resolved successfully.";
            s.Responses[400] = "Invalid request or access data.";
            s.Responses[404] = "Form or data list not found.";
        });
    }

    public override async Task<Results<Ok<IReadOnlyCollection<DataListPublicChoiceModel>>, ProblemHttpResult>> ExecuteAsync(
        GetChoiceDisplayValuesRequest request,
        CancellationToken ct)
    {
        PublicFormAccessContext accessContext = new(request.FormId, request.Token, request.TokenType);
        var accessDataResult = await publicFormAccessPolicy.GetAccessData(accessContext, ct).ConfigureAwait(false);

        if (!accessDataResult.IsSuccess)
        {
            return accessDataResult.ToProblem();
        }

        GetDataListChoiceDisplayValuesQuery query = new(request.DataListId, request.Values);
        var result = await mediator.Send(query, ct);

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
    /// Optional access or submission token (same semantics as <c>access/public/forms/{formId}</c>).
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// Token type when <see cref="Token"/> is set.
    /// </summary>
    public SubmissionTokenType? TokenType { get; init; }

    /// <summary>
    /// The values to get display values for.
    /// </summary>
    public IReadOnlyCollection<string> Values { get; init; } = [];
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
        RuleFor(x => x.Values)
            .NotNull()
            .Must(values => values.Count > 0)
            .WithMessage("'values' query parameter is required.");
    }
}
