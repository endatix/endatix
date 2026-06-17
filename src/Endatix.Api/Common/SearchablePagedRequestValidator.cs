using Endatix.Core.Infrastructure.Paging;
using FluentValidation;

namespace Endatix.Api.Common;

/// <summary>
/// Reusable FluentValidation for <see cref="ISearchablePagedRequest"/> implementations.
/// Includes <see cref="PagedRequestValidator"/> plus max page size and search length limits.
/// </summary>
public sealed class SearchablePagedRequestValidator : AbstractValidator<ISearchablePagedRequest>
{
    public SearchablePagedRequestValidator()
    {
        Include(new PagedRequestValidator());

        RuleFor(request => request.PageSize)
            .LessThanOrEqualTo(PagedRequestLimits.MAX_PAGE_SIZE)
            .When(request => request.PageSize.HasValue);

        RuleFor(request => request.Search)
            .MaximumLength(PagedRequestLimits.MAX_SEARCH_LENGTH)
            .When(request => request.Search is not null);
    }
}
