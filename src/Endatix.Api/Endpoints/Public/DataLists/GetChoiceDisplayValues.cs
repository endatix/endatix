using Endatix.Api.Common;
using Endatix.Api.Common.Security;
using Endatix.Api.Endpoints.DataLists;
using Endatix.Api.Infrastructure;
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
    IResourceAccessQuery<PublicFormAccessData, PublicFormAccessContext> publicFormAccessPolicy)
    : Endpoint<GetChoiceDisplayValuesRequest, Results<Ok<IReadOnlyCollection<DataListPublicChoiceModel>>, ProblemHttpResult>>
{

    /// <inheritdoc />
    public override void Configure()
    {
        Get("forms/{formId}/data-lists/{dataListId}/display-values");
        Group<PublicApiGroup>();
        Policies(AuthorizationPolicies.PublicResourceAccess);
        Summary(s =>
        {
            s.Summary = "Resolve data list display values";
            s.Description =
                "Returns label/value pairs for stored values in a runtime form context. Requires the form access JWT as Authorization: Bearer {jwt}.";
            s.Responses[200] = "Data list display values resolved successfully.";
            s.Responses[400] = "Invalid request or access data.";
            s.Responses[401] = "Unauthorized. Send Authorization: Bearer <jwt>";
            s.Responses[404] = "Form not found.";

        });
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
        RuleFor(x => x.Values)
            .NotNull()
            .Must(values => values.Count > 0)
            .WithMessage("'values' query parameter is required.");
    }
}
