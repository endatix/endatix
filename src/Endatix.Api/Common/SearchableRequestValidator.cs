using Endatix.Core.Infrastructure.Paging;
using FluentValidation;

namespace Endatix.Api.Common;

/// <summary>
/// Reusable FluentValidation for <see cref="ISearchableRequest"/> implementations.
/// </summary>
public sealed class SearchableRequestValidator : AbstractValidator<ISearchableRequest>
{
    public SearchableRequestValidator()
    {
        RuleFor(request => request.Search)
            .MaximumLength(PagedRequestLimits.MAX_SEARCH_LENGTH)
            .When(request => request.Search is not null);
    }
}
