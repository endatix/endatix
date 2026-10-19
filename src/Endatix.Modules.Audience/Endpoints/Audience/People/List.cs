using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Features.People;
using Endatix.Modules.Audience.Shared;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Modules.Audience.Endpoints.Audience.People;

/// <summary>
/// Lists people on a form's audience.
/// </summary>
public sealed class List(
    IMediator mediator,
    ITenantContext tenantContext)
    : Endpoint<ListAudiencePeopleRequest, Results<Ok<Paged<AudiencePersonResponse>>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("forms/{formId}/audience/people");
        Permissions(Actions.Forms.Edit);
        Summary(summary =>
        {
            summary.Summary = "List audience people";
            summary.Description =
                "Lists people on this form's audience. Page size is capped at 5,000. " +
                "test=all|real|test (default all) keeps everyone, only real people, or only test persons; " +
                "the total counts the people kept.";
            summary.Responses[200] = "People retrieved.";
            summary.Responses[400] = "Invalid paging or test filter.";
            summary.Responses[404] = "Form not found.";
        });
        Description(builder => builder
            .Produces<Paged<AudiencePersonResponse>>(200, "application/json")
            .ProducesProblem(400)
            .ProducesProblem(404));
    }

    public override async Task<Results<Ok<Paged<AudiencePersonResponse>>, ProblemHttpResult>> ExecuteAsync(
        ListAudiencePeopleRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new ListPeopleQuery(
                tenantContext.TenantId,
                request.FormId,
                request.Page,
                request.PageSize,
                request.Filter),
            ct);

        return TypedResultsBuilder
            .MapResult(result, Map)
            .SetTypedResults<Ok<Paged<AudiencePersonResponse>>, ProblemHttpResult>();
    }

    private static Paged<AudiencePersonResponse> Map(Paged<PersonDto> paged) =>
        new(
            paged.Page,
            paged.PageSize,
            paged.TotalRecords,
            paged.TotalPages,
            paged.Items.Select(AudiencePersonResponse.FromDto).ToList());
}

/// <summary>
/// Validator for list audience people.
/// </summary>
public sealed class ListAudiencePeopleValidator : Validator<ListAudiencePeopleRequest>
{
    public ListAudiencePeopleValidator()
    {
        RuleFor(request => request.FormId).GreaterThan(0);
        RuleFor(request => request.Page)
            .GreaterThan(0)
            .When(request => request.Page.HasValue);
        RuleFor(request => request.PageSize)
            .GreaterThan(0)
            .LessThanOrEqualTo(AudiencePaging.MaxPageSize)
            .When(request => request.PageSize.HasValue);
        RuleFor(request => request.Test).ValidPeopleTestFilter();
    }
}

/// <summary>
/// Request to list audience people.
/// </summary>
public sealed class ListAudiencePeopleRequest
{
    public long FormId { get; init; }

    public int? Page { get; init; }

    public int? PageSize { get; init; }

    /// <summary><c>all</c> (default), <c>real</c> or <c>test</c>.</summary>
    public string? Test { get; init; }

    /// <summary>The filter <see cref="Test"/> names; the validator has refused any other value.</summary>
    internal PeopleTestFilter Filter => PeopleTestFilterQuery.Parse(Test)!.Value;
}
