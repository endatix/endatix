using FluentValidation;

namespace Endatix.Api.Common;

/// <summary>
/// Reusable Fluent validation for the IPagedRequest implementations.
/// To use in your Validators add this to the validation <c>Include(new PagedRequestValidator());</c>
/// </summary>
public class PagedRequestValidator : AbstractValidator<IPagedRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public PagedRequestValidator()
    {
        RuleFor(x => x.Page)
               .GreaterThan(0)
               .When(x => x.Page.HasValue);

        RuleFor(x => x.PageSize)
                .GreaterThan(0)
                .When(x => x.PageSize.HasValue);
    }
}
