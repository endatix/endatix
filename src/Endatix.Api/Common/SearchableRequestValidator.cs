using Endatix.Core.Infrastructure.Paging;
using FluentValidation;

namespace Endatix.Api.Common;

/// <summary>
/// Reusable FluentValidation for <see cref="ISearchable"/> implementations.
/// </summary>
public sealed class SearchableRequestValidator : AbstractValidator<ISearchable>
{
    public SearchableRequestValidator()
    {
        RuleFor(request => request.Search)
            .MaximumLength(PagedRequestLimits.MAX_SEARCH_LENGTH)
            .When(request => request.Search is not null);
    }
}
