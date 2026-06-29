using Endatix.Core.Infrastructure.Paging;
using FluentValidation;

namespace Endatix.Api.Common;

/// <summary>
/// Reusable FluentValidation for <see cref="ISearchablePagedRequest"/> implementations.
/// Includes <see cref="PageableRequestValidator"/> and <see cref="SearchableRequestValidator"/> rules.
/// </summary>
public sealed class SearchablePagedRequestValidator : AbstractValidator<ISearchablePagedRequest>
{
    public SearchablePagedRequestValidator()
    {
        Include(new PageableRequestValidator());

        RuleFor(request => request.PageSize)
            .LessThanOrEqualTo(PagedRequestLimits.MAX_PAGE_SIZE)
            .When(request => request.PageSize.HasValue);

        Include(new SearchableRequestValidator());
    }
}
