using FluentValidation;
using Endatix.Api.Common;

namespace Endatix.Api;

/// <summary>
/// Reusable Fluent validation for the IPageRequest implementations.
/// To use in your Validators add this to the validation <c>Include(new PageRequestValidator());</c>
/// </summary>
public class PageRequestValidator : AbstractValidator<IPageRequest>
{
    /// <summary>
    /// Default constructor
    /// </summary>
    public PageRequestValidator()
    {
        RuleFor(x => x.Page)
               .GreaterThan(0)
               .When(x => x.Page.HasValue);

        RuleFor(x => x.PageSize)
                .GreaterThan(0)
                .When(x => x.PageSize.HasValue);
    }
}
