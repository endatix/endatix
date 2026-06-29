using Endatix.Api.Common;
using Endatix.Api.Infrastructure;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Forms;
using Endatix.Core.UseCases.Forms.List;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Endpoint for listing forms.
/// </summary>
public class List(IMediator mediator)
    : Endpoint<FormsListRequest, Results<Ok<Paged<FormModel>>, ProblemHttpResult>>
{
    /// <inheritdoc/>
    public override void Configure()
    {
        Get("forms");
        Permissions(Actions.Forms.View);
        Summary(s =>
        {
            s.Summary = "List forms";
            s.Description =
                "Lists tenant forms with paging, search, optional enabled/public filters, filter expressions, and optional folder scope.";
            s.ExampleRequest = new FormsListRequest
            {
                Page = 1,
                PageSize = 20,
                Search = "customer",
                IsEnabled = true,
                IsPublic = false,
                FolderId = 1,
                Filter = ["name:contains:survey"],
            };
            s.ResponseExamples[200] = new Paged<FormModel>(
                page: 1,
                pageSize: 20,
                totalRecords: 1,
                totalPages: 1,
                items:
                [
                    new FormModel
                    {
                        Id = "1",
                        Name = "Customer satisfaction",
                        IsEnabled = true,
                        IsPublic = false,
                        SubmissionsCount = 4,
                    },
                ]);
            s.Responses[200] = "Forms retrieved successfully.";
            s.Responses[400] = "Invalid input data.";
        });
        Description(builder => builder
            .Produces<Paged<FormModel>>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest));
    }

    /// <inheritdoc/>
    public override async Task<Results<Ok<Paged<FormModel>>, ProblemHttpResult>> ExecuteAsync(
        FormsListRequest request,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new ListFormsQuery(
                request.Page,
                request.PageSize,
                request.Search,
                request.IsEnabled,
                request.IsPublic,
                request.Filter,
                request.FolderId),
            ct);

        return TypedResultsBuilder
            .MapResult(result, Map)
            .SetTypedResults<Ok<Paged<FormModel>>, ProblemHttpResult>();
    }

    private static Paged<FormModel> Map(Paged<FormDto> pagedForms)
    {
        var items = pagedForms.Items.ToFormModelList().ToList();
        return new Paged<FormModel>(
            pagedForms.Page,
            pagedForms.PageSize,
            pagedForms.TotalRecords,
            pagedForms.TotalPages,
            items);
    }
}

/// <summary>
/// Validation rules for the <c>FormsListRequest</c> class.
/// </summary>
public sealed class FormsListValidator : Validator<FormsListRequest>
{
    private static readonly Dictionary<string, Type> _filterableFields = new()
    {
        { "id", typeof(long) },
        { "createdAt", typeof(DateTime) },
        { "updatedAt", typeof(DateTime) },
        { "isEnabled", typeof(bool) },
        { "isPublic", typeof(bool) },
        { "themeId", typeof(long) },
        { "activeDefinitionId", typeof(long) },
        { "folderId", typeof(long?) },
        { "name", typeof(string) },
        { "description", typeof(string) },
    };

    /// <summary>
    /// Default constructor.
    /// </summary>
    public FormsListValidator()
    {
        Include(new SearchablePagedRequestValidator());
        Include(new FilteredRequestValidator(_filterableFields));
    }
}

/// <summary>
/// Request model for listing forms.
/// </summary>
public sealed class FormsListRequest : ISearchablePagedRequest, IFilterable
{
    /// <inheritdoc />
    public int? Page { get; set; }

    /// <inheritdoc />
    public int? PageSize { get; set; }

    /// <inheritdoc />
    public string? Search { get; set; }

    /// <summary>
    /// When set, filters forms by enabled state.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// When set, filters forms by public visibility.
    /// </summary>
    public bool? IsPublic { get; set; }

    /// <summary>
    /// Optional filter expressions.
    /// </summary>
    public IEnumerable<string>? Filter { get; set; }

    /// <summary>
    /// Optional folder id to filter forms.
    /// </summary>
    public long? FolderId { get; set; }
}
