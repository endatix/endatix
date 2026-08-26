using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.CustomQuestions.List;

/// <summary>
/// Query for listing custom questions with pagination, sort, and date bounds.
/// </summary>
/// <param name="Page">Optional page number.</param>
/// <param name="PageSize">Optional page size.</param>
/// <param name="SortBy">Sort field. Defaults to <see cref="CustomQuestionListSortBy.CreatedAt"/>.</param>
/// <param name="SortDescending">When true, sort descending (default).</param>
/// <param name="CreatedFrom">Inclusive UTC start of created-at day filter.</param>
/// <param name="CreatedTo">Exclusive UTC end of created-at day filter.</param>
/// <param name="ModifiedFrom">Inclusive UTC start of modified-at day filter.</param>
/// <param name="ModifiedTo">Exclusive UTC end of modified-at day filter.</param>
public record ListCustomQuestionsQuery(
    int? Page = null,
    int? PageSize = null,
    CustomQuestionListSortBy SortBy = CustomQuestionListSortBy.CreatedAt,
    bool SortDescending = true,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    DateTime? ModifiedFrom = null,
    DateTime? ModifiedTo = null) : IQuery<Result<Paged<CustomQuestion>>>;
