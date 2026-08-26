using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.CustomQuestions.List;
using FastEndpoints;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.CustomQuestions;

/// <summary>
/// Endpoint for listing custom questions.
/// </summary>
public class List(IMediator mediator)
    : Endpoint<CustomQuestionsListRequest, Results<Ok<Paged<CustomQuestionModel>>, BadRequest>>
{
    /// <summary>
    /// Configures the endpoint settings.
    /// </summary>
    public override void Configure()
    {
        Get("questions");
        Permissions(Actions.Questions.View);
        Summary(s =>
        {
            s.Summary = "List custom questions";
            s.Description =
                "Lists custom questions for the current tenant with paging, sort, and created/modified date bounds.";
            s.ExampleRequest = new CustomQuestionsListRequest
            {
                Page = 1,
                PageSize = 20,
                SortBy = CustomQuestionListSortBy.CreatedAt,
                SortDir = SortDirection.Desc
            };
            s.Responses[200] = "Custom questions retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
        });
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<Paged<CustomQuestionModel>>, BadRequest>> ExecuteAsync(
        CustomQuestionsListRequest request,
        CancellationToken cancellationToken)
    {
        var sort = request.ToSortRequest(CustomQuestionListSortBy.CreatedAt, SortDirection.Desc);
        var query = new ListCustomQuestionsQuery(
            request.Page,
            request.PageSize,
            sort.Field,
            sort.Direction == SortDirection.Desc,
            request.ToCreatedRange(),
            request.ToModifiedRange());
        var result = await mediator.Send(query, cancellationToken);

        return TypedResultsBuilder
            .MapResult(result, Map)
            .SetTypedResults<Ok<Paged<CustomQuestionModel>>, BadRequest>();
    }

    private static Paged<CustomQuestionModel> Map(Paged<Core.Entities.CustomQuestion> paged)
    {
        var items = CustomQuestionMapper.Map<CustomQuestionModel>(paged.Items).ToList();
        return new Paged<CustomQuestionModel>(
            paged.Page,
            paged.PageSize,
            paged.TotalRecords,
            paged.TotalPages,
            items);
    }
}

/// <summary>
/// Request model for listing custom questions.
/// </summary>
public class CustomQuestionsListRequest :
    IPagedRequest,
    ISortableRequest<CustomQuestionListSortBy>,
    ICreatedRange,
    IModifiedRange
{
    /// <inheritdoc />
    public int? Page { get; set; }

    /// <inheritdoc />
    public int? PageSize { get; set; }

    /// <inheritdoc />
    public CustomQuestionListSortBy? SortBy { get; set; }

    /// <inheritdoc />
    public SortDirection? SortDir { get; set; }

    /// <inheritdoc />
    public string? CreatedFrom { get; set; }

    /// <inheritdoc />
    public string? CreatedTo { get; set; }

    /// <inheritdoc />
    public string? ModifiedFrom { get; set; }

    /// <inheritdoc />
    public string? ModifiedTo { get; set; }
}

/// <summary>
/// Validator for <see cref="CustomQuestionsListRequest"/>.
/// </summary>
public sealed class CustomQuestionsListValidator : Validator<CustomQuestionsListRequest>
{
    public CustomQuestionsListValidator()
    {
        Include(new PageableRequestValidator());

        RuleFor(x => x.PageSize)
            .LessThanOrEqualTo(PagedRequestLimits.MAX_PAGE_SIZE)
            .When(x => x.PageSize.HasValue);

        Include(new SortableRequestValidator<CustomQuestionListSortBy>());
        this.RuleForCalendarDayRange(x => x.CreatedFrom, x => x.CreatedTo, "CreatedFrom");
        this.RuleForCalendarDayRange(x => x.ModifiedFrom, x => x.ModifiedTo, "ModifiedFrom");
    }
}
