using FluentValidation;

namespace Endatix.Api.Common;

/// <summary>
/// Reusable FluentValidation for <see cref="IPageable"/> implementations.
/// To use in your validators add: <c>Include(new PageableRequestValidator());</c>
/// For searchable paged lists, prefer <see cref="SearchablePagedRequestValidator"/>.
/// </summary>
public class PageableRequestValidator : AbstractValidator<IPageable>
{
    public PageableRequestValidator()
    {
        RuleFor(request => request.Page)
            .GreaterThan(0)
            .When(request => request.Page.HasValue);

        RuleFor(request => request.PageSize)
            .GreaterThan(0)
            .When(request => request.PageSize.HasValue);
    }
}
