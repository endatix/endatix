using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Paging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.CustomQuestions.List;

/// <summary>
/// Query for listing custom questions with pagination, sort, and date bounds.
/// </summary>
/// <param name="Page">Optional page number.</param>
/// <param name="PageSize">Optional page size.</param>
/// <param name="SortBy">Sort field. Defaults to <see cref="CustomQuestionListSortBy.CreatedAt"/>.</param>
/// <param name="SortDescending">When true, sort descending (default).</param>
/// <param name="Created">Inclusive/exclusive UTC bounds for created-at.</param>
/// <param name="Modified">Inclusive/exclusive UTC bounds for modified-at.</param>
public record ListCustomQuestionsQuery(
    int? Page = null,
    int? PageSize = null,
    CustomQuestionListSortBy SortBy = CustomQuestionListSortBy.CreatedAt,
    bool SortDescending = true,
    UtcDateTimeRange Created = default,
    UtcDateTimeRange Modified = default) : IQuery<Result<Paged<CustomQuestion>>>;
